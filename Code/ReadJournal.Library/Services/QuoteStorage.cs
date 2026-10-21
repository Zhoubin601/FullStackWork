using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public class QuoteStorage : IQuoteStorage
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading;
    public QuoteStorage(IDatabaseService database, IReadingService reading) { _database = database; _reading = reading; }
    public Task<Quote?> GetAsync(int id) => _database.ReadAsync(c => (Quote?)c.Find<Quote>(id));
    public Task<IReadOnlyList<QuoteItem>> SearchAsync(string query, bool favoritesOnly, int? bookId = null) => _database.ReadAsync(c =>
    {
        var books = c.Table<Book>().ToDictionary(x => x.Id); var text = query.Trim();
        return (IReadOnlyList<QuoteItem>)c.Table<Quote>().OrderByDescending(x => x.UpdatedAt).ToList()
         .Where(x => (!favoritesOnly || x.IsFavorite) && (!bookId.HasValue || x.BookId == bookId) && books.ContainsKey(x.BookId))
         .Where(x => x.Text.Contains(text, StringComparison.OrdinalIgnoreCase) || books[x.BookId].Title.Contains(text, StringComparison.OrdinalIgnoreCase))
         .Select(x => new QuoteItem { Quote = x, BookTitle = books[x.BookId].Title }).ToArray();
    });
    public Task<int> SaveAsync(Quote quote) => _database.WriteAsync(c =>
    {
        var book = c.Find<Book>(quote.BookId) ?? throw new ArgumentException("请选择有效的书籍。");
        var old = quote.Id == 0 ? null : c.Find<Quote>(quote.Id);
        if (quote.Id != 0 && old is null) throw new InvalidOperationException("书摘已被删除。");
        _reading.ValidateQuote(quote, book); quote.UpdatedAt = DateTime.Now; quote.CreatedAt = old?.CreatedAt ?? quote.UpdatedAt;
        if (old is null) c.Insert(quote); else c.Update(quote); return quote.Id;
    });
    public Task ToggleFavoriteAsync(int id) => _database.WriteAsync(c =>
    {
        // Read and change the current row in one transaction; never write a detached quote snapshot.
        var quote = c.Find<Quote>(id) ?? throw new InvalidOperationException("书摘已不存在。");
        return c.Execute("UPDATE Quote SET IsFavorite = ?, UpdatedAt = ? WHERE Id = ?", !quote.IsFavorite, DateTime.Now, id);
    });
    public Task DeleteAsync(int id) => _database.WriteAsync(c => c.Delete<Quote>(id));
}
