using SQLite;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Backup;

// Called with the one shared startup connection, under maintenance, before migration/business readiness.
public sealed class RestoreRecoveryService
{
    private readonly DataPaths _paths;
    private readonly ManagedBookFiles _files;
    private readonly CompleteBackupArchive _archive;
    private readonly BackupV2Validator _validator;
    private readonly BackupLimits _limits;
    private readonly Services.Reader.BookFileOperations _bookOperations;
    public RestoreRecoveryService(DataPaths paths, ManagedBookFiles files, CompleteBackupArchive archive, BackupV2Validator validator, BackupLimits limits)
        : this(paths, files, archive, validator, limits, new(paths, files, limits)) { }
    public RestoreRecoveryService(DataPaths paths, ManagedBookFiles files, CompleteBackupArchive archive, BackupV2Validator validator, BackupLimits limits, Services.Reader.BookFileOperations bookOperations)
    { _paths = paths; _files = files; _archive = archive; _validator = validator; _limits = limits; _bookOperations = bookOperations; }
    public void ReconcileBookFiles(SQLiteConnection c) => _bookOperations.Reconcile(c);
    public string JournalPath(Guid id) => ManagedBookFiles.SafePath(_paths.DirectoryPath, "operations/restore-" + id.ToString("D") + ".json");
    public string StageRoot(Guid id) => ManagedBookFiles.SafePath(_paths.DirectoryPath, "staging/" + id.ToString("D"));
    public string TrashRoot(Guid id) => ManagedBookFiles.SafePath(_paths.DirectoryPath, "trash/" + id.ToString("D"));
    public string ManagedRoot => ManagedBookFiles.SafePath(_paths.DirectoryPath, "books/managed");
    public void RecoverPending(SQLiteConnection c)
    {
        var directory = ManagedBookFiles.SafePath(_paths.DirectoryPath, "operations");
        if (!Directory.Exists(directory)) return;
        var entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        var journals = entries.Where(p => p.EndsWith(".json", StringComparison.Ordinal)).ToArray();
        // A killed atomic rewrite may leave a temp beside its still-valid primary journal.
        foreach (var entry in entries.Except(journals))
        {
            var primary = journals.FirstOrDefault(p => entry.StartsWith(p + ".", StringComparison.Ordinal) && entry.EndsWith(".tmp", StringComparison.Ordinal) && Guid.TryParseExact(entry[(p.Length + 1)..^4], "N", out _));
            if (primary is null) throw new InvalidOperationException("RecoveryRequired：存在不完整或未知操作日志，请保留恢复材料后检查。");
            ManagedBookFiles.RejectLink(entry);
        }
        var restores = journals.Where(p => Path.GetFileName(p).StartsWith("restore-", StringComparison.Ordinal)).ToArray();
        if (journals.Length > _limits.MaxEntries || restores.Length > 1 || restores.Length == 1 && journals.Length != 1)
            throw new InvalidOperationException("RecoveryRequired：存在未知或多份操作日志。请保留 operations、staging、trash 和 backups 后检查。");
        if (restores.Length == 1) { Recover(c, Read(restores[0])); return; }
        var bookLogs = journals.Select(_bookOperations.Read).ToArray(); // Validate all identities before mutating any recovery material.
        RecoveryCleanupException? cleanup = null;
        foreach (var log in bookLogs)
        {
            try { _bookOperations.Recover(c, log); }
            catch (RecoveryCleanupException ex) { cleanup = ex; }
            catch (Exception ex) { throw new InvalidOperationException("RecoveryRequired：导入/删除对账不足以确认，请保留operations/staging/trash/books后检查。", ex); }
        }
        if (cleanup is not null) throw cleanup;
    }
    public RestoreJournal Read(string path)
    {
        try
        {
            ManagedBookFiles.RejectLink(path);
            if (new FileInfo(path).Length > _limits.MaxJsonBytes) throw new IOException("日志超限。");
            var envelope = BackupJson.Read<RestoreJournalEnvelope>(File.ReadAllBytes(path)); var log = envelope.Payload;
            if (envelope.Version != 1 || log is null || log.Version != 1 || log.RestoreId == Guid.Empty || Path.GetFileName(path) != "restore-" + log.RestoreId.ToString("D") + ".json" ||
                envelope.PayloadSha256 != ManagedBookFiles.Sha256(BackupJson.Write(log)) || log.Stage is not ("Prepared" or "Isolated" or "Installed" or "Committed" or "RolledBack" or "CleanupCommitted" or "CleanupRolledBack") ||
                log.AutomaticBackupPath != "backups/auto-backup-" + log.RestoreId.ToString("D") + ".zip" || log.OldFiles is null || log.NewFiles is null ||
                !Services.Reader.ReaderContractValidator.IsSha256(log.OldDataSha256) || !Services.Reader.ReaderContractValidator.IsSha256(log.NewDataSha256)) throw new IOException("日志字段或摘要不足。");
            foreach (var list in new[] { log.OldFiles, log.NewFiles })
            {
                if (list.Count > _limits.MaxEntries || list.Any(f => f is null) || list.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count) throw new IOException("日志清单无效。");
                foreach (var file in list)
                { ManagedBookFiles.ValidateRelative(file.Path); if (!file.Path.StartsWith("books/managed/", StringComparison.Ordinal) || file.Bytes < 0 || file.Bytes > _limits.MaxEntryBytes || !Services.Reader.ReaderContractValidator.IsSha256(file.Sha256)) throw new IOException("日志文件清单无效。"); }
            }
            return log;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new InvalidOperationException("RecoveryRequired：恢复日志无法确认，请保留两套数据及自动备份，勿删除 operations/staging/trash/backups。", ex); }
    }
    public void Recover(SQLiteConnection c, RestoreJournal log)
    {
        try
        {
            if (log.Stage is "CleanupCommitted" or "CleanupRolledBack") { Cleanup(log.RestoreId); return; }
            var current = _validator.Validate(BackupDataStore.Snapshot(c));
            var committed = BackupDataStore.LastRestoreId(c) == log.RestoreId.ToString("D");
            var actualHash = BackupDataStore.Hash(current);
            if (actualHash != (committed ? log.NewDataSha256 : log.OldDataSha256)) throw new IOException("数据库与日志的数据集摘要不一致。");
            using var automatic = _archive.Prepare(ManagedBookFiles.SafePath(_paths.DirectoryPath, log.AutomaticBackupPath), default);
            if (BackupDataStore.Hash(automatic.Data) != log.OldDataSha256 || !ManagedBookInventory.Same(automatic.Files, log.OldFiles)) throw new IOException("自动备份与旧集摘要不一致。");
            var stage = StageRoot(log.RestoreId); var trash = TrashRoot(log.RestoreId);
            var oldManaged = ManagedBookFiles.SafePath(trash, "books/managed");
            if (committed)
            {
                if (!Directory.Exists(ManagedRoot))
                {
                    var stagedManaged = ManagedBookFiles.SafePath(stage, "books/managed");
                    if (!Directory.Exists(stagedManaged)) throw new IOException("已提交目标文件缺失。");
                    VerifySet(stage, current, log.NewFiles);
                    _files.CreateDirectory(Path.GetDirectoryName(ManagedRoot)!); _files.MoveDirectory(stagedManaged, ManagedRoot);
                }
                VerifySet(_paths.DirectoryPath, current, log.NewFiles);
            }
            else
            {
                if (Directory.Exists(oldManaged))
                {
                    VerifySet(trash, automatic.Data, log.OldFiles);
                    if (Directory.Exists(ManagedRoot))
                    {
                        VerifyPhysical(_paths.DirectoryPath, log.NewFiles);
                        var rejected = ManagedBookFiles.SafePath(trash, "uncommitted-new/books/managed");
                        _files.CreateDirectory(Path.GetDirectoryName(rejected)!); _files.MoveDirectory(ManagedRoot, rejected);
                    }
                    _files.CreateDirectory(Path.GetDirectoryName(ManagedRoot)!); _files.MoveDirectory(oldManaged, ManagedRoot);
                }
                else if (log.HadManagedDirectory)
                {
                    if (!Directory.Exists(ManagedRoot)) throw new IOException("原受管目录及隔离副本均缺失。");
                    VerifySet(_paths.DirectoryPath, automatic.Data, log.OldFiles);
                }
                else if (Directory.Exists(ManagedRoot))
                {
                    VerifyPhysical(_paths.DirectoryPath, log.NewFiles);
                    var rejected = ManagedBookFiles.SafePath(trash, "uncommitted-new/books/managed");
                    _files.CreateDirectory(Path.GetDirectoryName(rejected)!); _files.MoveDirectory(ManagedRoot, rejected);
                }
                VerifySet(_paths.DirectoryPath, current, log.OldFiles);
            }
            // Only verified whole datasets permit cleanup. Failure leaves the log for a safe retry.
            _files.WriteJournal(JournalPath(log.RestoreId), log with { Stage = committed ? "CleanupCommitted" : "CleanupRolledBack" });
            Cleanup(log.RestoreId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not RecoveryCleanupException)
        { throw new InvalidOperationException("RecoveryRequired：无法确定完整旧集或新集，已停止主业务。请保留 operations/staging/trash/backups 后检查。", ex); }
    }
    private void VerifySet(string root, BackupDataV2 data, IReadOnlyList<BackupFileEntry> expected)
    { if (!ManagedBookInventory.Same(ManagedBookInventory.Inspect(root, data, _limits), expected)) throw new IOException("恢复文件集合不一致。"); }
    private static void VerifyPhysical(string root, IReadOnlyList<BackupFileEntry> expected)
    {
        ManagedBookInventory.Verify(root, expected);
        var managed = ManagedBookFiles.SafePath(root, "books/managed");
        var names = new List<string>();
        void Walk(string dir)
        {
            ManagedBookFiles.RejectLink(dir);
            foreach (var path in Directory.EnumerateFileSystemEntries(dir))
            { ManagedBookFiles.RejectLink(path); if (Directory.Exists(path)) Walk(path); else names.Add(Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/')); }
        }
        if (Directory.Exists(managed)) Walk(managed);
        if (!names.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Select(f => f.Path))) throw new IOException("恢复目录包含清单外内容。");
    }
    public void Cleanup(Guid id)
    {
        try { _files.Checkpoint("restore.cleanup"); _files.DeleteDirectory(TrashRoot(id)); _files.DeleteDirectory(StageRoot(id)); foreach (var temp in Directory.EnumerateFiles(Path.GetDirectoryName(JournalPath(id))!, Path.GetFileName(JournalPath(id)) + ".*.tmp")) { ManagedBookFiles.RejectLink(temp); _files.DeleteFile(temp); }
            _files.DeleteFile(JournalPath(id)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new RecoveryCleanupException("数据集已确认完整；临时资源清理未完成，下次启动会重试。", ex); }
    }
}
public sealed class RecoveryCleanupException(string message, Exception inner) : IOException(message, inner);
