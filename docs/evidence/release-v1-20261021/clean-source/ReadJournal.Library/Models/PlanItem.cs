namespace ReadJournal.Library.Models;
public class PlanItem
{
    public ReadingPlan Plan { get; init; } = new();
    public BookProgress Progress { get; init; } = new();
    public int RemainingPages => Math.Max(0, Progress.Book.TotalPages - Progress.CurrentPage);
    public string State => RemainingPages == 0 ? "已完成" : DateTime.Today > Plan.TargetDate.Date ? "已逾期" : "进行中";
    public int SuggestedDailyPages => RemainingPages == 0 || State == "已逾期" ? 0 : (int)Math.Ceiling(RemainingPages / (Plan.TargetDate.Date - DateTime.Today).Add(TimeSpan.FromDays(1)).TotalDays);
    public string Suggestion => State == "已完成" ? "已完成 · 建议每日 0 页" : State == "已逾期" ? $"已逾期 · 剩余 {RemainingPages} 页" : $"建议每日 {SuggestedDailyPages} 页 · 剩余 {RemainingPages} 页";
    public string Deadline => $"目标日期 {Plan.TargetDate:yyyy-MM-dd}";
}
