using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using Xunit;
using Moq;
using ReadJournal.Library.ViewModels;
namespace ReadJournal.UnitTest;

public sealed class ReaderStateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalP2", Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData(false, false, 0, "未开始")]
    [InlineData(true, false, 0, "在读")]
    [InlineData(true, false, 100, "在读")]
    [InlineData(true, true, 10, "已完成")]
    public void EbookStateDoesNotUseZeroPagesOrRewindForCompletion(bool started, bool completed, double percent, string state)
    {
        var book = new Book { Kind = BookKind.Ebook, Title = "合成电子书", TotalPages = 0 };
        var value = new ReadingStateCalculator().Calculate(book, [], new(started, completed ? DateTimeOffset.Now : null, percent, 1999, 2));
        Assert.Equal(state, value.State); Assert.Equal(percent, value.Percent); Assert.DoesNotContain("0/0", value.ProgressText);
        var plan = new PlanItem { Progress = value, Plan = new ReadingPlan { StartDate = DateTime.Today, TargetDate = DateTime.Today.AddDays(1) } };
        Assert.Equal(completed ? "已完成" : "进行中", plan.State); Assert.DoesNotContain("页", plan.Suggestion);
        Assert.Equal(completed ? 0 : (100 - percent) / 2, plan.SuggestedDailyPercent);
        Assert.Equal(1999, value.DurationMilliseconds);
    }
    [Fact]
    public async Task EbookValidationAndImmutableIdentityProtectBothStorageAndSql()
    {
        using var db = new DatabaseService(new DataPaths(_directory)); var reading = new ReadingService(); var storage = new BookStorage(db, reading);
        var book = new Book { Kind = BookKind.Ebook, Title = "电子书", TotalPages = 0 }; await storage.SaveAsync(book);
        Assert.Throws<ArgumentException>(() => reading.Calculate(book, []));
        await Assert.ThrowsAsync<ArgumentException>(() => new ReadingRecordStorage(db, reading).SaveAsync(new() { BookId = book.Id, StartPage = 1, EndPage = 1, Minutes = 1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(new() { Title = "手动零页", TotalPages = 0 }));
        book.Kind = BookKind.Manual; book.TotalPages = 100;
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(book));
        var actual = (await storage.GetAsync(book.Id))!; Assert.Equal(BookKind.Ebook, actual.Kind);
        actual.BookUid = Guid.NewGuid(); await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(actual));
    }
    [Fact]
    public async Task QueriesPlansAndResetUseSameCompletionStateAndKeepHistory()
    {
        var paths = new DataPaths(_directory); using var db = new DatabaseService(paths);
        var reading = new ReadingService(); var books = new BookStorage(db, reading); var plans = new ReadingPlanStorage(db, reading);
        var book = new Book { Kind = BookKind.Ebook, Title = "完成后回看", TotalPages = 0 }; await books.SaveAsync(book);
        var key = ReaderCoordinatorTests.Key(); var position = new ReaderPosition(key,"chapter",10);
        var row = new EbookProgress { BookId = book.Id, HasStarted = true, CurrentPercent = 10,
            CompletedUtcTicks = DateTime.UtcNow.Ticks, CompletedOffsetMinutes = 480,
            DocumentKeyJson = ReaderContractValidator.WriteDocument(key), LastPositionJson = ReaderContractValidator.WritePosition(position) };
        BackupEntityValidator.ValidateProgress(row);
        await db.WriteAsync(c => c.Insert(row));
        await db.WriteAsync(c => c.Insert(new EbookReadingSession { SessionId = Guid.NewGuid(), BookId = book.Id, BookUid = book.BookUid, DurationMilliseconds = 35001 }));
        Assert.Equal("已完成", (await books.GetProgressAsync(book.Id))!.State);
        Assert.Single((await books.SearchAsync("", "已完成", 1, 20)).Items);
        Assert.Equal(1, (await new StatisticsService(db, reading).GetAsync()).CompletedBooks);
        await Assert.ThrowsAsync<ArgumentException>(() => plans.SaveAsync(new() { BookId = book.Id, TargetDate = DateTime.Today }));
        await db.WriteAsync(c => { c.Insert(new ReadingPlan { BookId = book.Id, StartDate = DateTime.Today, TargetDate = DateTime.Today }); return c.Insert(new ReadingPlan { BookId = book.Id, StartDate = DateTime.Today, TargetDate = DateTime.Today }); });
        Assert.All(await plans.GetAllAsync(), p => Assert.Equal("已完成", p.State));
        var reset = new EbookProgressMaintenance(db, db.Coordinator); var oldEpoch = db.Coordinator.CurrentEpoch;
        await Assert.ThrowsAsync<ArgumentException>(() => reset.ResetAsync(book.Id, book.BookUid, oldEpoch, default));
        Assert.Equal(oldEpoch, db.Coordinator.CurrentEpoch); Assert.Equal("已完成", (await books.GetProgressAsync(book.Id))!.State);
        await db.WriteAsync(c => c.Execute("DELETE FROM ReadingPlan WHERE Id=(SELECT max(Id) FROM ReadingPlan)"));
        var identity = new ReaderOpenIdentity(book.Id, book.BookUid, key, oldEpoch, Guid.NewGuid(), 1);
        await using (var registration = await db.Coordinator.RegisterReaderAsync(identity, default))
            await Assert.ThrowsAsync<InvalidOperationException>(() => reset.ResetAsync(book.Id, book.BookUid, oldEpoch, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reset.ResetAsync(book.Id, Guid.NewGuid(), oldEpoch, default));
        await reset.ResetAsync(book.Id, book.BookUid, oldEpoch, default);
        Assert.NotEqual(oldEpoch, db.Coordinator.CurrentEpoch); Assert.Equal("未开始", (await books.GetProgressAsync(book.Id))!.State);
        Assert.Single(await db.ReadAsync(c => c.Table<EbookReadingSession>().ToList()));
        Assert.Equal(35001, (await books.GetProgressAsync(book.Id))!.DurationMilliseconds);
        Assert.Equal("进行中", (await plans.GetAllAsync()).Single().State);
    }
    [Fact]
    public async Task LegacyJsonUpgradeRegeneratesIdentityAndCannotSilentlyDropEbookData()
    {
        var reading = new ReadingService(); var source = DemoData.Create();
        var adapter = new BackupV1Adapter(new BackupEntityValidator(reading));
        var a = adapter.Upgrade(source); var b = adapter.Upgrade(source);
        Assert.All(a.Books, row => { Assert.Equal(BookKind.Manual, row.Kind); Assert.NotEqual(Guid.Empty, row.BookUid); });
        Assert.NotEqual(a.Books[0].BookUid, b.Books[0].BookUid); Assert.All(a.Quotes, q => Assert.Equal(0,q.Revision));
        var paths = new DataPaths(_directory); using var db = new DatabaseService(paths);
        await new BookStorage(db, reading).SaveAsync(new() { Title = "合成电子书", Kind = BookKind.Ebook });
        var backup = new BackupService(db, reading, new Files(), paths);
        var input = Path.Combine(_directory,"old.json"); await File.WriteAllTextAsync(input,JsonSerializer.Serialize(source));
        await Assert.ThrowsAsync<InvalidOperationException>(() => backup.ExportAsync(Path.Combine(_directory,"export.json")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => backup.RestoreAsync(input));
        Assert.Single(await db.ReadAsync(c => c.Table<Book>().ToList())); Assert.False(File.Exists(Path.Combine(_directory,"export.json")));
    }
    [Fact]
    public void PositionSerializationHasVersionAndRejectsInvalidDocuments()
    {
        var position = new ReaderPosition(ReaderCoordinatorTests.Key(),"正文",0);
        Assert.Equal(position, ReaderContractValidator.ReadPosition(ReaderContractValidator.WritePosition(position)));
        Assert.Throws<ArgumentException>(() => ReaderContractValidator.ReadPosition(ReaderContractValidator.WritePosition(position).Replace("\"ContractVersion\":1", "\"ContractVersion\":2")));
        Assert.Throws<JsonException>(() => ReaderContractValidator.ReadPosition(ReaderContractValidator.WritePosition(position).Replace("\"Utf16Offset\":0,", "")));
        Assert.Throws<ArgumentException>(() => ReaderContractValidator.WritePosition(position with { Utf16Offset = -1 }));
        Assert.Throws<ArgumentException>(() => ReaderContractValidator.Validate(position.Document with { CanonicalSha256 = new string('A',64) }));
    }
    [Fact]
    public async Task RealBookEditorPreservesExistingIdentityAcrossDetachedSave()
    {
        using var db = new DatabaseService(new DataPaths(_directory)); var books = new BookStorage(db,new ReadingService());
        var book = new Book { Title = "原书名",TotalPages = 100 }; await books.SaveAsync(book);
        var editor = new BookEditViewModel(books,Mock.Of<IAlertService>(),new MainViewModel(Mock.Of<IMenuNavigationService>()));
        editor.SetParameter(book.Id); await editor.OnLoadedCommand.ExecuteAsync(null); editor.Title = "修改后";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Empty(editor.Error); var saved = (await books.GetAsync(book.Id))!;
        Assert.Equal(book.BookUid,saved.BookUid); Assert.Equal(BookKind.Manual,saved.Kind); Assert.Equal("修改后",saved.Title);
    }
    [Fact]
    public async Task StatisticsSumMillisecondsBeforeRoundingAndDoNotInventEbookPages()
    {
        using var db = new DatabaseService(new DataPaths(_directory));
        var book = new Book { Title = "合成",Kind = BookKind.Ebook }; await new BookStorage(db,new ReadingService()).SaveAsync(book);
        await db.WriteAsync(c =>
        {
            foreach(var milliseconds in new long[]{59001,1999}) c.Insert(new EbookReadingSession { SessionId = Guid.NewGuid(),BookId = book.Id,BookUid = book.BookUid,
                DurationMilliseconds = milliseconds,CloseLocalDate = DateTime.Today.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture) });
            return 0;
        });
        var stats = await new StatisticsService(db,new ReadingService()).GetAsync();
        Assert.Equal(61000,stats.DurationMilliseconds); Assert.Equal(1,stats.TotalMinutes); Assert.Equal(0,stats.TotalPages); Assert.Equal(2,stats.SessionCount);
        Assert.Equal(61000,stats.LastSevenDays.Last().DurationMilliseconds); Assert.Equal(1,stats.LastSevenDays.Last().Minutes);
        var progress = (await new BookStorage(db,new ReadingService()).GetProgressAsync(book.Id))!;
        Assert.Equal("未开始",progress.State); Assert.Equal(61000,progress.DurationMilliseconds); Assert.Equal(2,progress.SessionCount);
    }
    private sealed class Files : IFileService
    {
        public Task<string?> ChooseSavePathAsync() => Task.FromResult<string?>(null);
        public Task<string?> ChooseOpenPathAsync() => Task.FromResult<string?>(null);
        public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
        public Task WriteTextAsync(string path,string content) => File.WriteAllTextAsync(path,content);
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory,true); }
}
