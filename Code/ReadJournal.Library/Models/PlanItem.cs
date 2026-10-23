using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Models;
public class PlanItem
{
    public ReadingPlan Plan { get; init; } = new();
    public BookProgress Progress { get; init; } = new();
    public int RemainingPages => Math.Max(0, Progress.Book.TotalPages - Progress.CurrentPage);
    public string State => Progress.State == "已完成" ? "已完成" : DateTime.Today > Plan.TargetDate.Date ? "已逾期" : "进行中";
    public int SuggestedDailyPages => RemainingPages == 0 || State == "已逾期" ? 0 : (int)Math.Ceiling(RemainingPages / (Plan.TargetDate.Date - DateTime.Today).Add(TimeSpan.FromDays(1)).TotalDays);
    public double RemainingPercent => Math.Max(0, 100 - Progress.Percent);
    public double SuggestedDailyPercent => State is "已完成" or "已逾期" ? 0 : RemainingPercent / Math.Max(1, (Plan.TargetDate.Date - DateTime.Today).TotalDays + 1);
    public string Suggestion => Progress.Book.Kind == BookKind.Ebook
        ? State == "已完成" ? "已完成 · 建议每日 0%" : State == "已逾期" ? $"已逾期 · 距正文末尾 {RemainingPercent:0.#}%" : $"建议每日 {SuggestedDailyPercent:0.#}% · 距正文末尾 {RemainingPercent:0.#}%"
        : State == "已完成" ? "已完成 · 建议每日 0 页" : State == "已逾期" ? $"已逾期 · 剩余 {RemainingPages} 页" : $"建议每日 {SuggestedDailyPages} 页 · 剩余 {RemainingPages} 页";
    public string Deadline => $"目标日期 {Plan.TargetDate:yyyy-MM-dd}";
}
