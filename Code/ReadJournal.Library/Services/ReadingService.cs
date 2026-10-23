using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;
public class ReadingService : IReadingService
{
    private readonly IReadingStateCalculator _state;
    public ReadingService() : this(new ReadingStateCalculator()) { }
    public ReadingService(IReadingStateCalculator state) { _state = state; }
    public void ValidateBook(Book book, int maximumReferencedPage)
    {
        if (string.IsNullOrWhiteSpace(book.Title)) throw new ArgumentException("请填写书名。");
        if (!Enum.IsDefined(book.Kind) || book.BookUid == Guid.Empty) throw new ArgumentException("书籍身份或类型无效。");
        if (book.Kind == BookKind.Ebook)
        {
            if (book.TotalPages != 0 || maximumReferencedPage != 0) throw new ArgumentException("电子书不使用固定总页数。");
            return;
        }
        if (book.TotalPages <= 0) throw new ArgumentException("总页数必须为正整数。");
        if (book.TotalPages < maximumReferencedPage) throw new ArgumentException($"总页数不能小于已有记录或书摘的最大页码 {maximumReferencedPage}。");
    }
    public void ValidateRecord(ReadingRecord record, Book book)
    {
        if (book.Kind != BookKind.Manual) throw new ArgumentException("电子书不能新增或编辑手动页码记录。");
        if (record.BookId != book.Id) throw new ArgumentException("阅读记录的书籍关联无效。");
        if (record.StartPage < 1 || record.EndPage < record.StartPage || record.EndPage > book.TotalPages) throw new ArgumentException("阅读页码必须满足 1 ≤ 起始页 ≤ 结束页 ≤ 总页数。");
        if (record.Minutes <= 0) throw new ArgumentException("阅读分钟数必须为正整数。");
        if (record.ReadingDate.Date > DateTime.Today) throw new ArgumentException("阅读日期不能晚于今天。");
    }
    public void ValidateQuote(Quote quote, Book book)
    {
        if (quote.BookId != book.Id) throw new ArgumentException("书摘的书籍关联无效。");
        if (string.IsNullOrWhiteSpace(quote.Text)) throw new ArgumentException("请填写书摘正文。");
        if (quote.Revision < 0) throw new ArgumentException("书摘版本无效。");
        if (book.Kind == BookKind.Ebook && quote.Page.HasValue) throw new ArgumentException("电子书书摘不能填写物理页码。");
        if (quote.Page is int page && (page < 1 || page > book.TotalPages)) throw new ArgumentException("书摘页码必须在 1 到总页数之间，或留空。");
    }
    public BookProgress Calculate(Book book, IEnumerable<ReadingRecord> records)
    {
        if (book.Kind != BookKind.Manual) throw new ArgumentException("电子书进度须经统一状态计算器，不能使用手动页码计算。");
        return _state.Calculate(book, records.ToArray(), null);
    }
}
