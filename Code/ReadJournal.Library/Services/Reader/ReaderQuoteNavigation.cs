using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Reader;
public sealed class ReaderQuoteNavigation(IReaderQuoteService quotes, IDatabaseService database, IContentNavigationService navigation)
{
    public Task<QuoteOrigin?> GetOriginAsync(int quoteId) => database.ReadAsync(c => (QuoteOrigin?)c.Find<QuoteOrigin>(quoteId));
    public async Task<bool> OpenAsync(int quoteId, CancellationToken ct = default)
    {
        var resolution = await quotes.ResolveQuoteAsync(quoteId, ct);
        if (!resolution.IsExact) throw new InvalidOperationException(resolution.Reason ?? "书摘无法定位。");
        var row = await database.ReadAsync(c => (Quote: c.Find<ReadJournal.Library.Models.Quote>(quoteId), Origin: c.Find<QuoteOrigin>(quoteId)));
        if (row.Quote is null || row.Origin?.EndPositionJson is null) throw new InvalidOperationException("书摘定位来源已改变，请刷新。");
        var target = new DocumentRange(resolution.Position!, ReaderContractValidator.ReadPosition(row.Origin.EndPositionJson));
        return await navigation.NavigateToAsync(ContentNavigationConstant.ReaderView, new ReaderOpenRequest(row.Quote.BookId, target), ct);
    }
}
