using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services;
public interface IQuoteStorage
{
    Task<QuoteBatchResult> SaveAiBatchAsync(VerifiedAiBatchCommand command, CancellationToken ct);
    Task<UndoBatchResult> UndoAiBatchAsync(Guid batchId, DataEpoch expectedEpoch, CancellationToken ct);
    Task<Quote?> GetAsync(int id);
    Task<IReadOnlyList<QuoteItem>> SearchAsync(string query, bool favoritesOnly, int? bookId = null);
    Task<int> SaveAsync(Quote quote);
    Task ToggleFavoriteAsync(int id);
    Task DeleteAsync(int id);
}
