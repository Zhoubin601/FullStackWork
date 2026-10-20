using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public interface IReadingService
{
    void ValidateBook(Book book, int maximumReferencedPage);
    void ValidateRecord(ReadingRecord record, Book book);
    void ValidateQuote(Quote quote, Book book);
    BookProgress Calculate(Book book, IEnumerable<ReadingRecord> records);
}
