namespace ReadJournal.Library.Models;
public class DayActivity
{
    public DateTime Date { get; init; }
    public long Pages { get; init; }
    public long Minutes { get; init; }
    public long DurationMilliseconds { get; init; }
    public int SessionCount { get; init; }
    public double DurationBarValue { get; init; }
    public double BarValue { get; init; }
    public string Label => $"{Date:MM-dd} · {Pages} 页 / {DurationMilliseconds / 60000.0:0.###} 分钟";
}
