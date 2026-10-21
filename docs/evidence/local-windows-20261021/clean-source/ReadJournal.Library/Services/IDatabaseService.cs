using SQLite;
namespace ReadJournal.Library.Services;
public interface IDatabaseService
{
    bool IsInitialized { get; }
    Task InitializeAsync();
    Task<T> ReadAsync<T>(Func<SQLiteConnection, T> read);
    Task<T> WriteAsync<T>(Func<SQLiteConnection, T> write);
}
