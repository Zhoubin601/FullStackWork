using SQLite;
using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
// 阅记新增：共享连接串行化和事务。参考示例未提供这些原子性保护。
public class DatabaseService : IDatabaseService, IDisposable
{
    private readonly DataPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteConnection? _connection;
    public bool IsInitialized { get; private set; }
    public DatabaseService(DataPaths paths) { _paths = paths; }
    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (IsInitialized) return;
            await Task.Run(() =>
            {
                Directory.CreateDirectory(_paths.DirectoryPath);
                var connection = new SQLiteConnection(_paths.DatabasePath);
                try
                {
                    connection.Execute("PRAGMA foreign_keys=ON");
                    connection.ExecuteScalar<int>("PRAGMA busy_timeout=5000");
                    var version = connection.ExecuteScalar<int>("PRAGMA user_version");
                    if (version > 1) throw new InvalidOperationException("数据库版本高于当前应用支持的版本。");
                    connection.RunInTransaction(() =>
                    {
                        connection.CreateTable<Book>();
                        connection.Execute("CREATE TABLE IF NOT EXISTS ReadingRecord (Id INTEGER PRIMARY KEY AUTOINCREMENT, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, ReadingDate BIGINT NOT NULL, StartPage INTEGER NOT NULL CHECK(StartPage>=1), EndPage INTEGER NOT NULL CHECK(EndPage>=StartPage), Minutes INTEGER NOT NULL CHECK(Minutes>0))");
                        connection.Execute("CREATE TABLE IF NOT EXISTS Quote (Id INTEGER PRIMARY KEY AUTOINCREMENT, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, Text TEXT NOT NULL, Thoughts TEXT, Page INTEGER, IsFavorite INTEGER NOT NULL, CreatedAt BIGINT NOT NULL, UpdatedAt BIGINT NOT NULL)");
                        connection.Execute("CREATE TABLE IF NOT EXISTS ReadingPlan (Id INTEGER PRIMARY KEY AUTOINCREMENT, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, StartDate BIGINT NOT NULL, TargetDate BIGINT NOT NULL)");
                        connection.Execute("PRAGMA user_version=1");
                    });
                    _connection = connection;
                    IsInitialized = true;
                }
                catch { connection.Dispose(); throw; }
            });
        }
        finally { _gate.Release(); }
    }
    public Task<T> ReadAsync<T>(Func<SQLiteConnection, T> read) => ExecuteAsync(read, false);
    public Task<T> WriteAsync<T>(Func<SQLiteConnection, T> write) => ExecuteAsync(write, true);
    private async Task<T> ExecuteAsync<T>(Func<SQLiteConnection, T> action, bool transaction)
    {
        await InitializeAsync(); await _gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                if (!transaction) return action(_connection!);
                T result = default!; _connection!.RunInTransaction(() => result = action(_connection)); return result;
            });
        }
        finally { _gate.Release(); }
    }
    public void Dispose() { _gate.Wait(); try { _connection?.Dispose(); _connection = null; IsInitialized = false; } finally { _gate.Release(); } }
}
