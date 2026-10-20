using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public interface IBookStorage
{
    Task<Book?> GetAsync(int id);
    Task<IReadOnlyList<Book>> GetAllAsync();
    Task<BookProgress?> GetProgressAsync(int id);
    Task<PageResult<BookProgress>> SearchAsync(string query, string state, int page, int pageSize);
    Task<int> SaveAsync(Book book);
    Task<string> GetDeleteImpactAsync(int id);
    Task DeleteAsync(int id);
}
