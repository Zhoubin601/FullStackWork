using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Models;
public class BookProgress
{
    public Book Book { get; init; } = new();
    public int CurrentPage { get; init; }
    public long TotalReadPages { get; init; }
    public long TotalMinutes { get; init; }
    public DateTime? CompletedOn { get; init; }
    public bool HasStarted { get; init; }
    public DateTimeOffset? EbookCompletedAt { get; init; }
    public double CurrentPercent { get; init; }
    public long DurationMilliseconds { get; init; }
    public int SessionCount { get; init; }
    public string State => Book.Kind == BookKind.Ebook
        ? EbookCompletedAt.HasValue ? "已完成" : HasStarted ? "在读" : "未开始"
        : CurrentPage == 0 ? "未开始" : CurrentPage >= Book.TotalPages ? "已完成" : "在读";
    public double Percent => Book.Kind == BookKind.Ebook ? CurrentPercent : Book.TotalPages > 0 ? CurrentPage * 100.0 / Book.TotalPages : 0;
    public string ProgressText => Book.Kind == BookKind.Ebook ? $"{State} · 正文位置 {Percent:0.#}%" : $"{State} · 当前阅读进度 {CurrentPage}/{Book.TotalPages} 页（{Percent:0.#}%）";
    public string ActivityText => Book.Kind == BookKind.Ebook ? $"{SessionCount} 次阅读 · 累计 {DurationMilliseconds / 60000.0:0.#} 分钟" : $"累计阅读页数 {TotalReadPages} 页 · 累计 {TotalMinutes} 分钟（含重读）";
}
