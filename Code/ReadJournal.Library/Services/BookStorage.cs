using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;
public class BookStorage : IBookStorage
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading;
    private readonly IReadingStateCalculator _state;
    private readonly BookDeletionService? _deletion;
    public BookStorage(IDatabaseService database, IReadingService reading) : this(database, reading, new ReadingStateCalculator()) { }
    public BookStorage(IDatabaseService database, IReadingService reading, IReadingStateCalculator state)
    { _database = database; _reading = reading; _state = state; if (database is DatabaseService actual) _deletion = EbookRuntime.CreateDeletion(actual); }
    public BookStorage(IDatabaseService database, IReadingService reading, IReadingStateCalculator state, BookDeletionService deletion)
        : this(database, reading, state) { _deletion = deletion; }
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
    public async Task<string> GetDeleteImpactAsync(int id)
    {
        var impact = await (_deletion ?? throw new InvalidOperationException("请注册统一删除服务。")).GetImpactAsync(id, CancellationToken.None);
        return $"将同时删除 {impact.ManualRecords} 条手动阅读记录、{impact.Sessions} 次电子阅读会话、{impact.Quotes} 条书摘和 {impact.Plans} 个计划，以及 {impact.ManagedFileBytes} 字节受管文件。外部和收件来源保留。此操作不能撤销。";
    }
    public async Task DeleteAsync(int id)
    {
        if (_database is not DatabaseService actual) throw new InvalidOperationException("请通过正式数据协调器调用统一删除。");
        await _database.InitializeAsync();
        var epoch = actual.Coordinator.CurrentEpoch;
        var book = await GetAsync(id) ?? throw new ArgumentException("书籍不存在。");
        await (_deletion ?? throw new InvalidOperationException("请注册统一删除服务。")).DeleteAsync(id, book.BookUid, epoch, CancellationToken.None);
    }
}
