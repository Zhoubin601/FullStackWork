using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public interface IQuoteStorage
{
    Task<Quote?> GetAsync(int id);
    Task<IReadOnlyList<QuoteItem>> SearchAsync(string query, bool favoritesOnly, int? bookId = null);
    Task<int> SaveAsync(Quote quote);
    Task DeleteAsync(int id);
}
