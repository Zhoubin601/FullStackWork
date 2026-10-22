using SQLite;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.Library.Services.Reader;

// No IDatabaseService dependency: startup calls this with its original shared connection, before business readiness.
public sealed class BookFileOperations(DataPaths paths, ManagedBookFiles files, BackupLimits limits)
{
    public string? LastWarning { get; private set; }
    public string Root => paths.DirectoryPath;
    public string JournalPath(BookFileJournal log) => ManagedBookFiles.SafePath(Root, "operations/" + log.Kind + "-" + log.OperationId.ToString("D") + ".json");
    public string Stage(Guid id) => ManagedBookFiles.SafePath(Root, "staging/" + id.ToString("D"));
    public string Trash(Guid id) => ManagedBookFiles.SafePath(Root, "trash/" + id.ToString("D"));
    public string Managed(Guid id) => ManagedBookFiles.SafePath(Root, ManagedBookFiles.BookPrefix(id).TrimEnd('/'));
    public void Write(BookFileJournal log) => files.WriteBookJournal(JournalPath(log), log);
    public BookFileJournal Read(string path)
    {
        try
        {
            ManagedBookFiles.RejectLink(path);
            if (new FileInfo(path).Length > limits.MaxJsonBytes) throw new IOException("操作日志超限。");
            var envelope = BackupJson.Read<BookFileJournalEnvelope>(File.ReadAllBytes(path)); var log = envelope.Payload;
            if (envelope.Version != 1 || log is null || log.Version != 1 || log.OperationId == Guid.Empty ||
                log.Kind is not ("import" or "delete" or "orphan") || Path.GetFullPath(path) != JournalPath(log) ||
                log.Stage is not ("Copying" or "Prepared" or "Installed" or "CleanupImported" or "CleanupDiscarded" or "CleanupDeleted") ||
                envelope.PayloadSha256 != ManagedBookFiles.Sha256(BackupJson.Write(log)) || log.Files is null || log.Files.Count > limits.MaxEntries ||
                log.Files.Any(f => f is null) || log.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != log.Files.Count ||
                log.Kind == "import" && (log.FileId == Guid.Empty || log.BookUid == Guid.Empty) ||
                log.Kind == "delete" && (log.BookId <= 0 || log.BookUid == Guid.Empty) || log.Kind == "orphan" && log.FileId == Guid.Empty ||
                log.SourceSha256 is not null && !ReaderContractValidator.IsSha256(log.SourceSha256)) throw new IOException("操作日志字段不足。");
            foreach (var f in log.Files)
            { ManagedBookFiles.ValidateRelative(f.Path); if (!f.Path.StartsWith(ManagedBookFiles.BookPrefix(log.FileId), StringComparison.Ordinal) || f.Bytes < 0 || f.Bytes > limits.MaxEntryBytes || !ReaderContractValidator.IsSha256(f.Sha256)) throw new IOException("操作文件清单无效。"); }
            if (log.Kind == "import" && log.Stage != "Copying" && log.Stage != "CleanupDiscarded" && (log.SourceSha256 is null || log.Files.Count < 3)) throw new IOException("导入日志缺少完整清单。");
            return log;
        }
        catch (Exception ex) { throw new InvalidOperationException("RecoveryRequired：导入/删除日志无法确认，请保留 operations/staging/trash/books 后检查。", ex); }
    }
    public static BackupDataV2 Single(BackupDataV2 data, int id)
    {
        var quoteIds = data.Quotes.Where(q => q.Quote.BookId == id).Select(q => q.Quote.Id).ToHashSet();
        var batchIds = data.Batches.Where(b => b.BookId == id).Select(b => b.BatchId).ToHashSet();
        return data with { Books = data.Books.Where(b => b.Book.Id == id).ToArray(), ReadingRecords = data.ReadingRecords.Where(r => r.BookId == id).ToArray(),
            Quotes = data.Quotes.Where(q => quoteIds.Contains(q.Quote.Id)).ToArray(), ReadingPlans = data.ReadingPlans.Where(p => p.BookId == id).ToArray(),
            Files = data.Files.Where(f => f.BookId == id).ToArray(), Progress = data.Progress.Where(p => p.BookId == id).ToArray(), Sessions = data.Sessions.Where(s => s.BookId == id).ToArray(),
            Origins = data.Origins.Where(o => quoteIds.Contains(o.QuoteId)).ToArray(), Batches = data.Batches.Where(b => batchIds.Contains(b.BatchId)).ToArray(),
            BatchItems = data.BatchItems.Where(b => batchIds.Contains(b.BatchId)).ToArray() };
    }
    public IReadOnlyList<BackupFileEntry> PhysicalFiles(Guid fileId, string? root = null)
    {
        root ??= Root; var directory = ManagedBookFiles.SafePath(root, ManagedBookFiles.BookPrefix(fileId).TrimEnd('/'));
        var result = new List<BackupFileEntry>();
        void Walk(string current)
        {
            ManagedBookFiles.RejectLink(current);
            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                ManagedBookFiles.RejectLink(path);
                if (Directory.Exists(path)) Walk(path);
                else
                {
                    if (result.Count >= limits.MaxEntries || new FileInfo(path).Length > limits.MaxEntryBytes) throw new IOException("受管文件清单超限。");
                    result.Add(new(Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'), new FileInfo(path).Length, ManagedBookFiles.HashFile(path)));
                }
            }
        }
        if (Directory.Exists(directory)) Walk(directory);
        return result.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
    }
    private void VerifyPhysical(BookFileJournal log, string root)
    { if (!ManagedBookInventory.Same(PhysicalFiles(log.FileId, root), log.Files)) throw new IOException("受管文件与操作日志不一致，已保留原文件。"); }
    public void Recover(SQLiteConnection c, BookFileJournal log)
    {
        var row = log.FileId == Guid.Empty ? null : c.Table<EbookFile>().FirstOrDefault(f => f.FileId == log.FileId);
        var book = log.BookId > 0 ? c.Find<Book>(log.BookId) : row is not null ? c.Find<Book>(row.BookId) : null;
        if (log.Kind == "import")
        {
            if (row is not null)
            {
                if (book?.BookUid != log.BookUid || row.SourceSha256 != log.SourceSha256 || log.Stage is "Copying" or "CleanupDiscarded") throw new InvalidOperationException("RecoveryRequired：导入实体与日志身份不一致。");
                var staged = ManagedBookFiles.SafePath(Stage(log.OperationId), ManagedBookFiles.BookPrefix(log.FileId).TrimEnd('/'));
                if (!Directory.Exists(Managed(log.FileId)) && Directory.Exists(staged))
                { VerifyPhysical(log, Stage(log.OperationId)); files.CreateDirectory(Path.GetDirectoryName(Managed(log.FileId))!); files.MoveDirectory(staged, Managed(log.FileId)); }
                VerifyPhysical(log, Root);
                var data = Single(BackupDataStore.Snapshot(c), row.BookId);
                ManagedBookInventory.Inspect(Root, data, limits, false);
                Write(log = log with { Stage = "CleanupImported" });
            }
            else
            {
                if (c.Table<Book>().Any(b => b.BookUid == log.BookUid)) throw new InvalidOperationException("RecoveryRequired：导入关联不完整。");
                if (Directory.Exists(Managed(log.FileId)))
                {
                    if (log.Stage == "Copying") throw new InvalidOperationException("RecoveryRequired：未准备完成的导入出现目标目录。");
                    VerifyPhysical(log, Root); MoveToTrash(log);
                }
                Write(log = log with { Stage = "CleanupDiscarded" });
            }
        }
        else if (log.Kind == "delete" && book?.BookUid == log.BookUid)
        {
            if (Directory.Exists(Trash(log.OperationId))) throw new InvalidOperationException("RecoveryRequired：删除尚未提交但存在隔离文件。");
            RemoveJournal(log); return; // No live file movement occurs before the SQL commit.
        }
        else
        {
            if (row is not null) throw new InvalidOperationException("RecoveryRequired：待清理文件仍被书库引用，已停止清理。");
            if (log.FileId != Guid.Empty && Directory.Exists(Managed(log.FileId)))
            {
                VerifyPhysical(log, Root);
                try { MoveToTrash(log); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { LastWarning = "业务行已删除，文件已确认无引用；隔离清理待下次启动/刷新重试。"; throw new RecoveryCleanupException(LastWarning, ex); }
            }
            if (log.Kind == "orphan") { RemoveJournal(log); LastWarning = "已将无关联受管目录隔离到trash，请复核；来源文件保留。"; return; }
            Write(log = log with { Stage = "CleanupDeleted" });
        }
        Cleanup(log);
    }
    private void MoveToTrash(BookFileJournal log)
    {
        var target = ManagedBookFiles.SafePath(Trash(log.OperationId), ManagedBookFiles.BookPrefix(log.FileId).TrimEnd('/'));
        files.CreateDirectory(Path.GetDirectoryName(target)!); files.Checkpoint(log.Kind + ".before-trash");
        files.MoveDirectory(Managed(log.FileId), target); files.Checkpoint(log.Kind + ".after-trash");
    }
    private void RemoveJournal(BookFileJournal log)
    {
        var path = JournalPath(log);
        foreach (var temp in Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".*.tmp"))
        { ManagedBookFiles.RejectLink(temp); files.DeleteFile(temp); }
        files.DeleteFile(path);
    }
    private void Cleanup(BookFileJournal log)
    {
        try { files.Checkpoint(log.Kind + ".cleanup"); files.DeleteDirectory(Stage(log.OperationId)); files.DeleteDirectory(Trash(log.OperationId)); RemoveJournal(log); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { LastWarning = "业务提交结果已确认；临时文件清理未完成，下次启动/刷新会重试。"; throw new RecoveryCleanupException(LastWarning, ex); }
    }
    public void Reconcile(SQLiteConnection c)
    {
        var data = BackupDataStore.Snapshot(c);
        foreach (var file in data.Files)
        {
            var status = EbookFileStatus.Ready;
            try { ManagedBookInventory.Inspect(Root, Single(data, file.BookId), limits, false); }
            catch (Exception ex) when (ex is ArgumentException or IOException or System.Text.Json.JsonException)
            { status = ex.Message.Contains("缺失", StringComparison.Ordinal) || ex is FileNotFoundException or DirectoryNotFoundException ? EbookFileStatus.Missing : EbookFileStatus.Invalid; }
            if (file.Status != (int)status) c.Execute("UPDATE EbookFile SET Status=? WHERE BookId=?", (int)status, file.BookId);
        }
        var managed = ManagedBookFiles.SafePath(Root, "books/managed"); if (!Directory.Exists(managed)) return;
        var referenced = data.Files.Select(f => f.FileId).ToHashSet();
        var operations = ManagedBookFiles.SafePath(Root, "operations");
        if (Directory.Exists(operations))
            foreach (var path in Directory.EnumerateFiles(operations, "*.json").Where(p => !Path.GetFileName(p).StartsWith("restore-", StringComparison.Ordinal))) referenced.Add(Read(path).FileId);
        foreach (var entry in Directory.EnumerateFileSystemEntries(managed))
        {
            ManagedBookFiles.RejectLink(entry);
            if (!Directory.Exists(entry) || !Guid.TryParseExact(Path.GetFileName(entry), "D", out var id)) throw new InvalidOperationException("RecoveryRequired：受管目录存在未知内容，请保留后检查。");
            if (referenced.Contains(id)) continue;
            var log = new BookFileJournal(1, Guid.NewGuid(), "orphan", "Prepared", id, 0, Guid.Empty, null, PhysicalFiles(id)); Write(log); Recover(c, log);
        }
    }
}
