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
        var old = record.Id == 0 ? null : c.Find<ReadingRecord>(record.Id);
        if (record.Id != 0 && old is null) throw new InvalidOperationException("阅读记录已被删除。");
        _reading.ValidateRecord(record, book);
        var remaining = c.Table<ReadingRecord>().ToList().Where(x => x.Id != record.Id).ToArray();
        var plans = c.Table<ReadingPlan>().ToList();
        // 预验算与实际写入共享同一串行事务；更换关联时也保护原书籍的计划。
        if (old is not null && old.BookId != record.BookId)
            ReadingPlanConsistency.Validate(_reading, c.Find<Book>(old.BookId)!, remaining, plans);
        ReadingPlanConsistency.Validate(_reading, book, remaining.Append(record), plans);
        record.ReadingDate = record.ReadingDate.Date;
        if (record.Id == 0) c.Insert(record); else c.Update(record); return record.Id;
    });
    public Task DeleteAsync(int id) => _database.WriteAsync(c =>
    {
        var record = c.Find<ReadingRecord>(id);
        if (record is null) return 0;
        ReadingPlanConsistency.Validate(_reading, c.Find<Book>(record.BookId)!, c.Table<ReadingRecord>().ToList().Where(x => x.Id != id), c.Table<ReadingPlan>().ToList());
        return c.Delete<ReadingRecord>(id);
    });
}
