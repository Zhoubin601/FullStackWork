using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
internal static class ReadingPlanConsistency
{
    // 兼容旧备份中的多个已完成计划，但任何修改都不能让它们同时退回未完成。
    internal static void Validate(IReadingService reading, Book book, IEnumerable<ReadingRecord> records, IEnumerable<ReadingPlan> plans)
    {
        if (plans.Count(x => x.BookId == book.Id) > 1 && reading.Calculate(book, records).State != "已完成")
            throw new ArgumentException("该书籍有多个历史计划，不能同时变为未完成。请先在阅读计划中删除多余计划，再修改阅读记录或总页数；本次操作未保存。");
    }
}
