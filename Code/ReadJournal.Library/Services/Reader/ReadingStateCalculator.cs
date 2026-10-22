using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Reader;

public sealed class ReadingStateCalculator : IReadingStateCalculator
{
    public BookProgress Calculate(Book book, IReadOnlyList<ReadingRecord> manualRecords, EbookStateInput? ebook)
    {
        if (book.Kind == BookKind.Manual)
        {
            var all = manualRecords.Where(x => x.BookId == book.Id).ToArray();
            return new BookProgress { Book = book,
                CurrentPage = all.Select(x => x.EndPage).DefaultIfEmpty(0).Max(),
                TotalReadPages = all.Sum(x => (long)x.EndPage - x.StartPage + 1),
                TotalMinutes = all.Sum(x => (long)x.Minutes),
                DurationMilliseconds = all.Sum(x => checked((long)x.Minutes * 60000)),
                CompletedOn = all.Where(x => x.EndPage == book.TotalPages).Select(x => (DateTime?)x.ReadingDate.Date).Min() };
        }
        if (book.Kind != BookKind.Ebook) throw new ArgumentException("书籍类型无效。");
        var value = ebook ?? new EbookStateInput(false, null, 0, 0, 0);
        if (!double.IsFinite(value.CurrentPercent) || value.CurrentPercent < 0 || value.CurrentPercent > 100 || value.SessionMilliseconds < 0 || value.SessionCount < 0 || (value.CompletedAt.HasValue && !value.HasStarted))
            throw new ArgumentException("电子书进度无效。");
        return new BookProgress { Book = book, HasStarted = value.HasStarted, EbookCompletedAt = value.CompletedAt,
            CurrentPercent = value.CurrentPercent, DurationMilliseconds = value.SessionMilliseconds,
            TotalMinutes = value.SessionMilliseconds / 60000, SessionCount = value.SessionCount,
            CompletedOn = value.CompletedAt?.Date };
    }
    public static void ValidateReset(Book book, int historicalPlanCount)
    {
        if (book.Kind != BookKind.Ebook) throw new ArgumentException("只有电子书可重置正文进度。");
        if (historicalPlanCount > 1) throw new ArgumentException("该书籍有多个历史计划，请先删除多余计划再重置进度。");
    }
}
