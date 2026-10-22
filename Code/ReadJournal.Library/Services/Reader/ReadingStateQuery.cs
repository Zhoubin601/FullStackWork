using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using SQLite;
namespace ReadJournal.Library.Services.Reader;
// Used only inside an existing SQL delegate: no async storage calls or nested database gates.
internal sealed class ReadingStateQuery
{
    private readonly Dictionary<int, EbookProgress> _progress;
    private readonly Dictionary<int, (long Milliseconds, int Count)> _sessions;
    private readonly IReadOnlyList<ReadingRecord> _records;
    private readonly Dictionary<int, EbookFile> _files;
    private readonly IReadingStateCalculator _calculator;
    internal ReadingStateQuery(SQLiteConnection c, IReadingStateCalculator calculator)
    {
        _calculator = calculator; _records = c.Table<ReadingRecord>().ToList();
        _files = c.Table<EbookFile>().ToDictionary(f => f.BookId);
        _progress = c.Table<EbookProgress>().ToDictionary(x => x.BookId);
        _sessions = c.Table<EbookReadingSession>().ToList().GroupBy(x => x.BookId)
            .ToDictionary(g => g.Key, g => (g.Sum(x => x.DurationMilliseconds), g.Count()));
    }
    internal BookProgress Calculate(Book book)
    {
        _progress.TryGetValue(book.Id, out var row); _sessions.TryGetValue(book.Id, out var activity);
        var completed = row?.CompletedUtcTicks is long ticks
            ? new DateTimeOffset(ticks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(row.CompletedOffsetMinutes ?? 0)) : (DateTimeOffset?)null;
        var result = _calculator.Calculate(book, _records, new EbookStateInput(row?.HasStarted ?? false,
            completed, row?.CurrentPercent ?? 0, activity.Milliseconds, activity.Count));
        if (book.Kind == BookKind.Ebook) result.FileStatus = _files.TryGetValue(book.Id, out var file) ? file.Status : (int)EbookFileStatus.Missing;
        return result;
    }
}
