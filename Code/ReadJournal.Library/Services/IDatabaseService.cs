using SQLite;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;
public interface IDatabaseService
{
    bool IsInitialized { get; }
    Task InitializeAsync();
    Task<T> ReadAsync<T>(IDataOperationLease lease, Func<SQLiteConnection, T> read);
    Task<T> WriteAsync<T>(IDataOperationLease lease, Func<SQLiteConnection, T> write);
    Task<T> ReadAsync<T>(IDataMaintenanceLease lease, Func<SQLiteConnection, T> read);
    Task<T> WriteAsync<T>(IDataMaintenanceLease lease, Func<SQLiteConnection, T> write);
    Task<T> ReadAsync<T>(Func<SQLiteConnection, T> read);
    Task<T> WriteAsync<T>(Func<SQLiteConnection, T> write);
}
