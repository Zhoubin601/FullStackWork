using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Models;
public class BookProgress
{
    public Book Book { get; init; } = new();
    public int FileStatus { get; set; }
    public int CurrentPage { get; init; }
    public long TotalReadPages { get; init; }
    public long TotalMinutes { get; init; }
    public DateTime? CompletedOn { get; init; }
    public bool HasStarted { get; init; }
    public DateTimeOffset? EbookCompletedAt { get; init; }
    public double CurrentPercent { get; init; }
    public long DurationMilliseconds { get; init; }
    public int SessionCount { get; init; }
    public string KindLabel => Book.Kind == BookKind.Ebook ? "电子书" : "纸质/手动";
    public string State => Book.Kind == BookKind.Ebook
        ? EbookCompletedAt.HasValue ? "已完成" : HasStarted ? "在读" : "未开始"
        : CurrentPage == 0 ? "未开始" : CurrentPage >= Book.TotalPages ? "已完成" : "在读";
    public double Percent => Book.Kind == BookKind.Ebook ? CurrentPercent : Book.TotalPages > 0 ? CurrentPage * 100.0 / Book.TotalPages : 0;
    public string ProgressText => Book.Kind == BookKind.Ebook ? FileStatus == (int)EbookFileStatus.Missing ? "Missing：受管原文缺失。请补回文件，或删除该条目后恢复备份/重新导入。" : FileStatus == (int)EbookFileStatus.Invalid ? "原文校验失败，暂不可阅读。请补回文件，或删除该条目后恢复备份。" : $"{State} · 正文位置 {Percent:0.#}%" : $"{State} · 当前阅读进度 {CurrentPage}/{Book.TotalPages} 页（{Percent:0.#}%）";
    public string ActivityText => Book.Kind == BookKind.Ebook ? $"{SessionCount} 次阅读 · 累计 {DurationMilliseconds / 60000.0:0.#} 分钟" : $"累计阅读页数 {TotalReadPages} 页 · 累计 {TotalMinutes} 分钟（含重读）";
}
