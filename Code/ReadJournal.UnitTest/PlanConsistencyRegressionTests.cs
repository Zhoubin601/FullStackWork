using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using Xunit;
namespace ReadJournal.UnitTest;
public class PlanConsistencyRegressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalPlanConsistency", Guid.NewGuid().ToString());
    private readonly DatabaseService _database;
    private readonly BookStorage _books;
    private readonly ReadingRecordStorage _records;
    private readonly ReadingPlanStorage _plans;
    private readonly BackupService _backup;
    public PlanConsistencyRegressionTests()
    {
        var paths = new DataPaths(_directory); var reading = new ReadingService();
        _database = new(paths); _books = new(_database, reading); _records = new(_database, reading);
        _plans = new(_database, reading); _backup = new(_database, reading, new TestFiles(), paths);
    }
    [Fact]
    public async Task RemovingCompletionRecordMustNotProduceTwoUnfinishedPlans()
    {
        var (book, other, completion, first, second) = await CreateOrdinaryBooksAndPlansAsync();
        second.BookId = book.Id;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => _plans.SaveAsync(second));
        Assert.Contains("已有阅读计划", error.Message);
        Assert.Equal(other.Id, (await _plans.GetAsync(second.Id))!.BookId);
        Assert.Equal(book.Id, (await _plans.GetAsync(first.Id))!.BookId);
        Assert.Single(await _records.GetForBookAsync(book.Id));
        await _records.DeleteAsync(completion.Id);
        var plans = await _plans.GetAllAsync();
        Assert.Single(plans.Where(p => p.Plan.BookId == book.Id && p.State != "已完成"));
        Assert.Single(plans.Where(p => p.Plan.BookId == other.Id && p.State != "已完成"));
    }
    [Fact]
    public async Task BackupExportedAfterLegalUiOperationsMustPassOwnValidation()
    {
        var (book, other, completion, _, second) = await CreateOrdinaryBooksAndPlansAsync();
        second.BookId = book.Id;
        await Assert.ThrowsAsync<ArgumentException>(() => _plans.SaveAsync(second));
        await _records.DeleteAsync(completion.Id);
        Assert.Equal(other.Id, (await _plans.GetAsync(second.Id))!.BookId);
        await AssertBackupRoundTripAsync();
        Assert.Equal(2, (await _plans.GetAllAsync()).Count);
        Assert.Empty(await _records.GetForBookAsync(book.Id));
    }
    [Theory]
    [InlineData("delete-record")]
    [InlineData("edit-record")]
    [InlineData("move-record")]
    [InlineData("increase-pages")]
    public async Task LegacyCompletedPlansRejectInvalidatingChangesWithoutPartialWrites(string operation)
    {
        await RestoreLegacyCompletedPlansAsync();
        var before = await ExportSnapshotAsync();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ApplyLegacyChangeAsync(operation));
        Assert.Contains("删除多余计划", error.Message);
        Assert.Equal(before, await ExportSnapshotAsync());
        Assert.Equal(2, (await _plans.GetAllAsync()).Count(p => p.State == "已完成"));
        Assert.Equal(100, (await _records.GetForBookAsync(10)).Single().EndPage);
        Assert.Equal(100, (await _books.GetAsync(10))!.TotalPages);
        Assert.Empty(await _records.GetForBookAsync(20));
        await AssertBackupRoundTripAsync();

        // 用户明确删除多余计划后，同一业务修改可正常完成，没有自动删记录或计划。
        await _plans.DeleteAsync(102);
        await ApplyLegacyChangeAsync(operation);
        var remaining = (await _plans.GetAllAsync()).Single();
        Assert.Equal(101, remaining.Plan.Id); Assert.NotEqual("已完成", remaining.State);
        if (operation == "delete-record") Assert.Empty(await _records.GetForBookAsync(10));
        else if (operation == "move-record") Assert.Single(await _records.GetForBookAsync(20));
        else Assert.Single(await _records.GetForBookAsync(10));
        await AssertBackupRoundTripAsync();
    }
    [Fact]
    public async Task LegacyCompletedPlansAllowSafeChangesAndRetainHistoryThroughBackup()
    {
        await RestoreLegacyCompletedPlansAsync();
        var extra = new ReadingRecord { BookId = 10, StartPage = 2, EndPage = 3, Minutes = 2 };
        await _records.SaveAsync(extra); await _records.DeleteAsync(extra.Id);
        var completion = (await _records.GetForBookAsync(10)).Single(); completion.Minutes = 15;
        await _records.SaveAsync(completion);
        var book = (await _books.GetAsync(10))!; book.Title = "已安全修改的书名"; await _books.SaveAsync(book);
        Assert.Equal(2, (await _plans.GetAllAsync()).Count(p => p.State == "已完成"));
        await AssertBackupRoundTripAsync();
        Assert.Equal(15, (await _records.GetForBookAsync(10)).Single().Minutes);
        Assert.Equal("已安全修改的书名", (await _books.GetAsync(10))!.Title);
    }
    [Fact]
    public async Task ConcurrentPlanSavesLeaveOnePlanAndRestorableBackup()
    {
        var book = new Book { Title = "并发计划", TotalPages = 100 }; await _books.SaveAsync(book);
        async Task<bool> SaveAsync()
        {
            try { await _plans.SaveAsync(new ReadingPlan { BookId = book.Id, TargetDate = DateTime.Today }); return true; }
            catch (ArgumentException) { return false; }
        }
        var results = await Task.WhenAll(SaveAsync(), SaveAsync());
        Assert.Single(results.Where(x => x)); Assert.Single(await _plans.GetAllAsync());
        await AssertBackupRoundTripAsync();
    }
    private async Task<(Book Book, Book Other, ReadingRecord Completion, ReadingPlan First, ReadingPlan Second)> CreateOrdinaryBooksAndPlansAsync()
    {
        var book = new Book { Title = "A", TotalPages = 100 }; var other = new Book { Title = "B", TotalPages = 100 };
        await _books.SaveAsync(book); await _books.SaveAsync(other);
        var first = new ReadingPlan { BookId = book.Id, TargetDate = DateTime.Today };
        var second = new ReadingPlan { BookId = other.Id, TargetDate = DateTime.Today };
        await _plans.SaveAsync(first); await _plans.SaveAsync(second);
        var completion = new ReadingRecord { BookId = book.Id, StartPage = 1, EndPage = 100, Minutes = 10 };
        await _records.SaveAsync(completion);
        return (book, other, completion, first, second);
    }
    private async Task RestoreLegacyCompletedPlansAsync()
    {
        var doc = new BackupDocument
        {
            Books = [new() { Id = 10, Title = "历史书籍", TotalPages = 100, CreatedAt = DateTime.Today, UpdatedAt = DateTime.Today }, new() { Id = 20, Title = "其他书籍", TotalPages = 100, CreatedAt = DateTime.Today, UpdatedAt = DateTime.Today }],
            ReadingRecords = [new() { Id = 70, BookId = 10, StartPage = 1, EndPage = 100, Minutes = 10 }],
            ReadingPlans = [new() { Id = 101, BookId = 10, StartDate = DateTime.Today, TargetDate = DateTime.Today }, new() { Id = 102, BookId = 10, StartDate = DateTime.Today, TargetDate = DateTime.Today }]
        };
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "legacy.json"); await File.WriteAllTextAsync(path, JsonSerializer.Serialize(doc));
        await _backup.RestoreAsync(path);
        Assert.Equal(2, (await _plans.GetAllAsync()).Count(p => p.State == "已完成"));
    }
    private async Task ApplyLegacyChangeAsync(string operation)
    {
        if (operation == "delete-record") { await _records.DeleteAsync(70); return; }
        if (operation == "increase-pages") { var book = (await _books.GetAsync(10))!; book.TotalPages = 200; book.Title = "修改尚未保存"; await _books.SaveAsync(book); return; }
        var record = (await _records.GetForBookAsync(10)).Single();
        if (operation == "move-record") record.BookId = 20;
        else { record.EndPage = 90; record.Minutes = 9; }
        await _records.SaveAsync(record);
    }
    private async Task<string> ExportSnapshotAsync()
    {
        var path = Path.Combine(_directory, "snapshot.zip"); await _backup.ExportAsync(path); return JsonSerializer.Serialize(ReaderBackupTestData.Legacy(path));
    }
    private async Task AssertBackupRoundTripAsync()
    {
        var before = await ExportSnapshotAsync(); _backup.Validate(JsonSerializer.Deserialize<BackupDocument>(before)!);
        var auto = await _backup.RestoreAsync(Path.Combine(_directory, "snapshot.zip")); Assert.True(File.Exists(auto));
        Assert.Equal(before, await ExportSnapshotAsync());
    }
    public void Dispose() { _database.Dispose(); if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private class TestFiles : IFileService
    {
        public Task<string?> ChooseSavePathAsync() => Task.FromResult<string?>(null);
        public Task<string?> ChooseOpenPathAsync() => Task.FromResult<string?>(null);
        public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
        public Task WriteTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);
    }
}
