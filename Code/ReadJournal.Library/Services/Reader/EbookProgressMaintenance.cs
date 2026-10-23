using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Reader;

public interface IEbookProgressMaintenance
{
    Task ResetAsync(int bookId, Guid bookUid, DataEpoch expectedEpoch, CancellationToken ct);
}
public sealed class EbookProgressMaintenance : IEbookProgressMaintenance
{
    private readonly IDatabaseService _database;
    private readonly IApplicationDataCoordinator _coordinator;
    public EbookProgressMaintenance(IDatabaseService database, IApplicationDataCoordinator coordinator)
    { _database = database; _coordinator = coordinator; }
    public async Task ResetAsync(int bookId, Guid bookUid, DataEpoch expectedEpoch, CancellationToken ct)
    {
        await _database.InitializeAsync();
        await using var lease = await _coordinator.EnterMaintenanceAsync(MaintenanceKind.Reset, ct);
        if (lease.Epoch != expectedEpoch) throw new InvalidOperationException("StaleContext：数据集已改变。");
        ct.ThrowIfCancellationRequested();
        await _database.WriteAsync(lease, c =>
        {
            ct.ThrowIfCancellationRequested();
            var book = c.Find<Book>(bookId) ?? throw new ArgumentException("书籍不存在。");
            if (book.BookUid != bookUid) throw new InvalidOperationException("StaleContext：书籍身份已改变。");
            ReadingStateCalculator.ValidateReset(book, c.Table<ReadingPlan>().Count(x => x.BookId == bookId));
            c.Delete<EbookProgress>(bookId); // Closed sessions remain historical activity.
            return 0;
        });
        // Commit first, then invalidate stale work. Cancellation after commit cannot report an uncommitted reset.
        lease.CompleteReplacement();
    }
}
