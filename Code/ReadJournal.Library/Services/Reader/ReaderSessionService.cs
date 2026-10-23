using System.Globalization;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using SQLite;
namespace ReadJournal.Library.Services.Reader;

public class ReaderClock { public virtual DateTimeOffset Now => DateTimeOffset.Now; }
public class ReaderSessionCheckpoints { public virtual void Checkpoint(string point) { } }

public sealed class ReaderSessionService(IDatabaseService database, IApplicationDataCoordinator coordinator,
    IReaderDocumentService documents, IEbookProgressMaintenance reset, ReaderClock clock, ReaderSessionCheckpoints checkpoints) : IReaderSessionService
{
    private enum Phase { Active, Closing, CloseFailed, Closed }
    private sealed class Entry(ReaderContext context, ReaderPosition start, DateTimeOffset opened, IReaderRegistration registration)
    {
        public ReaderContext Context { get; } = context;
        public ReaderPosition Start { get; } = start;
        public DateTimeOffset Opened { get; } = opened;
        public IReaderRegistration? Registration { get; set; } = registration;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Phase State { get; set; } = Phase.Active;
        public SessionCloseCommand? Frozen { get; set; }
    }
    private readonly object _sync = new();
    private readonly SemaphoreSlim _beginGate = new(1, 1);
    private readonly Dictionary<Guid, Entry> _entries = new();
    private static void Time(DateTimeOffset time)
    { if (time.UtcTicks <= 0 || Math.Abs(time.Offset.TotalMinutes) > 840) throw new ArgumentException("阅读时间无效。"); }
    private static EbookFile ValidateBook(SQLiteConnection c, ReaderOpenIdentity identity)
    {
        ReaderContractValidator.Validate(identity.Document);
        var book = c.Find<Book>(identity.BookId); var file = c.Find<EbookFile>(identity.BookId);
        if (book?.BookUid != identity.BookUid || book.Kind != BookKind.Ebook || file is null ||
            new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, file.ParserVersion) != identity.Document)
            throw new InvalidOperationException("StaleContext：书籍或正文身份已改变，请重新打开。");
        return file;
    }
    private Entry? Find(ReaderContext context)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(context.SessionId, out var entry)) return null;
            if (entry.Context != context) throw new InvalidOperationException("StaleContext：会话上下文不一致。");
            return entry;
        }
    }
    public async Task<ReaderContext> BeginAsync(ReaderOpenIdentity identity, ReaderPosition start, DateTimeOffset openedAt, CancellationToken ct)
    {
        Time(openedAt); ReaderContractValidator.Validate(start);
        if (start.Document != identity.Document) throw new ArgumentException("会话起点文档不一致。");
        await database.InitializeAsync(); await _beginGate.WaitAsync(ct);
        try
        {
            lock (_sync)
            {
                var existing = _entries.Values.FirstOrDefault(e => e.Context.Identity.ReaderInstanceId == identity.ReaderInstanceId);
                if (existing is not null)
                {
                    if (existing.Context.Identity == identity && existing.Start == start && existing.Opened == openedAt && existing.State == Phase.Active) return existing.Context;
                    throw new InvalidOperationException("StaleContext：阅读器实例不能复用已结束或不同的会话。");
                }
                if (_entries.Values.Any(e => e.Context.Identity.BookUid == identity.BookUid && e.State != Phase.Closed)) throw new InvalidOperationException("该书已有未结束会话，请先完成离开。");
            }
            await using var lease = await coordinator.EnterOperationAsync(identity.Epoch, ct);
            await database.ReadAsync(lease, c => { var f = ValidateBook(c, identity); if (f.Status != (int)EbookFileStatus.Ready) throw new InvalidOperationException("FileMissing：原文尚未就绪。"); return 0; });
            var startPercent = await documents.GetPercentAsync(start, ct);
            var registration = await coordinator.RegisterReaderAsync(identity, ct);
            try
            {
                await database.WriteAsync(lease, c =>
                {
                    ct.ThrowIfCancellationRequested(); ValidateBook(c, identity); checkpoints.Checkpoint("session.begin-in-transaction");
                    var row = c.Find<EbookProgress>(identity.BookId);
                    if (row is null) c.Insert(new EbookProgress { BookId = identity.BookId, DocumentKeyJson = ReaderContractValidator.WriteDocument(identity.Document), LastPositionJson = ReaderContractValidator.WritePosition(start),
                        HasStarted = true, CurrentPercent = startPercent, LastOpenedUtcTicks = openedAt.UtcTicks, LastSavedUtcTicks = openedAt.UtcTicks });
                    else { row.HasStarted = true; row.LastOpenedUtcTicks = openedAt.UtcTicks; c.Update(row); }
                    return 0;
                });
                var entry = new Entry(new(identity, Guid.NewGuid()), start, openedAt, registration);
                lock (_sync) _entries.Add(entry.Context.SessionId, entry);
                return entry.Context;
            }
            catch { await registration.DisposeAsync(); throw; }
        }
        finally { _beginGate.Release(); }
    }
    private static SessionCommitResult Result(EbookReadingSession row, bool replay) => new(row.SessionId, replay, row.DurationMilliseconds, ReaderContractValidator.ReadPosition(row.EndPositionJson));
    private async Task Complete(Entry? entry)
    {
        if (entry is null) return;
        entry.State = Phase.Closed;
        if (entry.Registration is not null) { await entry.Registration.DisposeAsync(); entry.Registration = null; }
        lock (_sync)
        {
            foreach (var old in _entries.Where(p => p.Value.State == Phase.Closed && p.Key != entry.Context.SessionId).Take(Math.Max(0, _entries.Count - 32)).Select(p => p.Key).ToArray()) _entries.Remove(old);
        }
    }
    public async Task<SessionCommitResult> CloseAsync(SessionCloseCommand command, CancellationToken ct)
    {
        var context = command.Context; var entry = Find(context); var gate = entry?.Gate ?? new SemaphoreSlim(1, 1);
        await gate.WaitAsync(ct);
        try
        {
            await using var lease = await coordinator.EnterOperationAsync(context.Identity.Epoch, ct);
            var prior = await database.ReadAsync(lease, c => { ValidateBook(c, context.Identity); return c.Find<EbookReadingSession>(context.SessionId); });
            if (prior is not null)
            {
                if (prior.BookUid != context.Identity.BookUid || ReaderContractValidator.ReadDocument(prior.DocumentKeyJson) != context.Identity.Document) throw new InvalidOperationException("StaleContext：已提交会话身份不一致。");
                await Complete(entry); return Result(prior, true);
            }
            if (entry is null || entry.State == Phase.Closed) throw new InvalidOperationException("StaleContext：会话不活动或未知。");
            entry.State = Phase.Closing;
            if (command.StartPosition != entry.Start || command.OpenedAt != entry.Opened || command.EndPosition.Document != context.Identity.Document || !Enum.IsDefined(command.Reason)) throw new ArgumentException("关闭命令与实际会话起点不一致。");
            if (entry.Frozen is not null && entry.Frozen != command) throw new ArgumentException("关闭重试必须使用原冻结命令。");
            Time(command.ClosedAt); Time(command.OpenedAt); entry.Frozen ??= command;
            var startPercent = await documents.GetPercentAsync(entry.Start, ct); var endPercent = await documents.GetPercentAsync(command.EndPosition, ct);
            var ticks = command.ClosedAt.UtcTicks - entry.Opened.UtcTicks;
            var row = new EbookReadingSession { SessionId = context.SessionId, BookId = context.Identity.BookId, BookUid = context.Identity.BookUid,
                DocumentKeyJson = ReaderContractValidator.WriteDocument(context.Identity.Document), StartPositionJson = ReaderContractValidator.WritePosition(entry.Start), EndPositionJson = ReaderContractValidator.WritePosition(command.EndPosition),
                StartPercent = startPercent, EndPercent = endPercent, OpenedUtcTicks = entry.Opened.UtcTicks, ClosedUtcTicks = command.ClosedAt.UtcTicks,
                OpenedOffsetMinutes = (int)entry.Opened.Offset.TotalMinutes, ClosedOffsetMinutes = (int)command.ClosedAt.Offset.TotalMinutes,
                DurationMilliseconds = Math.Max(0, ticks / TimeSpan.TicksPerMillisecond), CloseLocalDate = command.ClosedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ClockAnomaly = ticks < 0 };
            checkpoints.Checkpoint("session.before-write");
            await database.WriteAsync(lease, c =>
            {
                ValidateBook(c, context.Identity); ct.ThrowIfCancellationRequested();
                var existing = c.Find<EbookReadingSession>(context.SessionId); if (existing is not null) return 0;
                var progress = c.Find<EbookProgress>(row.BookId) ?? throw new InvalidOperationException("正文进度已被重置，请重新打开。");
                c.Insert(row); progress.LastPositionJson = row.EndPositionJson; progress.CurrentPercent = endPercent; progress.HasStarted = true; progress.LastSavedUtcTicks = row.ClosedUtcTicks;
                if (command.MarkCompleted && progress.CompletedUtcTicks is null) { progress.CompletedUtcTicks = row.ClosedUtcTicks; progress.CompletedOffsetMinutes = row.ClosedOffsetMinutes; }
                c.Update(progress); checkpoints.Checkpoint("session.in-transaction"); return 0;
            });
            checkpoints.Checkpoint("session.after-commit"); await Complete(entry); return Result(row, false);
        }
        catch { if (entry is not null && entry.State != Phase.Closed) entry.State = Phase.CloseFailed; throw; }
        finally { gate.Release(); }
    }
    public async Task<SessionCommitResult?> FindCommittedAsync(Guid sessionId, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); var row = await database.ReadAsync(c => c.Find<EbookReadingSession>(sessionId)); return row is null ? null : Result(row, true); }
    public async Task<IReadOnlyList<EbookReadingSession>> GetForBookAsync(int bookId, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return await database.ReadAsync(c => (IReadOnlyList<EbookReadingSession>)c.Table<EbookReadingSession>().Where(s => s.BookId == bookId).OrderByDescending(s => s.ClosedUtcTicks).ToList()); }
    public async Task<IReaderActionLease> EnterActiveActionAsync(ReaderContext context, CancellationToken ct)
    {
        var entry = Find(context) ?? throw new InvalidOperationException("会话不活动。"); await entry.Gate.WaitAsync(ct);
        try
        {
            if (entry.State != Phase.Active) throw new InvalidOperationException("阅读器正在关闭或关闭失败，请先重试/继续阅读。");
            await using var lease = await coordinator.EnterOperationAsync(context.Identity.Epoch, ct); await database.ReadAsync(lease, c => { ValidateBook(c, context.Identity); return 0; });
            return new ActionLease(entry.Gate);
        }
        catch { entry.Gate.Release(); throw; }
    }
    private sealed class ActionLease(SemaphoreSlim gate) : IReaderActionLease
    { private bool _released; public ValueTask DisposeAsync() { if (!_released) { _released = true; gate.Release(); } return ValueTask.CompletedTask; } }
    public async Task<ReaderContext> ContinueAfterCloseFailureAsync(ReaderContext context, ReaderPosition position, DateTimeOffset now, CancellationToken ct)
    {
        var entry = Find(context) ?? throw new InvalidOperationException("会话不活动。"); var committed = false;
        await entry.Gate.WaitAsync(ct);
        try
        {
            await using var lease = await coordinator.EnterOperationAsync(context.Identity.Epoch, ct);
            if (entry.State != Phase.CloseFailed) throw new InvalidOperationException("只有关闭失败的会话可以继续阅读。");
            committed = await database.ReadAsync(lease, c => { ValidateBook(c, context.Identity); return c.Find<EbookReadingSession>(context.SessionId) is not null; });
            if (committed) await Complete(entry);
            else { entry.State = Phase.Active; entry.Frozen = null; return context; }
        }
        finally { entry.Gate.Release(); }
        return await BeginAsync(context.Identity with { ReaderInstanceId = Guid.NewGuid() }, position, now, ct);
    }
    public async Task MarkCompletedAsync(ReaderContext context, CancellationToken ct)
    {
        await using var action = await EnterActiveActionAsync(context, ct); await using var lease = await coordinator.EnterOperationAsync(context.Identity.Epoch, ct); var now = clock.Now; Time(now);
        await database.WriteAsync(lease, c => { ValidateBook(c, context.Identity); var p = c.Find<EbookProgress>(context.Identity.BookId)!; if (p.CompletedUtcTicks is null) { p.CompletedUtcTicks = now.UtcTicks; p.CompletedOffsetMinutes = (int)now.Offset.TotalMinutes; c.Update(p); } return 0; });
    }
    public Task ResetProgressAsync(int bookId, Guid bookUid, DataEpoch expectedEpoch, CancellationToken ct) => reset.ResetAsync(bookId, bookUid, expectedEpoch, ct);
}
