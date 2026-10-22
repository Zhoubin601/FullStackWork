using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;

public sealed class BackupEntityValidator
{
    private readonly IReadingService _reading;
    public BackupEntityValidator(IReadingService reading) { _reading = reading; }
    public void ValidateV1(BackupDocument doc)
    {
        if (doc.FormatVersion != 1) throw new ArgumentException("不支持此备份格式版本。");
        if (doc.Books is null || doc.ReadingRecords is null || doc.Quotes is null || doc.ReadingPlans is null) throw new ArgumentException("备份缺少实体集合。");
        void Ids<T>(IEnumerable<T> items, Func<T, int> id)
        {
            var all = items.ToArray();
            if (all.Any(x => x is null) || all.Select(id).Any(x => x <= 0) || all.Select(id).Distinct().Count() != all.Length)
                throw new ArgumentException("备份包含空实体、非法或重复 ID。");
        }
        Ids(doc.Books, b => b.Id); Ids(doc.ReadingRecords, r => r.Id); Ids(doc.Quotes, q => q.Id); Ids(doc.ReadingPlans, p => p.Id);
        var books = doc.Books.ToDictionary(x => x.Id);
        Book Parent(int id) => books.TryGetValue(id, out var b) ? b : throw new ArgumentException("备份包含孤立关联。");
        foreach (var b in doc.Books)
        {
            if (b.Kind != BookKind.Manual) throw new ArgumentException("旧 JSON 不包含电子书文件，不能作为电子书备份。");
            _reading.ValidateBook(b, 0);
            if (b.Author is null || b.Category is null || b.Description is null || b.CreatedAt == default || b.UpdatedAt < b.CreatedAt) throw new ArgumentException("书籍字段或时间无效。");
        }
        foreach (var row in doc.ReadingRecords) _reading.ValidateRecord(row, Parent(row.BookId));
        foreach (var q in doc.Quotes)
        {
            _reading.ValidateQuote(q, Parent(q.BookId));
            if (q.Thoughts is null || q.CreatedAt == default || q.UpdatedAt < q.CreatedAt) throw new ArgumentException("书摘字段或时间无效。");
        }
        foreach (var p in doc.ReadingPlans)
        {
            Parent(p.BookId);
            if (p.StartDate == default || p.StartDate.Date > DateTime.Today || p.TargetDate.Date < p.StartDate.Date) throw new ArgumentException("计划日期无效。");
        }
        foreach (var group in doc.ReadingPlans.GroupBy(x => x.BookId)) ReadingPlanConsistency.Validate(_reading, Parent(group.Key), doc.ReadingRecords, group);
    }
    public static void ValidateProgress(EbookProgress row)
    {
        var key = ReaderContractValidator.ReadDocument(row.DocumentKeyJson);
        var position = ReaderContractValidator.ReadPosition(row.LastPositionJson);
        if (key != position.Document || !double.IsFinite(row.CurrentPercent) || row.CurrentPercent < 0 || row.CurrentPercent > 100 ||
            (row.CompletedUtcTicks.HasValue != row.CompletedOffsetMinutes.HasValue) || (row.CompletedUtcTicks.HasValue && !row.HasStarted))
            throw new ArgumentException("电子书进度字段不一致。");
        if (row.CompletedUtcTicks is long ticks)
            _ = new DateTimeOffset(ticks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(row.CompletedOffsetMinutes!.Value));
    }
}
