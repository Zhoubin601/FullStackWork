using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public class ReadingRecordStorage : IReadingRecordStorage
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading;
    public ReadingRecordStorage(IDatabaseService database, IReadingService reading) { _database = database; _reading = reading; }
    public Task<IReadOnlyList<ReadingRecord>> GetForBookAsync(int bookId) => _database.ReadAsync(c => (IReadOnlyList<ReadingRecord>)c.Table<ReadingRecord>().Where(x => x.BookId == bookId).OrderByDescending(x => x.ReadingDate).ThenByDescending(x => x.Id).ToList());
    public Task<int> SaveAsync(ReadingRecord record) => _database.WriteAsync(c =>
    {
        var book = c.Find<Book>(record.BookId) ?? throw new ArgumentException("请先选择有效的书籍。");
        if (record.Id != 0 && c.Find<ReadingRecord>(record.Id) is null) throw new InvalidOperationException("阅读记录已被删除。");
        _reading.ValidateRecord(record, book); record.ReadingDate = record.ReadingDate.Date;
        if (record.Id == 0) c.Insert(record); else c.Update(record); return record.Id;
    });
    public Task DeleteAsync(int id) => _database.WriteAsync(c => c.Delete<ReadingRecord>(id));
}
