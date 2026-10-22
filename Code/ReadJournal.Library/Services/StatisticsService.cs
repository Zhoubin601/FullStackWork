using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;
public class StatisticsService : IStatisticsService
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading;
    private readonly IReadingStateCalculator _state;
    public StatisticsService(IDatabaseService database, IReadingService reading) : this(database, reading, new ReadingStateCalculator()) { }
    public StatisticsService(IDatabaseService database, IReadingService reading, IReadingStateCalculator state) { _database = database; _reading = reading; _state = state; }
    public Task<ReadingStatistics> GetAsync() => _database.ReadAsync(c =>
    {
        var records = c.Table<ReadingRecord>().ToList(); var books = c.Table<Book>().ToList();
        var states = new ReadingStateQuery(c, _state);
        var sessions = c.Table<EbookReadingSession>().ToList();
        var totalMs = checked(records.Sum(x => (long)x.Minutes * 60000) + sessions.Sum(x => x.DurationMilliseconds));
        var recent = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).Select(date =>
        {
            var manual = records.Where(x => x.ReadingDate.Date == date).ToArray();
            var ebook = sessions.Where(x => x.CloseLocalDate == date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var milliseconds = checked(manual.Sum(x => (long)x.Minutes * 60000) + ebook.Sum(x => x.DurationMilliseconds));
            return new DayActivity { Date = date, Pages = manual.Sum(x => (long)x.Pages), Minutes = milliseconds / 60000,
                DurationMilliseconds = milliseconds, SessionCount = ebook.Length };
        }).ToArray();
        var max = Math.Max(1, recent.Max(x => x.Pages));
        return new ReadingStatistics { TotalPages = records.Sum(x => (long)x.Pages), TotalMinutes = totalMs / 60000,
            DurationMilliseconds = totalMs, SessionCount = sessions.Count,
            CompletedBooks = books.Count(x => states.Calculate(x).State == "已完成"), LastSevenDays = recent.Select(x => new DayActivity
            { Date = x.Date, Pages = x.Pages, Minutes = x.Minutes, DurationMilliseconds = x.DurationMilliseconds,
                SessionCount = x.SessionCount, BarValue = x.Pages * 100.0 / max }).ToArray() };
    });
}
