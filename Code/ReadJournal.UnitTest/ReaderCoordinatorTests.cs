using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using Xunit;
namespace ReadJournal.UnitTest;

public sealed class ReaderCoordinatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalP2", Guid.NewGuid().ToString("N"));
    internal static DocumentKey Key() => new(Guid.NewGuid(), new string('a', 64), new string('b', 64), 1);
    private static ReaderOpenIdentity Identity(DataEpoch epoch) => new(1, Guid.NewGuid(), Key(), epoch, Guid.NewGuid(), 1);
    [Fact]
    public async Task MaintenanceDrainsOperationsRejectsRegistrationAndInvalidatesWaitingOldEpoch()
    {
        using var coordinator = new ApplicationDataCoordinator(new DataPaths(_directory));
        var old = coordinator.CurrentEpoch;
        var operation = await coordinator.EnterOperationAsync(old, default);
        var maintenanceTask = coordinator.EnterMaintenanceAsync(MaintenanceKind.Restore, default);
        Assert.False(maintenanceTask.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RegisterReaderAsync(Identity(old), default));
        var waiting = coordinator.EnterOperationAsync(old, default); Assert.False(waiting.IsCompleted);
        await operation.DisposeAsync();
        await using (var maintenance = await maintenanceTask.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            var changed = maintenance.CompleteReplacement(); Assert.NotEqual(old, changed);
            Assert.Equal(changed, maintenance.CompleteReplacement());
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
        await using var fresh = await coordinator.EnterOperationAsync(coordinator.CurrentEpoch, default);
    }
    [Fact]
    public async Task ActiveReaderMakesMaintenanceFailImmediatelyAndReleaseAllowsIt()
    {
        using var coordinator = new ApplicationDataCoordinator(new DataPaths(_directory)); var epoch = coordinator.CurrentEpoch;
        var registration = await coordinator.RegisterReaderAsync(Identity(epoch), default);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.EnterMaintenanceAsync(MaintenanceKind.Reset, default).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Contains("退出阅读器", failure.Message);
        await registration.DisposeAsync(); await registration.DisposeAsync();
        await using var maintenance = await coordinator.EnterMaintenanceAsync(MaintenanceKind.Backup, default);
        Assert.Throws<InvalidOperationException>(() => maintenance.CompleteReplacement()); Assert.Equal(epoch, coordinator.CurrentEpoch);
    }
    [Fact]
    public async Task CancelledMaintenanceAndOperationsReleaseTheirGates()
    {
        using var coordinator = new ApplicationDataCoordinator(new DataPaths(_directory)); var epoch = coordinator.CurrentEpoch;
        var active = await coordinator.EnterOperationAsync(epoch, default);
        using var cancel = new CancellationTokenSource();
        var blocked = coordinator.EnterMaintenanceAsync(MaintenanceKind.Reset, cancel.Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked);
        await active.DisposeAsync();
        await using (var maintenance = await coordinator.EnterMaintenanceAsync(MaintenanceKind.Backup, default))
        {
            using var c = new CancellationTokenSource(); var waiting = coordinator.EnterOperationAsync(epoch, c.Token); c.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        await using var resumed = await coordinator.EnterOperationAsync(epoch, default);
    }
    [Fact]
    public async Task LeasedDatabaseCallsDoNotReenterAndRejectDisposedOrForeignLeases()
    {
        var paths = new DataPaths(_directory); using var coordinator = new ApplicationDataCoordinator(paths);
        using var db = new DatabaseService(paths, coordinator, new DatabaseMigrationV2()); await db.InitializeAsync();
        var old = coordinator.CurrentEpoch;
        var lease = await coordinator.EnterOperationAsync(old, default);
        Assert.Equal(2, await db.ReadAsync(lease, c => c.ExecuteScalar<int>("PRAGMA user_version")).WaitAsync(TimeSpan.FromSeconds(2)));
        await lease.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.WriteAsync(lease, c => c.Execute("INSERT INTO AppMetadata VALUES('should-not-write','')")));
        await using (var maintenance = await coordinator.EnterMaintenanceAsync(MaintenanceKind.Reset, default))
        {
            await db.WriteAsync(maintenance, c => c.Execute("INSERT INTO AppMetadata VALUES('committed','yes')")).WaitAsync(TimeSpan.FromSeconds(2));
            maintenance.CompleteReplacement();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.EnterOperationAsync(old, default));
        Assert.Equal("yes", await db.ReadAsync(c => c.ExecuteScalar<string>("SELECT Value FROM AppMetadata WHERE Key='committed'")));
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
