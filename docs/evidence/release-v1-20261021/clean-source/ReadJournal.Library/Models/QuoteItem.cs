namespace ReadJournal.Library.Models;
public class QuoteItem
{
    public Quote Quote { get; init; } = new();
    public string BookTitle { get; init; } = "";
    public string Location => $"《{BookTitle}》" + (Quote.Page is int page ? $" · 第 {page} 页" : "");
}
