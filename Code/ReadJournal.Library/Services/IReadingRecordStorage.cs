using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public interface IReadingRecordStorage
{
    Task<IReadOnlyList<ReadingRecord>> GetForBookAsync(int bookId);
    Task<IReadOnlyList<string>> GetRecentSummariesAsync(int limit);
    Task<int> SaveAsync(ReadingRecord record);
    Task DeleteAsync(int id);
}
