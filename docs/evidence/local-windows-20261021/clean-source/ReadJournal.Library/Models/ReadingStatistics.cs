namespace ReadJournal.Library.Models;
public class ReadingStatistics
{
    public long TotalPages { get; init; }
    public long TotalMinutes { get; init; }
    public int CompletedBooks { get; init; }
    public IReadOnlyList<DayActivity> LastSevenDays { get; init; } = [];
    public string Summary => $"累计阅读页数 {TotalPages} 页 · 累计 {TotalMinutes} 分钟 · 完成 {CompletedBooks} 本书";
}
