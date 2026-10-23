using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using Xunit;
namespace ReadJournal.UnitTest;
public sealed class ReaderSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReadJournalP5", Guid.NewGuid().ToString("N"));
    private DatabaseService? _db;
    private sealed class Faults : ReaderSessionCheckpoints
    { public string? Fail; public override void Checkpoint(string point) { if (Fail == point) { Fail = null; throw new IOException(point); } } }
    private async Task<(DatabaseService Db, ReaderSessionService Sessions, ReaderOpenIdentity Identity, ReaderPosition Start, Faults Fault)> Setup()
    {
        Directory.CreateDirectory(_root); var source = Path.Combine(_root, "book.txt"); await File.WriteAllTextAsync(source, "第一章 会话测试\n" + new string('文', 1000));
        var db = _db = new DatabaseService(new DataPaths(Path.Combine(_root, "data"))); var result = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); Assert.Equal(ImportState.Imported, result.State);
        var tuple = await db.ReadAsync(c => (c.Find<Book>(result.BookId), c.Find<EbookFile>(result.BookId), c.Find<EbookProgress>(result.BookId)));
        var key = ReaderContractValidator.ReadDocument(tuple.Item3.DocumentKeyJson); var start = ReaderContractValidator.ReadPosition(tuple.Item3.LastPositionJson);
        var identity = new ReaderOpenIdentity(tuple.Item1.Id, tuple.Item1.BookUid, key, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1);
        var docs = new ReaderDocumentService(db, db.Coordinator); await docs.OpenAsync(identity, default); var fault = new Faults();
        return (db, new(db, db.Coordinator, docs, new EbookProgressMaintenance(db, db.Coordinator), new ReaderClock(), fault), identity, start, fault);
    }
    [Theory][InlineData(12345, false)][InlineData(1, false)][InlineData(-100, true)][InlineData(86401234, false)]
    public async Task ExactMillisecondsOffsetCloseDateAndNoOpenRow(long milliseconds, bool anomaly)
    {
        var x = await Setup(); var opened = new DateTimeOffset(2026, 10, 22, 23, 59, 58, TimeSpan.FromHours(8)); var context = await x.Sessions.BeginAsync(x.Identity, x.Start, opened, default);
        Assert.Empty(await x.Sessions.GetForBookAsync(x.Identity.BookId, default)); Assert.True(await x.Db.ReadAsync(c => c.Find<EbookProgress>(x.Identity.BookId).HasStarted));
        var end = x.Start with { Utf16Offset = 100 }; var closed = opened.AddMilliseconds(milliseconds).ToOffset(TimeSpan.FromHours(-4));
        var command = new SessionCloseCommand(context, x.Start, end, opened, closed, false, ReaderExitReason.WindowClose);
        var result = await x.Sessions.CloseAsync(command, default); Assert.Equal(Math.Max(0, milliseconds), result.DurationMilliseconds);
        var row = Assert.Single(await x.Sessions.GetForBookAsync(x.Identity.BookId, default)); Assert.Equal(anomaly, row.ClockAnomaly); Assert.Equal(-240, row.ClosedOffsetMinutes); Assert.Equal(closed.ToString("yyyy-MM-dd"), row.CloseLocalDate);
        await using var maintenance = await x.Db.Coordinator.EnterMaintenanceAsync(MaintenanceKind.Backup, default);
    }
    [Theory][InlineData("session.before-write")][InlineData("session.in-transaction")][InlineData("session.after-commit")]
    public async Task RetryUsesSameFrozenCommandAndAtomicCommitEvenWhenAcknowledgementLost(string failure)
    {
        var x = await Setup(); var opened = DateTimeOffset.Now; var context = await x.Sessions.BeginAsync(x.Identity, x.Start, opened, default);
        var command = new SessionCloseCommand(context, x.Start, x.Start with { Utf16Offset = 50 }, opened, opened.AddMilliseconds(444), false, ReaderExitReason.Back);
        x.Fault.Fail = failure; await Assert.ThrowsAsync<IOException>(() => x.Sessions.CloseAsync(command, default));
        Assert.Equal(failure == "session.after-commit" ? 1 : 0, (await x.Sessions.GetForBookAsync(x.Identity.BookId, default)).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => x.Db.Coordinator.EnterMaintenanceAsync(MaintenanceKind.Backup, default));
        var replay = await x.Sessions.CloseAsync(command, default); Assert.Equal(failure == "session.after-commit", replay.WasAlreadyCommitted);
        Assert.Single(await x.Sessions.GetForBookAsync(x.Identity.BookId, default)); Assert.Equal(444, replay.DurationMilliseconds);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ContinueUsesDatabaseFactAndRetainsOrRestartsClock(bool committed)
    {
        var x = await Setup(); var opened = DateTimeOffset.Now; var context = await x.Sessions.BeginAsync(x.Identity, x.Start, opened, default);
        var command = new SessionCloseCommand(context, x.Start, x.Start with { Utf16Offset = 20 }, opened, opened.AddSeconds(1), false, ReaderExitReason.Menu);
        x.Fault.Fail = committed ? "session.after-commit" : "session.before-write"; await Assert.ThrowsAsync<IOException>(() => x.Sessions.CloseAsync(command, default));
        var now = opened.AddSeconds(5); var next = await x.Sessions.ContinueAfterCloseFailureAsync(context, command.EndPosition, now, default);
        Assert.Equal(committed, next.SessionId != context.SessionId);
        var result = await x.Sessions.CloseAsync(new(next, committed ? command.EndPosition : x.Start, x.Start with { Utf16Offset = 30 }, committed ? now : opened, now.AddSeconds(2), false, ReaderExitReason.Back), default);
        Assert.Equal(committed ? 2000 : 7000, result.DurationMilliseconds);
    }
    [Fact]
    public async Task DuplicateCloseNeverOverwritesLaterBackwardPositionAndStaleEpochReplayFails()
    {
        var x = await Setup(); var now = DateTimeOffset.Now;
        var first = await x.Sessions.BeginAsync(x.Identity, x.Start, now, default); var command = new SessionCloseCommand(first, x.Start, x.Start with { Utf16Offset = 300 }, now, now.AddSeconds(1), false, ReaderExitReason.Back);
        await x.Sessions.CloseAsync(command, default);
        var secondStart = command.EndPosition; var second = await x.Sessions.BeginAsync(x.Identity with { ReaderInstanceId = Guid.NewGuid() }, secondStart, now.AddSeconds(2), default);
        await x.Sessions.CloseAsync(new(second, secondStart, x.Start with { Utf16Offset = 10 }, now.AddSeconds(2), now.AddSeconds(3), false, ReaderExitReason.Back), default);
        await Task.WhenAll(x.Sessions.CloseAsync(command, default), x.Sessions.CloseAsync(command, default));
        Assert.Equal(10, ReaderContractValidator.ReadPosition((await x.Db.ReadAsync(c => c.Find<EbookProgress>(x.Identity.BookId))).LastPositionJson).Utf16Offset);
        await using (var lease = await x.Db.Coordinator.EnterMaintenanceAsync(MaintenanceKind.Restore, default)) lease.CompleteReplacement();
        await Assert.ThrowsAsync<InvalidOperationException>(() => x.Sessions.CloseAsync(command, default));
    }
    [Fact]
    public async Task ActionLeaseSerializesCloseAndWrongFrozenPayloadIsRejected()
    {
        var x = await Setup(); var now = DateTimeOffset.Now; var context = await x.Sessions.BeginAsync(x.Identity, x.Start, now, default);
        var action = await x.Sessions.EnterActiveActionAsync(context, default); var command = new SessionCloseCommand(context, x.Start, x.Start, now, now.AddSeconds(1), false, ReaderExitReason.Back);
        x.Fault.Fail = "session.before-write"; var close = x.Sessions.CloseAsync(command, default); Assert.False(close.IsCompleted); await action.DisposeAsync(); await Assert.ThrowsAsync<IOException>(() => close);
        await Assert.ThrowsAsync<ArgumentException>(() => x.Sessions.CloseAsync(command with { ClosedAt = now.AddSeconds(2) }, default)); await x.Sessions.CloseAsync(command, default);
    }
    public void Dispose() { _db?.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
