using System.Security.Cryptography;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.Library.Services.Reader;

public sealed class EbookLibraryService(IDatabaseService database, ApplicationDataCoordinator coordinator, DataPaths paths,
    ManagedBookFiles files, BookFileOperations operations, IEnumerable<IBookParser> parsers, BackupV2Validator validator,
    BackupLimits limits, RestoreRecoveryService recovery, BookDeletionService deletion) : IEbookLibraryService
{
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    public string? LastWarning => deletion.LastWarning ?? operations.LastWarning;
    public Task<ImportResult> ImportAsync(ImportRequest request, IProgress<ImportProgress>? progress, CancellationToken ct)
        => ImportCoreAsync(request, progress, ct, null);
    private async Task<ImportResult> ImportCoreAsync(ImportRequest request, IProgress<ImportProgress>? progress, CancellationToken ct, DataEpoch? expectedEpoch)
    {
        await database.InitializeAsync(); var epoch = expectedEpoch ?? coordinator.CurrentEpoch;
        await using var lease = await coordinator.EnterOperationAsync(epoch, ct);
        var id = Guid.NewGuid(); var fileId = Guid.NewGuid(); var uid = Guid.NewGuid();
        var log = new BookFileJournal(1, id, "import", "Copying", fileId, 0, uid, null, []);
        var written = false; var committed = false; int bookId = 0; string? hash = null;
        void Report(string step, double? fraction) => progress?.Report(new(id, step, fraction));
        try
        {
            var source = Path.GetFullPath(request.SourcePath); ManagedBookFiles.RejectLink(source);
            var format = Path.GetExtension(source).ToLowerInvariant() switch { ".txt" => EbookFormat.Txt, ".epub" => EbookFormat.Epub, _ => throw new ArgumentException("InvalidFormat：请选择TXT或EPUB。") };
            var info = new FileInfo(source);
            if (!info.Exists) throw new ArgumentException("FileMissing：来源文件不存在。");
            if (info.Length > 104857600) throw new ArgumentException("LimitExceeded：单源文件超过100MiB。");
            // Managed data is not a user source: copying it during cleanup would create an ambiguous ownership graph.
            var dataPrefix = paths.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var inboxPrefix = Path.Combine(paths.DirectoryPath, "books") + Path.DirectorySeparatorChar;
            if (source.StartsWith(dataPrefix, StringComparison.Ordinal) && (!source.StartsWith(inboxPrefix, StringComparison.Ordinal) || Path.GetDirectoryName(source) != Path.Combine(paths.DirectoryPath, "books")))
                throw new ArgumentException("InvalidFormat：只允许外部TXT/EPUB或books收件区本层文件。");
            operations.Write(log); written = true;
            var stage = operations.Stage(id); var relative = ManagedBookFiles.BookPrefix(fileId) + (format == EbookFormat.Txt ? "source.txt" : "source.epub");
            var staged = ManagedBookFiles.SafePath(stage, relative); Report("复制并校验来源", 0);
            hash = await Task.Run(() => CopySource(source, staged, ct), ct);
            log = log with { SourceSha256 = hash };
            var existing = await database.ReadAsync(lease, c => c.Table<EbookFile>().FirstOrDefault(f => f.SourceSha256 == hash));
            if (existing is not null)
            {
                await database.ReadAsync(lease, c => { operations.Recover(c, log); return 0; });
                var oldBook = await database.ReadAsync(lease, c => c.Find<Book>(existing.BookId));
                return new(ImportState.Duplicate, existing.BookId, oldBook?.BookUid, "Duplicate：相同字节内容已在书库，未创建副本。");
            }
            Report("解析并写入规范化正文", .35);
            var parser = parsers.Single(p => p.Format == format);
            var parsed = await parser.ParseAsync(staged, stage, new(1, request.EncodingName, 104857600, 314572800, limits.MaxEntries, limits.MaxImagePixels, Path.GetFileNameWithoutExtension(source)), ct);
            var now = DateTime.Now; var ticks = DateTime.UtcNow.Ticks;
            var book = new Book { Id = 1, BookUid = uid, Kind = BookKind.Ebook, Title = parsed.Title, Author = parsed.Author, TotalPages = 0, CreatedAt = now, UpdatedAt = now };
            var file = new EbookFile { BookId = 1, FileId = fileId, Format = parsed.Format, SourceRelativePath = relative, CanonicalRelativePath = parsed.PreparedDocumentRelativePath,
                SourceSha256 = hash, CanonicalSha256 = parsed.CanonicalSha256, SourceBytes = new FileInfo(staged).Length, ParserVersion = parsed.ParserVersion, ImportedUtcTicks = ticks, Status = (int)EbookFileStatus.Ready };
            var manifest = BackupJson.Read<CanonicalManifest>(await File.ReadAllBytesAsync(ManagedBookFiles.SafePath(stage, file.CanonicalRelativePath), ct));
            var key = new DocumentKey(fileId, hash, parsed.CanonicalSha256, parsed.ParserVersion);
            var initial = new EbookProgress { BookId = 1, DocumentKeyJson = ReaderContractValidator.WriteDocument(key), LastPositionJson = ReaderContractValidator.WritePosition(new ReaderPosition(key, manifest.Chapters[0].Key, 0)), LastOpenedUtcTicks = ticks, LastSavedUtcTicks = ticks };
            var data = validator.Validate(new(2, [new(book, uid, BookKind.Ebook)], [], [], [], [file], [initial], [], [], [], []));
            var inventory = await Task.Run(() => ManagedBookInventory.Inspect(stage, data, limits), ct);
            operations.Write(log = log with { Stage = "Prepared", Files = inventory });
            ct.ThrowIfCancellationRequested(); Report("安装书籍文件", .75);
            await Task.Run(() =>
            {
                files.Checkpoint("import.before-install");
                files.CreateDirectory(Path.GetDirectoryName(operations.Managed(fileId))!);
                files.MoveDirectory(ManagedBookFiles.SafePath(stage, ManagedBookFiles.BookPrefix(fileId).TrimEnd('/')), operations.Managed(fileId));
                files.Checkpoint("import.after-install"); operations.Write(log = log with { Stage = "Installed" });
                ManagedBookInventory.Inspect(paths.DirectoryPath, data, limits, false);
            }, ct);
            ct.ThrowIfCancellationRequested(); files.Checkpoint("import.before-transaction");
            bookId = await database.WriteAsync(lease, c =>
            {
                ct.ThrowIfCancellationRequested(); book.Id = 0; c.Insert(book); file.BookId = initial.BookId = book.Id;
                c.Insert(file); c.Insert(initial); files.Checkpoint("import.in-transaction"); return book.Id;
            });
            committed = true; files.Checkpoint("import.after-commit");
            await database.ReadAsync(lease, c => { operations.Recover(c, log); return 0; });
            Report("导入完成", 1); return new(ImportState.Imported, bookId, uid, "导入成功。" + (parsed.Warnings.Count == 0 ? "" : "注意：" + string.Join("；", parsed.Warnings)));
        }
        catch (Exception ex)
        {
            var warning = "";
            if (written)
            {
                try { await database.ReadAsync(lease, c => { operations.Recover(c, log); return 0; }); }
                catch (RecoveryCleanupException) { warning = "临时文件清理待重试。"; }
                catch (Exception)
                { coordinator.RequireRecovery("RecoveryRequired：导入收尾未能确认，请关闭应用并保留operations/staging/trash后重启。"); warning = "RecoveryRequired：请保留恢复材料并重启。"; }
            }
            if (committed) return new(ImportState.Imported, bookId, uid, "导入已提交。" + warning);
            if (ex is OperationCanceledException) return new(ImportState.Cancelled, null, null, "OperationCancelled：已取消导入。" + warning);
            if (hash is not null && string.IsNullOrEmpty(warning))
            {
                var existing = await database.ReadAsync(lease, c => c.Table<EbookFile>().FirstOrDefault(f => f.SourceSha256 == hash));
                if (existing is not null) return new(ImportState.Duplicate, existing.BookId, await database.ReadAsync(lease, c => c.Find<Book>(existing.BookId)?.BookUid), "Duplicate：相同内容已由另一导入提交。");
            }
            return new(ImportState.Rejected, null, null, string.IsNullOrEmpty(warning) ? ex.Message : warning);
        }
    }
    private string CopySource(string source, string staged, CancellationToken ct)
    {
        var before = new FileInfo(source); var length = before.Length; var modified = before.LastWriteTimeUtc;
        files.CreateDirectory(Path.GetDirectoryName(staged)!);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; int count; long copied = 0;
        while ((count = input.Read(buffer)) > 0)
        {
            ct.ThrowIfCancellationRequested(); copied += count; if (copied > 104857600) throw new ArgumentException("LimitExceeded：来源增长超过100MiB。");
            output.Write(buffer, 0, count); sha.AppendData(buffer, 0, count); files.Checkpoint("import.copying");
        }
        output.Flush(true); var hash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        before.Refresh();
        if (copied != length || before.Length != length || before.LastWriteTimeUtc != modified || ManagedBookFiles.HashFile(source) != hash)
            throw new IOException("SourceChanging：来源文件仍在变化，请稍后刷新重试。");
        return hash;
    }
    public async Task<ScanResult> ScanInboxAsync(CancellationToken ct)
    {
        await _scanGate.WaitAsync(ct);
        try
        {
            await ReconcileAsync(ct);
            var scanEpoch = coordinator.CurrentEpoch;
            var inbox = ManagedBookFiles.SafePath(paths.DirectoryPath, "books"); files.CreateDirectory(inbox);
            var candidates = Directory.EnumerateFiles(inbox).Where(p => Path.GetExtension(p).Equals(".txt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(p).Equals(".epub", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).ToArray();
            var observed = new List<(string FullName, long Length, DateTime LastWriteTimeUtc)>(); var items = new List<ImportResult>();
            foreach (var path in candidates)
            {
                try { ManagedBookFiles.RejectLink(path); var f = new FileInfo(path); observed.Add((f.FullName, f.Length, f.LastWriteTimeUtc)); }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                { items.Add(new(ImportState.Rejected, null, null, ex.Message)); }
            }
            files.Checkpoint("scan.observed"); await Task.Delay(150, ct);
            foreach (var candidate in observed)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    ManagedBookFiles.RejectLink(candidate.FullName); var current = new FileInfo(candidate.FullName);
                    if (!current.Exists || current.Length != candidate.Length || current.LastWriteTimeUtc != candidate.LastWriteTimeUtc)
                    { items.Add(new(ImportState.Rejected, null, null, "SourceChanging：收件文件仍在变化，稍后再试。")); continue; }
                    // Local inbox observations survive deletion/restore. Keeping the source must not recreate a deleted book.
                    // Explicit file-picker import bypasses these observations and remains an intentional user action.
                    var sourceHash = await Task.Run(() => ManagedBookFiles.HashFile(candidate.FullName), ct);
                    current.Refresh();
                    if (current.Length != candidate.Length || current.LastWriteTimeUtc != candidate.LastWriteTimeUtc)
                    { items.Add(new(ImportState.Rejected, null, null, "SourceChanging：收件文件仍在变化，稍后再试。")); continue; }
                    var observationKey = "InboxSeen/" + ManagedBookFiles.Sha256(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(candidate.FullName)));
                    var observation = await database.ReadAsync(c => new { Seen = c.Find<AppMetadata>(observationKey)?.Value,
                        Exists = c.Table<EbookFile>().Any(f => f.SourceSha256 == sourceHash) });
                    if (observation.Seen == sourceHash && !observation.Exists) continue;
                    var result = await ImportCoreAsync(new(candidate.FullName), null, ct, scanEpoch); items.Add(result);
                    if (result.State is ImportState.Imported or ImportState.Duplicate)
                        await database.WriteAsync(c => { c.InsertOrReplace(new AppMetadata { Key = observationKey, Value = sourceHash }); return 0; });
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                { items.Add(new(ImportState.Rejected, null, null, ex.Message)); }
            }
            return new(items.Count(i => i.State == ImportState.Imported), items.Count(i => i.State == ImportState.Duplicate), items.Count(i => i.State == ImportState.Rejected), items);
        }
        finally { _scanGate.Release(); }
    }
    public async Task ReconcileAsync(CancellationToken ct)
    {
        await database.InitializeAsync(); await using var lease = await coordinator.EnterMaintenanceAsync(MaintenanceKind.Reconcile, ct);
        await database.ReadAsync(lease, c =>
        {
            ct.ThrowIfCancellationRequested();
            try { recovery.RecoverPending(c); } catch (RecoveryCleanupException) { /* Verified business data; cleanup remains retryable. */ }
            operations.Reconcile(c); return 0;
        });
    }
    public Task<DeleteImpact> GetDeleteImpactAsync(int bookId, CancellationToken ct) => deletion.GetImpactAsync(bookId, ct);
    public Task DeleteBookAsync(int bookId, Guid bookUid, DataEpoch expectedEpoch, CancellationToken ct) => deletion.DeleteAsync(bookId, bookUid, expectedEpoch, ct);
}
