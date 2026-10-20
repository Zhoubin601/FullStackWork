using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public class ReadingService : IReadingService
{
    public void ValidateBook(Book book, int maximumReferencedPage)
    {
        if (string.IsNullOrWhiteSpace(book.Title)) throw new ArgumentException("请填写书名。");
        if (book.TotalPages <= 0) throw new ArgumentException("总页数必须为正整数。");
        if (book.TotalPages < maximumReferencedPage) throw new ArgumentException($"总页数不能小于已有记录或书摘的最大页码 {maximumReferencedPage}。");
    }
    public void ValidateRecord(ReadingRecord record, Book book)
    {
        if (record.BookId != book.Id) throw new ArgumentException("阅读记录的书籍关联无效。");
        if (record.StartPage < 1 || record.EndPage < record.StartPage || record.EndPage > book.TotalPages) throw new ArgumentException("阅读页码必须满足 1 ≤ 起始页 ≤ 结束页 ≤ 总页数。");
        if (record.Minutes <= 0) throw new ArgumentException("阅读分钟数必须为正整数。");
        if (record.ReadingDate.Date > DateTime.Today) throw new ArgumentException("阅读日期不能晚于今天。");
    }
    public void ValidateQuote(Quote quote, Book book)
    {
        if (quote.BookId != book.Id) throw new ArgumentException("书摘的书籍关联无效。");
        if (string.IsNullOrWhiteSpace(quote.Text)) throw new ArgumentException("请填写书摘正文。");
        if (quote.Page is int page && (page < 1 || page > book.TotalPages)) throw new ArgumentException("书摘页码必须在 1 到总页数之间，或留空。");
    }
    public BookProgress Calculate(Book book, IEnumerable<ReadingRecord> records)
    {
        var all = records.Where(x => x.BookId == book.Id).ToArray();
        return new BookProgress
        {
            Book = book,
            CurrentPage = all.Select(x => x.EndPage).DefaultIfEmpty(0).Max(),
            TotalReadPages = all.Sum(x => (long)x.EndPage - x.StartPage + 1),
            TotalMinutes = all.Sum(x => (long)x.Minutes),
            CompletedOn = all.Where(x => x.EndPage == book.TotalPages).Select(x => (DateTime?)x.ReadingDate.Date).Min()
        };
    }
}
