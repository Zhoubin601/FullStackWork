using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;
public class BookStorage : IBookStorage
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading;
    private readonly IReadingStateCalculator _state;
    public BookStorage(IDatabaseService database, IReadingService reading) : this(database, reading, new ReadingStateCalculator()) { }
    public BookStorage(IDatabaseService database, IReadingService reading, IReadingStateCalculator state) { _database = database; _reading = reading; _state = state; }
    public Task<Book?> GetAsync(int id) => _database.ReadAsync(c => (Book?)c.Find<Book>(id));
    public Task<IReadOnlyList<Book>> GetAllAsync() => _database.ReadAsync(c => (IReadOnlyList<Book>)c.Table<Book>().OrderBy(x => x.Title).ToList());
    public Task<BookProgress?> GetProgressAsync(int id) => _database.ReadAsync(c =>
    {
        var book = c.Find<Book>(id); return book is null ? null : new ReadingStateQuery(c, _state).Calculate(book);
    });
    public Task<PageResult<BookProgress>> SearchAsync(string query, string state, int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(page));
        return _database.ReadAsync(c =>
        {
            var states = new ReadingStateQuery(c, _state);
            var all = c.Table<Book>().OrderByDescending(x => x.UpdatedAt).ToList()
             .Where(x => x.Title.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) || x.Author.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
             .Select(x => states.Calculate(x)).Where(x => state == "全部" || x.State == state).ToArray();
            return new PageResult<BookProgress> { TotalCount = all.Length, Items = all.Skip((page - 1) * pageSize).Take(pageSize).ToArray() };
        });
    }
    public Task<int> SaveAsync(Book book) => _database.WriteAsync(c =>
    {
        var old = book.Id == 0 ? null : c.Find<Book>(book.Id);
        if (book.Id != 0 && old is null) throw new InvalidOperationException("书籍已被删除。");
        if (old is not null && (old.Kind != book.Kind || old.BookUid != book.BookUid)) throw new ArgumentException("不能转换书籍类型或更换书籍身份。");
        var maximum = book.Id == 0 ? 0 : Math.Max(c.Table<ReadingRecord>().Where(x => x.BookId == book.Id).Select(x => x.EndPage).DefaultIfEmpty(0).Max(), c.Table<Quote>().Where(x => x.BookId == book.Id).Select(x => x.Page ?? 0).DefaultIfEmpty(0).Max());
        _reading.ValidateBook(book, maximum);
        if (book.Kind == BookKind.Manual) ReadingPlanConsistency.Validate(_reading, book, c.Table<ReadingRecord>().Where(x => x.BookId == book.Id), c.Table<ReadingPlan>().Where(x => x.BookId == book.Id));
        book.Title = book.Title.Trim(); book.UpdatedAt = DateTime.Now; book.CreatedAt = old?.CreatedAt ?? book.UpdatedAt;
        if (old is null) c.Insert(book); else c.Update(book); return book.Id;
    });
    public Task<string> GetDeleteImpactAsync(int id) => _database.ReadAsync(c => $"将同时删除 {c.Table<ReadingRecord>().Count(x => x.BookId == id)} 条阅读记录、{c.Table<Quote>().Count(x => x.BookId == id)} 条书摘和 {c.Table<ReadingPlan>().Count(x => x.BookId == id)} 个计划。此操作不能撤销。");
    public Task DeleteAsync(int id) => _database.WriteAsync(c => c.Delete<Book>(id));
}
