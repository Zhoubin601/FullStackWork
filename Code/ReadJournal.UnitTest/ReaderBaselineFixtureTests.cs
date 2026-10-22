using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using SQLite;
using Xunit;

namespace ReadJournal.UnitTest;

public class ReaderBaselineFixtureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalReaderBaseline", Guid.NewGuid().ToString("N"));
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Reader", name);
    private static string Canonical(BackupDocument document) => JsonSerializer.Serialize(document);
    private static BackupDocument Expected() => JsonSerializer.Deserialize<BackupDocument>(File.ReadAllText(Fixture("schema1.json")))!;

    // Frozen SQL comes from the original schema 1 engine, not the initializer under test.
    private DatabaseService OpenGoldenDatabase()
    {
        Directory.CreateDirectory(_directory);
        var paths = new DataPaths(_directory);
        using (var connection = new SQLiteConnection(paths.DatabasePath))
        {
            foreach (var sql in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Fixture("schema1-sql.json")))!)
                connection.Execute(sql);
            Assert.Equal(1, connection.ExecuteScalar<int>("PRAGMA user_version"));
        }
        return new DatabaseService(paths);
    }

    private static Task<BackupDocument> Snapshot(DatabaseService database) => database.ReadAsync(c => new BackupDocument
    {
        Books = c.Table<Book>().OrderBy(x => x.Id).ToList(),
        ReadingRecords = c.Table<ReadingRecord>().OrderBy(x => x.Id).ToList(),
        Quotes = c.Table<Quote>().OrderBy(x => x.Id).ToList(),
        ReadingPlans = c.Table<ReadingPlan>().OrderBy(x => x.Id).ToList()
    });

    [Fact]
    public async Task FrozenSchema1ReadsAllFieldsAndLegacyStatesWithoutLoss()
    {
        using var database = OpenGoldenDatabase();
        await database.InitializeAsync();
        Assert.Equal(2, await database.ReadAsync(c => c.ExecuteScalar<int>("PRAGMA user_version")));
        Assert.Equal("ok", await database.ReadAsync(c => c.ExecuteScalar<string>("PRAGMA integrity_check")));
        Assert.Empty(await database.ReadAsync(c => c.Query<ForeignKeyViolation>("PRAGMA foreign_key_check")));
        Assert.Equal(Canonical(Expected()), Canonical(await Snapshot(database)));
        var reading = new ReadingService();
        var books = new BookStorage(database, reading);
        var progress = (await books.GetProgressAsync(11))!;
        Assert.Equal("在读", progress.State);
        Assert.Equal(20, progress.CurrentPage);
        Assert.Equal(21, progress.TotalReadPages); // Overlap is counted as reading activity.
        Assert.Equal("未开始", (await books.GetProgressAsync(93))!.State);
        Assert.Equal("已完成", (await books.GetProgressAsync(27))!.State);
        var plans = await new ReadingPlanStorage(database, reading).GetAllAsync();
        Assert.Equal(2, plans.Count(p => p.Plan.BookId == 27 && p.State == "已完成"));
    }

    [Fact]
    public async Task FrozenJsonRoundTripsSparseIdsAndKeepsHistoricalPlanProtection()
    {
        using var database = new DatabaseService(new DataPaths(_directory));
        var reading = new ReadingService();
        var backup = new BackupService(database, reading, new LocalFiles(), new DataPaths(_directory));
        var originalBytes = await File.ReadAllBytesAsync(Fixture("schema1.json"));
        await backup.RestoreAsync(Fixture("schema1.json"));
        Assert.Equal(Canonical(Expected()), Canonical(await Snapshot(database)));
        var exportPath = Path.Combine(_directory, "export.json");
        await backup.ExportAsync(exportPath);
        Assert.Equal(Canonical(Expected()), Canonical(JsonSerializer.Deserialize<BackupDocument>(await File.ReadAllTextAsync(exportPath))!));
        await new BookStorage(database, reading).DeleteAsync(11);
        await backup.RestoreAsync(exportPath);
        Assert.Equal(Canonical(Expected()), Canonical(await Snapshot(database)));
        await Assert.ThrowsAsync<ArgumentException>(() => new ReadingRecordStorage(database, reading).DeleteAsync(203));
        Assert.Equal(Canonical(Expected()), Canonical(await Snapshot(database)));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(Fixture("schema1.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class ForeignKeyViolation { public string table { get; set; } = ""; }
    private sealed class LocalFiles : IFileService
    {
        public Task<string?> ChooseSavePathAsync() => Task.FromResult<string?>(null);
        public Task<string?> ChooseOpenPathAsync() => Task.FromResult<string?>(null);
        public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
        public Task WriteTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);
    }
}
