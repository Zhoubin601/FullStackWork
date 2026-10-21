namespace ReadJournal.Library.Models;
public class BookProgress
{
    public Book Book { get; init; } = new();
    public int CurrentPage { get; init; }
    public long TotalReadPages { get; init; }
    public long TotalMinutes { get; init; }
    public DateTime? CompletedOn { get; init; }
    public string State => CurrentPage == 0 ? "未开始" : CurrentPage >= Book.TotalPages ? "已完成" : "在读";
    public double Percent => Book.TotalPages > 0 ? CurrentPage * 100.0 / Book.TotalPages : 0;
    public string ProgressText => $"{State} · 当前阅读进度 {CurrentPage}/{Book.TotalPages} 页（{Percent:0.#}%）";
    public string ActivityText => $"累计阅读页数 {TotalReadPages} 页 · 累计 {TotalMinutes} 分钟（含重读）";
}
