using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public interface IStatisticsService { Task<ReadingStatistics> GetAsync(); }
