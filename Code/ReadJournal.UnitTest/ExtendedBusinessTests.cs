using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using Xunit;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.UnitTest;
public class ExtendedBusinessTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString());
    private readonly DatabaseService _db; private readonly ReadingService _reading = new(); private readonly BackupService _backup; private readonly TestFiles _files = new();
    public ExtendedBusinessTests() { var paths = new DataPaths(_dir); _db = new(paths); _backup = new(_db, _reading, _files, paths); }
    [Theory]
    [InlineData(0, 100, 0, "进行中", 100)]
    [InlineData(20, 100, 4, "进行中", 16)]
    [InlineData(20, 100, -1, "已逾期", 0)]
    [InlineData(100, 100, -1, "已完成", 0)]
    public void PlanDatesAndDenominators(int current, int total, int days, string state, int suggestion)
    {
        var item = new PlanItem { Plan = new() { StartDate = DateTime.Today.AddDays(-5), TargetDate = DateTime.Today.AddDays(days) }, Progress = new() { Book = new() { TotalPages = total }, CurrentPage = current } };
        Assert.Equal(state, item.State); Assert.Equal(suggestion, item.SuggestedDailyPages);
    }
    [Fact]
    public async Task PlanUniquenessCompletionAndDeletionDoNotAffectRecords()
    {
        var books = new BookStorage(_db, _reading); var records = new ReadingRecordStorage(_db, _reading); var plans = new ReadingPlanStorage(_db, _reading);
        var b = new Book { Title = "计划", TotalPages = 100 }; await books.SaveAsync(b);
        var plan = new ReadingPlan { BookId = b.Id, TargetDate = DateTime.Today }; await plans.SaveAsync(plan);
        await Assert.ThrowsAsync<ArgumentException>(() => plans.SaveAsync(new() { BookId = b.Id, TargetDate = DateTime.Today }));
        await records.SaveAsync(new() { BookId = b.Id, StartPage = 10, EndPage = 100, Minutes = 1 }); Assert.Equal("已完成", (await plans.GetAllAsync()).Single().State);
        await Assert.ThrowsAsync<ArgumentException>(() => plans.SaveAsync(new() { BookId = b.Id, TargetDate = DateTime.Today }));
        await plans.DeleteAsync(plan.Id); Assert.Single(await records.GetForBookAsync(b.Id));
    }
    [Fact]
    public async Task StatisticsDemoAndBackupRoundTripPreserveIdsAndPreferences()
    {
        await _backup.LoadDemoAsync(); await Assert.ThrowsAsync<InvalidOperationException>(() => _backup.LoadDemoAsync());
        var stats = await new StatisticsService(_db, _reading).GetAsync(); Assert.Equal(191, stats.TotalPages); Assert.Equal(210, stats.TotalMinutes); Assert.Equal(1, stats.CompletedBooks); Assert.Equal(7, stats.LastSevenDays.Count);
        var prefs = new FilePreferenceStorage(new DataPaths(_dir)); await prefs.SetThemeAsync("深色");
        var path = Path.Combine(_dir, "manual.zip"); await _backup.ExportAsync(path); var before = JsonSerializer.Serialize(ReaderBackupTestData.Legacy(path));
        await new BookStorage(_db, _reading).DeleteAsync(1); var auto = await _backup.RestoreAsync(path); Assert.True(File.Exists(auto));
        var after = Path.Combine(_dir, "after.zip"); await _backup.ExportAsync(after); Assert.Equal(before, JsonSerializer.Serialize(ReaderBackupTestData.Legacy(after))); Assert.Equal("深色", await prefs.GetThemeAsync());
        var doc = JsonSerializer.Deserialize<BackupDocument>(before)!; foreach (var book in doc.Books) book.Id *= 10; foreach (var r in doc.ReadingRecords) r.BookId *= 10; foreach (var q in doc.Quotes) q.BookId *= 10; foreach (var p in doc.ReadingPlans) p.BookId *= 10;
        var legacyPath = Path.Combine(_dir,"legacy.json"); await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(doc)); await _backup.RestoreAsync(legacyPath); Assert.NotNull(await new BookStorage(_db, _reading).GetAsync(20));
        Assert.All(await new ReadingRecordStorage(_db, _reading).GetForBookAsync(20), r => Assert.Equal(20, r.BookId));
    }
    [Theory]
    [InlineData("json")]
    [InlineData("missing")]
    [InlineData("version")]
    [InlineData("duplicate")]
    [InlineData("orphan")]
    [InlineData("page")]
    [InlineData("future")]
    [InlineData("plans")]
    [InlineData("null")]
    public async Task InvalidBackupNeverMutatesOriginalData(string kind)
    {
        await _backup.LoadDemoAsync(); var doc = DemoData.Create();
        switch (kind) { case "version": doc.FormatVersion = 99; break; case "duplicate": doc.Books.Add(doc.Books[0]); break; case "orphan": doc.Quotes[0].BookId = 999; break; case "page": doc.ReadingRecords[0].EndPage = 999; break; case "future": doc.ReadingRecords[0].ReadingDate = DateTime.Today.AddDays(1); break; case "plans": doc.ReadingPlans.Add(new() { Id = 9, BookId = 1, StartDate = DateTime.Today, TargetDate = DateTime.Today }); break; case "null": doc.Books[0].Author = null!; break; }
        var path = Path.Combine(_dir, "invalid.json"); await File.WriteAllTextAsync(path, kind == "json" ? "{not json" : kind == "missing" ? "{}" : JsonSerializer.Serialize(doc));
        await Assert.ThrowsAnyAsync<Exception>(() => _backup.RestoreAsync(path)); Assert.Equal(3, await _db.ReadAsync(c => c.Table<Book>().Count())); Assert.Equal(191, (await new StatisticsService(_db, _reading).GetAsync()).TotalPages);
        Assert.Empty(Directory.Exists(Path.Combine(_dir,"backups")) ? Directory.GetFiles(Path.Combine(_dir,"backups"), "auto-backup-*.zip") : []);
    }
    [Fact]
    public async Task AutoBackupFailurePreventsReplacementAndSqliteFailureRollsBack()
    {
        await _backup.LoadDemoAsync(); var path = Path.Combine(_dir, "source.zip"); await _backup.ExportAsync(path);
        var fault = new AutomaticFailure(); var complete = BackupRuntime.CreateService(_db,_reading,new DataPaths(_dir),fault);
        await Assert.ThrowsAsync<IOException>(() => complete.RestoreAsync(path,default)); Assert.Equal(3, await _db.ReadAsync(c => c.Table<Book>().Count()));

        await _db.WriteAsync(c => c.Execute("CREATE TRIGGER fail_quote BEFORE INSERT ON Quote BEGIN SELECT RAISE(FAIL, '模拟替换失败'); END"));
        await Assert.ThrowsAnyAsync<Exception>(() => _backup.RestoreAsync(path));
        Assert.Equal(3, await _db.ReadAsync(c => c.Table<Book>().Count())); Assert.Equal(2, await _db.ReadAsync(c => c.Table<Quote>().Count())); Assert.Equal(4, await _db.ReadAsync(c => c.Table<ReadingRecord>().Count())); Assert.Single(Directory.GetFiles(Path.Combine(_dir,"backups"), "auto-backup-*.zip"));
    }
    public void Dispose() { _db.Dispose(); if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
    private sealed class AutomaticFailure : ManagedBookFiles
    { public override void InstallFile(string source,string destination) { if(Path.GetFileName(destination).StartsWith("auto-backup-"))throw new IOException("injected automatic backup failure");base.InstallFile(source,destination); } }
    private class TestFiles : IFileService
    {
        public Task<string?> ChooseSavePathAsync() => Task.FromResult<string?>(null);
        public Task<string?> ChooseOpenPathAsync() => Task.FromResult<string?>(null);
        public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
        public Task WriteTextAsync(string path, string content) { return File.WriteAllTextAsync(path, content); }
    }
}
