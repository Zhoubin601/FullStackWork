using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Backup;

public sealed class CompleteBackupService : ICompleteBackupService
{
    private readonly IDatabaseService _database;
    private readonly ApplicationDataCoordinator _coordinator;
    private readonly DataPaths _paths;
    private readonly CompleteBackupArchive _archive;
    private readonly RestoreRecoveryService _recovery;
    private readonly ManagedBookFiles _files;
    private readonly BackupV2Validator _validator;
    private readonly BackupLimits _limits;
    private readonly IEnumerable<IDataSetInvalidationService> _invalidators;
    public CompleteBackupService(IDatabaseService database, ApplicationDataCoordinator coordinator, DataPaths paths,
        CompleteBackupArchive archive, RestoreRecoveryService recovery, ManagedBookFiles files, BackupV2Validator validator, BackupLimits limits,
        IEnumerable<IDataSetInvalidationService> invalidators)
    { _database = database; _coordinator = coordinator; _paths = paths; _archive = archive; _recovery = recovery; _files = files; _validator = validator; _limits = limits; _invalidators = invalidators; }
    public async Task<BackupValidationResult> ValidateAsync(string path, CancellationToken ct)
    {
        try
        {
            using var prepared = await Task.Run(() => _archive.Prepare(path, ct), ct);
            return new(true, prepared.InputVersion, prepared.Data.Books.Count, prepared.Files.Count, [], prepared.InputVersion == 1 ? ["旧JSON恢复会整体替换全部业务数据，包括当前电子书与会话。"] : []);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or System.Text.Json.JsonException or OverflowException)
        { return new(false, 0, 0, 0, ["备份校验失败：" + ex.Message], []); }
    }
    public async Task<BackupExportResult> ExportAsync(string path, CancellationToken ct)
    {
        await _database.InitializeAsync();
        var full = Path.GetFullPath(path); var root = _paths.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (full.StartsWith(root, StringComparison.Ordinal) && Path.GetDirectoryName(full) != _paths.DirectoryPath && !full.StartsWith(Path.Combine(_paths.DirectoryPath, "backups") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("请将备份保存到书库目录外或 backups 目录。");
        await using var maintenance = await _coordinator.EnterMaintenanceAsync(MaintenanceKind.Backup, ct);
        await EnsureNoPendingRecoveryAsync(maintenance);
        return await ExportUnderLeaseAsync(path, maintenance, ct);
    }
    private async Task EnsureNoPendingRecoveryAsync(IDataMaintenanceLease lease)
    {
        try { await _database.ReadAsync(lease, c => { _recovery.RecoverPending(c); return 0; }); }
        catch (RecoveryCleanupException) { throw new InvalidOperationException("上次临时资源尚未清理，请关闭应用重启后再备份或恢复。"); }
        catch (Exception ex) { _coordinator.RequireRecovery("RecoveryRequired：需重新启动应用完成数据对账。"); throw new InvalidOperationException("RecoveryRequired：未完成恢复日志阻止此次操作。", ex); }
    }
    private async Task<BackupExportResult> ExportUnderLeaseAsync(string path, IDataMaintenanceLease lease, CancellationToken ct)
    {
        var data = _validator.Validate(await _database.ReadAsync(lease, BackupDataStore.Snapshot));
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested(); var inventory = ManagedBookInventory.Inspect(_paths.DirectoryPath, data, _limits);
            _archive.Write(_paths.DirectoryPath, data, inventory, path, ct);
            return new BackupExportResult(Path.GetFullPath(path), ManagedBookFiles.HashFile(path), inventory.Count);
        }, ct);
    }
    public async Task<RestoreResult> RestoreAsync(string path, CancellationToken ct)
    {
        using var target = await Task.Run(() => _archive.Prepare(path, ct), ct); // Invalid input never changes the current data.
        await _database.InitializeAsync();
        await using var lease = await _coordinator.EnterMaintenanceAsync(MaintenanceKind.Restore, ct);
        await EnsureNoPendingRecoveryAsync(lease);
        var id = Guid.NewGuid(); var autoRelative = "backups/auto-backup-" + id.ToString("D") + ".zip";
        var automaticPath = ManagedBookFiles.SafePath(_paths.DirectoryPath, autoRelative);
        var old = _validator.Validate(await _database.ReadAsync(lease, BackupDataStore.Snapshot));
        var automatic = await ExportUnderLeaseAsync(automaticPath, lease, ct);
        using (var verified = await Task.Run(() => _archive.Prepare(automatic.Path, ct), ct))
            if (BackupDataStore.Hash(verified.Data) != BackupDataStore.Hash(old)) throw new IOException("自动备份复核失败，原数据未改变。");
        var oldFiles = await Task.Run(() => ManagedBookInventory.Inspect(_paths.DirectoryPath, old, _limits), ct);
        var stage = _recovery.StageRoot(id); var trash = _recovery.TrashRoot(id); var journalPath = _recovery.JournalPath(id);
        var log = new RestoreJournal(1, id, "Prepared", Directory.Exists(_recovery.ManagedRoot), autoRelative, oldFiles, target.Files, BackupDataStore.Hash(old), BackupDataStore.Hash(target.Data));
        var journalWritten = false; var committed = false; var warnings = new List<string>();
        try
        {
            await Task.Run(() =>
            {
                _files.CreateDirectory(ManagedBookFiles.SafePath(stage, "books/managed"));
                foreach (var file in target.Files)
                {
                    ct.ThrowIfCancellationRequested(); var destination = ManagedBookFiles.SafePath(stage, file.Path);
                    _files.WriteDurable(destination, File.ReadAllBytes(ManagedBookFiles.SafePath(target.Root, file.Path)));
                }
                if (!ManagedBookInventory.Same(ManagedBookInventory.Inspect(stage, target.Data, _limits), target.Files)) throw new IOException("目标staging复核失败。");
                _files.WriteJournal(journalPath, log); journalWritten = true;
                ct.ThrowIfCancellationRequested(); _files.Checkpoint("restore.before-isolate");
                if (log.HadManagedDirectory)
                {
                    var destination = ManagedBookFiles.SafePath(trash, "books/managed"); _files.CreateDirectory(Path.GetDirectoryName(destination)!);
                    _files.MoveDirectory(_recovery.ManagedRoot, destination);
                }
                _files.Checkpoint("restore.after-isolate");
                _files.WriteJournal(journalPath, log = log with { Stage = "Isolated" });
                ct.ThrowIfCancellationRequested(); _files.CreateDirectory(Path.GetDirectoryName(_recovery.ManagedRoot)!);
                _files.MoveDirectory(ManagedBookFiles.SafePath(stage, "books/managed"), _recovery.ManagedRoot);
                _files.Checkpoint("restore.after-install");
                _files.WriteJournal(journalPath, log = log with { Stage = "Installed" });
            }, ct);
            ct.ThrowIfCancellationRequested();
            if (!ManagedBookInventory.Same(await Task.Run(() => ManagedBookInventory.Inspect(_paths.DirectoryPath, target.Data, _limits)), target.Files)) throw new IOException("安装后的目标文件复核失败。");
            await _database.WriteAsync(lease, c =>
            {
                ct.ThrowIfCancellationRequested(); BackupDataStore.Replace(c, target.Data, id);
                _files.Checkpoint("restore.in-transaction"); return 0;
            });
            committed = true; lease.CompleteReplacement();
            _files.Checkpoint("restore.after-commit");
            foreach (var invalidator in _invalidators)
            {
                try { await invalidator.InvalidateAsync(CancellationToken.None); }
                catch (Exception) { warnings.Add("数据已恢复，旧页面清理未完成，请关闭应用后重开。"); _coordinator.RequireRecovery("RecoveryRequired：数据已恢复，需重启清理旧上下文。"); }
            }
            await Task.Run(() =>
            {
                _files.WriteJournal(journalPath, log with { Stage = "CleanupCommitted" });
                _recovery.Cleanup(id);
            });
        }
        catch (Exception ex)
        {
            if (committed)
            {
                if (ex is not RecoveryCleanupException) _coordinator.RequireRecovery("RecoveryRequired：数据已提交，请重新启动应用完成对账。");
                warnings.Add(ex is RecoveryCleanupException ? ex.Message : "数据已恢复，但收尾尚未完成，请关闭应用重启以完成对账。");
            }
            else
            {
                if (journalWritten)
                {
                    try { await _database.ReadAsync(lease, c => { _recovery.Recover(c, _recovery.Read(journalPath)); return 0; }); }
                    catch (RecoveryCleanupException) { /* Old dataset verified; cleanup-only log survives for retry. */ }
                    catch (Exception recoveryError)
                    { _coordinator.RequireRecovery("RecoveryRequired：需关闭应用重启，保留两套数据后完成对账。"); throw new InvalidOperationException("RecoveryRequired：恢复未提交且回退未完成，请保留 staging/trash/backups 后重启。", new AggregateException(ex, recoveryError)); }
                }
                else
                {
                    try { _files.DeleteDirectory(stage); } catch (IOException) { /* No live directory or SQL has changed. */ }
                }
                throw;
            }
        }
        return new(automaticPath, lease.Epoch, target.Data.Books.Count, target.Files.Count, warnings);
    }
}
