using SQLite;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;
// One shared connection. Data leases precede the SQL gate; no callbacks into async storage inside SQL.
public class DatabaseService : IDatabaseService, IOperationRecoveryService, IDisposable
{
    private readonly DataPaths _paths;
    private readonly ApplicationDataCoordinator _coordinator;
    private readonly DatabaseMigrationV2 _migration;
    private readonly bool _ownsCoordinator;
    private readonly RestoreRecoveryService _recovery;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private SQLiteConnection? _connection;
    public bool IsInitialized { get; private set; }
    public ApplicationDataCoordinator Coordinator => _coordinator;
    public DataPaths Paths => _paths;
    public DatabaseService(DataPaths paths) : this(paths, new ApplicationDataCoordinator(paths), new DatabaseMigrationV2()) { _ownsCoordinator = true; }
    public DatabaseService(DataPaths paths, ApplicationDataCoordinator coordinator, DatabaseMigrationV2 migration)
        : this(paths, coordinator, migration, BackupRuntime.CreateRecovery(paths)) { }
    public DatabaseService(DataPaths paths, ApplicationDataCoordinator coordinator, DatabaseMigrationV2 migration, RestoreRecoveryService recovery)
    { _paths = paths; _coordinator = coordinator; _migration = migration; _recovery = recovery; }
    public Task RecoverBeforeBusinessReadyAsync(CancellationToken ct) => InitializeAsync().WaitAsync(ct);
    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        // Concurrent startup callers coalesce before requesting exclusive maintenance.
        // A caller queued during initialization must not later request migration with an active Reader.
        await _initializationGate.WaitAsync();
        try
        {
            if (IsInitialized) return;
            // Startup migration is exclusive and precedes business readiness.
            await using var maintenance = await _coordinator.EnterMaintenanceAsync(MaintenanceKind.Migration, CancellationToken.None);
            await _gate.WaitAsync();
            try
            {
                if (IsInitialized) return;
                var migrated = await Task.Run(() =>
                {
                    var connection = new SQLiteConnection(_paths.DatabasePath);
                    try
                    {
                        connection.Execute("PRAGMA foreign_keys=ON");
                        connection.ExecuteScalar<int>("PRAGMA busy_timeout=5000");
                        if (connection.ExecuteScalar<int>("PRAGMA user_version") > DatabaseMigrationV2.CurrentVersion) throw new InvalidOperationException("数据库版本高于当前应用支持的版本。");
                        try { _recovery.RecoverPending(connection); }
                        catch (RecoveryCleanupException ex) { System.Diagnostics.Trace.WriteLine(ex.Message); }
                        var changed = _migration.Apply(connection);
                        _recovery.ReconcileBookFiles(connection);
                        _connection = connection; return changed;
                    }
                    catch { connection.Dispose(); throw; }
                });
                if (migrated) maintenance.CompleteReplacement();
                IsInitialized = true;
            }
            finally { _gate.Release(); }
        }
        finally { _initializationGate.Release(); }
    }
    public Task<T> ReadAsync<T>(Func<SQLiteConnection, T> read) => ExecuteAsync(read, false);
    public Task<T> WriteAsync<T>(Func<SQLiteConnection, T> write) => ExecuteAsync(write, true);
    private async Task<T> ExecuteAsync<T>(Func<SQLiteConnection, T> action, bool transaction)
    {
        await InitializeAsync();
        await using var lease = await _coordinator.EnterOperationAsync(_coordinator.CurrentEpoch, CancellationToken.None);
        return await ExecuteLeasedAsync(action, transaction, () => _coordinator.ValidateLease(lease));
    }
    public Task<T> ReadAsync<T>(IDataOperationLease lease, Func<SQLiteConnection, T> read) => ExecuteLeasedAsync(read, false, () => _coordinator.ValidateLease(lease));
    public Task<T> WriteAsync<T>(IDataOperationLease lease, Func<SQLiteConnection, T> write) => ExecuteLeasedAsync(write, true, () => _coordinator.ValidateLease(lease));
    public Task<T> ReadAsync<T>(IDataMaintenanceLease lease, Func<SQLiteConnection, T> read) => ExecuteLeasedAsync(read, false, () => _coordinator.ValidateLease(lease));
    public Task<T> WriteAsync<T>(IDataMaintenanceLease lease, Func<SQLiteConnection, T> write) => ExecuteLeasedAsync(write, true, () => _coordinator.ValidateLease(lease));
    private async Task<T> ExecuteLeasedAsync<T>(Func<SQLiteConnection, T> action, bool transaction, Action validate)
    {
        validate();
        if (!IsInitialized) throw new InvalidOperationException("请先初始化数据库，再取得业务租约。");
        await _gate.WaitAsync();
        try
        {
            validate();
            return await Task.Run(() =>
            {
                if (!transaction) return action(_connection!);
                T result = default!; _connection!.RunInTransaction(() => result = action(_connection)); return result;
            });
        }
        finally { _gate.Release(); }
    }
    public void Dispose()
    {
        _gate.Wait();
        try { _connection?.Dispose(); _connection = null; IsInitialized = false; }
        finally { _gate.Release(); }
        if (_ownsCoordinator) _coordinator.Dispose();
    }
}
