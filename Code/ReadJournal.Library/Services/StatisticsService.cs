using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public class StatisticsService : IStatisticsService
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading;
    public StatisticsService(IDatabaseService database, IReadingService reading) { _database = database; _reading = reading; }
    public Task<ReadingStatistics> GetAsync() => _database.ReadAsync(c =>
    {
        var records = c.Table<ReadingRecord>().ToList(); var books = c.Table<Book>().ToList();
        var recent = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).Select(date => new DayActivity { Date = date, Pages = records.Where(x => x.ReadingDate.Date == date).Sum(x => (long)x.Pages), Minutes = records.Where(x => x.ReadingDate.Date == date).Sum(x => (long)x.Minutes) }).ToArray();
        var max = Math.Max(1, recent.Max(x => x.Pages));
        return new ReadingStatistics { TotalPages = records.Sum(x => (long)x.Pages), TotalMinutes = records.Sum(x => (long)x.Minutes), CompletedBooks = books.Count(x => _reading.Calculate(x, records).State == "已完成"), LastSevenDays = recent.Select(x => new DayActivity { Date = x.Date, Pages = x.Pages, Minutes = x.Minutes, BarValue = x.Pages * 100.0 / max }).ToArray() };
    });
}
