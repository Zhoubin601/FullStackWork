using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using Xunit;
namespace ReadJournal.UnitTest;
public sealed class ReaderLocalWorkflowTests
{
    private sealed class Faults : ReaderQuoteCheckpoints
    { public string? Point; public override void Checkpoint(string point) { if (Point == point) { Point = null; throw new IOException(point); } } }
    private sealed class Fixture : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ReadJournalP6", Guid.NewGuid().ToString("N"));
        public DatabaseService Db { get; private set; } = null!;
        public ReaderDocumentService Documents { get; private set; } = null!;
        public ReaderSessionService Sessions { get; private set; } = null!;
        public ReaderQuoteService Quotes { get; private set; } = null!;
        public Faults Fault { get; } = new();
        public ReaderContext Context { get; private set; } = null!;
        public ReaderPosition Start { get; private set; } = null!;
        public DateTimeOffset Opened { get; private set; }
        public int Id => Context.Identity.BookId;
        public bool Active { get; private set; }
        public static async Task<Fixture> Create()
        {
            var x = new Fixture(); Directory.CreateDirectory(x.Root); var source = Path.Combine(x.Root, "book.txt"); await File.WriteAllTextAsync(source, "相同句子。相同句子。\n" + new string('字', 4000));
            x.Db = new(new DataPaths(Path.Combine(x.Root, "data"))); var imported = await EbookRuntime.CreateLibrary(x.Db).ImportAsync(new(source), null, default); Assert.Equal(ImportState.Imported, imported.State);
            var tuple = await x.Db.ReadAsync(c => (c.Find<Book>(imported.BookId), c.Find<EbookProgress>(imported.BookId)));
            x.Start = ReaderContractValidator.ReadPosition(tuple.Item2.LastPositionJson);
            x.Documents = new(x.Db, x.Db.Coordinator); var identity = new ReaderOpenIdentity(tuple.Item1.Id, tuple.Item1.BookUid, x.Start.Document, x.Db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1); await x.Documents.OpenAsync(identity, default);
            x.Sessions = new(x.Db, x.Db.Coordinator, x.Documents, new EbookProgressMaintenance(x.Db, x.Db.Coordinator), new ReaderClock(), new ReaderSessionCheckpoints());
            x.Quotes = new(x.Db, x.Db.Coordinator, x.Documents, x.Sessions, new ReadingService(), x.Fault);
            x.Opened = DateTimeOffset.Now; x.Context = await x.Sessions.BeginAsync(identity, x.Start, x.Opened, default); x.Active = true; return x;
        }
        public DocumentRange Range(int a = 0, int b = 5) => new(Start with { Utf16Offset = a }, Start with { Utf16Offset = b });
        public async Task Close(int milliseconds = 350)
        { if (!Active) return; await Sessions.CloseAsync(new(Context, Start, Start with { Utf16Offset = 50 }, Opened, Opened.AddMilliseconds(milliseconds), false, ReaderExitReason.Back), default); Active = false; }
        public async ValueTask DisposeAsync() { await Close(); Db.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    [Theory][InlineData("quote.after-insert")][InlineData("quote.after-origin")]
    public async Task QuoteAndOriginRollbackTogetherAndRetryCreatesOnePair(string point)
    {
        await using var x = await Fixture.Create(); x.Fault.Point = point;
        await Assert.ThrowsAsync<IOException>(() => x.Quotes.SaveSelectionAsync(x.Context, x.Range(), "", default));
        Assert.Equal(0, await x.Db.ReadAsync(c => c.Table<Quote>().Count())); Assert.Equal(0, await x.Db.ReadAsync(c => c.Table<QuoteOrigin>().Count()));
        var id = await x.Quotes.SaveSelectionAsync(x.Context, x.Range(), "感想", default);
        Assert.Equal("相同句子。", (await x.Db.ReadAsync(c => c.Find<Quote>(id))).Text); Assert.Null((await x.Db.ReadAsync(c => c.Find<Quote>(id))).Page);
    }
    [Fact]
    public async Task DedupeIsByPositionAndEditsPreserveOriginalTextAndExactReturn()
    {
        await using var x = await Fixture.Create(); var first = await x.Quotes.SaveSelectionAsync(x.Context, x.Range(), "初始", default);
        Assert.Equal(first, await x.Quotes.SaveSelectionAsync(x.Context, x.Range(), "重复", default));
        var second = await x.Quotes.SaveSelectionAsync(x.Context, x.Range(5, 10), "不同位置", default); Assert.NotEqual(first, second);
        var storage = new QuoteStorage(x.Db, new ReadingService()); var quote = (await storage.GetAsync(first))!; quote.Text = "用户修改的摘要"; quote.Thoughts = "新的感想"; await storage.SaveAsync(quote); await storage.ToggleFavoriteAsync(first);
        var origin = await x.Db.ReadAsync(c => c.Find<QuoteOrigin>(first)); Assert.Equal("相同句子。", origin.OriginalText); Assert.Equal(x.Range().Start, (await x.Quotes.ResolveQuoteAsync(first, default)).Position);
        Assert.Equal(2, (await storage.SearchAsync("", false)).Count); Assert.Single(await storage.SearchAsync("用户", true));
        var manual = new Book { Title = "另一书", TotalPages = 100 }; await new BookStorage(x.Db, new ReadingService()).SaveAsync(manual); quote.BookId = manual.Id;
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(quote)); Assert.Equal(x.Id, (await storage.GetAsync(first))!.BookId);
    }
    [Fact]
    public async Task VersionMismatchAndUnknownSourceReturnExplicitFailureWithoutGuessing()
    {
        await using var x = await Fixture.Create(); var id = await x.Quotes.SaveSelectionAsync(x.Context, x.Range(), "", default); var file = await x.Db.ReadAsync(c => c.Find<EbookFile>(x.Id)); var original = file.CanonicalSha256;
        try { file.CanonicalSha256 = new string('e', 64); await x.Db.WriteAsync(c => c.Update(file)); var result = await x.Quotes.ResolveQuoteAsync(id, default); Assert.False(result.IsExact); Assert.Contains("版本", result.Reason); }
        finally { file.CanonicalSha256 = original; await x.Db.WriteAsync(c => c.Update(file)); }
        Assert.False((await x.Quotes.ResolveQuoteAsync(99999, default)).IsExact);
    }
    [Fact]
    public async Task UnifiedHistoryStablePaginationAndSessionDeletePreserveProgressCompletion()
    {
        await using var x = await Fixture.Create(); await x.Sessions.MarkCompletedAsync(x.Context, default); await x.Close();
        var rules = new ReadingService(); var books = new BookStorage(x.Db, rules); var manual = new Book { Title = "手动书", TotalPages = 100 }; await books.SaveAsync(manual);
        var records = new ReadingRecordStorage(x.Db, rules); for (var i = 0; i < 12; i++) await records.SaveAsync(new() { BookId = manual.Id, ReadingDate = DateTime.Today, StartPage = 1, EndPage = 3, Minutes = 1 });
        var history = new ReadingHistoryService(x.Db, x.Db.Coordinator, rules); var first = await history.GetAsync(null, 1, 5, default); var repeated = await history.GetAsync(null, 1, 5, default); var second = await history.GetAsync(null, 2, 5, default);
        Assert.Equal(13, first.TotalCount); Assert.Equal(first.Items.Select(h => h.Key), repeated.Items.Select(h => h.Key)); Assert.Empty(first.Items.Select(h => h.Key).Intersect(second.Items.Select(h => h.Key)));
        var all = (await history.GetAsync(null, 1, 200, default)).Items; Assert.Equal(13, all.Select(h => h.Key).Distinct().Count()); Assert.Equal("12", all.First(h => h.CanEdit).Key.SourceId);
        var auto = Assert.Single(all.Where(h => !h.CanEdit)); Assert.Null(auto.RealPages); var before = JsonSerializer.Serialize(await x.Db.ReadAsync(c => c.Find<EbookProgress>(x.Id)));
        await history.DeleteAsync(auto.Key, x.Db.Coordinator.CurrentEpoch, default); Assert.Equal(before, JsonSerializer.Serialize(await x.Db.ReadAsync(c => c.Find<EbookProgress>(x.Id)))); Assert.Equal("已完成", (await books.GetProgressAsync(x.Id))!.State);
    }
    [Fact]
    public async Task MillisecondsAccumulateWithoutInventedPagesAndSevenDayDurationBarIsVisible()
    {
        await using var x = await Fixture.Create(); await x.Close(350); var now = x.Opened.AddSeconds(2); var start = x.Start with { Utf16Offset = 50 };
        var context = await x.Sessions.BeginAsync(x.Context.Identity with { ReaderInstanceId = Guid.NewGuid() }, start, now, default);
        await x.Sessions.CloseAsync(new(context, start, start, now, now.AddMilliseconds(450), false, ReaderExitReason.Back), default);
        var stats = await new StatisticsService(x.Db, new ReadingService()).GetAsync(); Assert.Equal(800, stats.DurationMilliseconds); Assert.Equal(0, stats.TotalMinutes); Assert.Equal(0, stats.TotalPages); Assert.Equal(2, stats.SessionCount);
        var day = Assert.Single(stats.LastSevenDays.Where(d => d.DurationMilliseconds > 0)); Assert.Equal(800, day.DurationMilliseconds); Assert.Equal(100, day.DurationBarValue); Assert.Equal(0, day.BarValue); Assert.Contains("0.013", day.Label);
    }
    [Fact]
    public async Task CompletedEbookCanReadBackwardAndMultiplePlansMakeResetEntirelyReject()
    {
        await using var x = await Fixture.Create(); var books = new BookStorage(x.Db, new ReadingService()); var plans = new ReadingPlanStorage(x.Db, new ReadingService());
        Assert.NotEqual("已完成", (await books.GetProgressAsync(x.Id))!.State); await x.Sessions.MarkCompletedAsync(x.Context, default); await x.Close();
        var now = x.Opened.AddSeconds(3); var back = await x.Sessions.BeginAsync(x.Context.Identity with { ReaderInstanceId = Guid.NewGuid() }, x.Start with { Utf16Offset = 50 }, now, default);
        await x.Sessions.CloseAsync(new(back, x.Start with { Utf16Offset = 50 }, x.Start with { Utf16Offset = 10 }, now, now.AddSeconds(1), false, ReaderExitReason.Back), default);
        Assert.Equal("已完成", (await books.GetProgressAsync(x.Id))!.State);
        await Assert.ThrowsAsync<ArgumentException>(() => plans.SaveAsync(new() { BookId = x.Id, StartDate = DateTime.Today, TargetDate = DateTime.Today.AddDays(1) }));
        // A valid restored legacy library may contain multiple historical completed plans.
        await x.Db.WriteAsync(c => { c.Insert(new ReadingPlan { BookId = x.Id, StartDate = DateTime.Today.AddDays(-5), TargetDate = DateTime.Today.AddDays(-2) }); c.Insert(new ReadingPlan { BookId = x.Id, StartDate = DateTime.Today.AddDays(-4), TargetDate = DateTime.Today.AddDays(-1) }); return 0; });
        var before = await x.Db.ReadAsync(c => BackupDataStore.Hash(BackupDataStore.Snapshot(c))); var epoch = x.Db.Coordinator.CurrentEpoch;
        await Assert.ThrowsAsync<ArgumentException>(() => x.Sessions.ResetProgressAsync(x.Id, x.Context.Identity.BookUid, epoch, default));
        Assert.Equal(before, await x.Db.ReadAsync(c => BackupDataStore.Hash(BackupDataStore.Snapshot(c)))); Assert.Equal(epoch, x.Db.Coordinator.CurrentEpoch);
    }
    [Fact]
    public async Task M1ActualImportQuoteCloseReopenBackupDeleteRestorePreservesAllFieldsAndFiles()
    {
        await using var x = await Fixture.Create(); var quoteId = await x.Quotes.SaveSelectionAsync(x.Context, x.Range(), "M1感想", default); await x.Close();
        await x.Documents.ReleaseAllAsync(); var reopened = new ReaderDocumentService(x.Db, x.Db.Coordinator); var progress = await x.Db.ReadAsync(c => c.Find<EbookProgress>(x.Id)); var last = ReaderContractValidator.ReadPosition(progress.LastPositionJson); await reopened.OpenAsync(x.Context.Identity with { ReaderInstanceId = Guid.NewGuid() }, default); Assert.Equal(50, last.Utf16Offset);
        var archive = Path.Combine(x.Root, "m1.zip"); var backup = BackupRuntime.CreateService(x.Db, new ReadingService(), x.Db.Paths); var before = await x.Db.ReadAsync(BackupDataStore.Snapshot); var inventory = ManagedBookInventory.Inspect(x.Db.Paths.DirectoryPath, before, new()); await backup.ExportAsync(archive, default);
        await EbookRuntime.CreateLibrary(x.Db).DeleteBookAsync(x.Id, x.Context.Identity.BookUid, x.Db.Coordinator.CurrentEpoch, default); Assert.Empty((await x.Db.ReadAsync(BackupDataStore.Snapshot)).Books);
        await backup.RestoreAsync(archive, default); var after = await x.Db.ReadAsync(BackupDataStore.Snapshot); Assert.Equal(BackupDataStore.Hash(before), BackupDataStore.Hash(after)); Assert.True(ManagedBookInventory.Same(inventory, ManagedBookInventory.Inspect(x.Db.Paths.DirectoryPath, after, new())));
        Assert.True((await x.Quotes.ResolveQuoteAsync(quoteId, default)).IsExact); Assert.Equal("ok", await x.Db.ReadAsync(c => c.ExecuteScalar<string>("pragma integrity_check")));
    }
}
