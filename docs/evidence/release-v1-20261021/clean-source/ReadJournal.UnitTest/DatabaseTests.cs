using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using Xunit;
namespace ReadJournal.UnitTest;
public class DatabaseTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString());
    [Fact]
    public async Task InitializesVersionAndPersistsAcrossReopen()
    {
        var paths = new DataPaths(_directory);
        using (var db = new DatabaseService(paths))
        {
            await db.InitializeAsync(); await db.InitializeAsync();
            Assert.Equal(1, await db.ReadAsync(c => c.ExecuteScalar<int>("PRAGMA user_version")));
            await db.WriteAsync(c => c.Insert(new Book { Title = "持久化", TotalPages = 100 }));
        }
        using (var db = new DatabaseService(paths)) { await db.InitializeAsync(); Assert.Equal("持久化", await db.ReadAsync(c => c.Table<Book>().Single().Title)); }
    }
    [Fact]
    public async Task ActualSqliteTransactionRollsBackOnConstraintFailure()
    {
        using var db = new DatabaseService(new DataPaths(_directory));
        await Assert.ThrowsAnyAsync<Exception>(() => db.WriteAsync(c =>
        {
            c.Insert(new Book { Title = "应回滚", TotalPages = 100 });
            return c.Insert(new ReadingRecord { BookId = 999, StartPage = 1, EndPage = 2, Minutes = 1 });
        }));
        Assert.Equal(0, await db.ReadAsync(c => c.Table<Book>().Count()));
    }
    [Fact]
    public async Task FailedDirectoryCanRetryAfterCauseRemoved()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_directory)!); await File.WriteAllTextAsync(_directory, "阻挡目录创建");
        using var db = new DatabaseService(new DataPaths(_directory));
        await Assert.ThrowsAnyAsync<IOException>(() => db.InitializeAsync()); Assert.False(db.IsInitialized);
        File.Delete(_directory); await db.InitializeAsync(); Assert.True(db.IsInitialized);
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); if (File.Exists(_directory)) File.Delete(_directory); }
}
