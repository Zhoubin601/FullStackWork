namespace ReadJournal.Library.Models;
public class QuoteItem
{
    public Quote Quote { get; init; } = new();
    public string BookTitle { get; init; } = "";
    public Reader.QuoteSource Source { get; init; }
    public Guid? BatchId { get; init; }
    public bool HasAiBatch => BatchId is not null;
    public bool CanOpenSource => Source != Reader.QuoteSource.Manual;
    public string SourceLabel => Source switch { Reader.QuoteSource.AiGenerated => "AI生成 · 解读需自行判断" + (Quote.Revision > 0 ? " · 已编辑/收藏变更" : ""), Reader.QuoteSource.ReaderSelection => "正文选区", _ => "手动书摘" };
    public string Location => $"《{BookTitle}》" + (Quote.Page is int page ? $" · 第 {page} 页" : "");
}
