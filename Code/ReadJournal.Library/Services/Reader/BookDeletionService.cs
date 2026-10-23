using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.Library.Services.Reader;

public sealed class BookDeletionService(IDatabaseService database, ApplicationDataCoordinator coordinator,
    ManagedBookFiles files, BookFileOperations operations, IEnumerable<IReaderDocumentService> documents, IEnumerable<IReaderLayoutService> layouts)
{
    public string? LastWarning { get; private set; }
    public async Task<DeleteImpact> GetImpactAsync(int bookId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var impact = await database.ReadAsync(c => new { File = c.Find<EbookFile>(bookId), Records = c.Table<ReadingRecord>().Count(r => r.BookId == bookId), Sessions = c.Table<EbookReadingSession>().Count(s => s.BookId == bookId), Quotes = c.Table<Quote>().Count(q => q.BookId == bookId), Plans = c.Table<ReadingPlan>().Count(p => p.BookId == bookId) });
        var bytes = impact.File is null ? 0 : await Task.Run(() => operations.PhysicalFiles(impact.File.FileId).Sum(f => f.Bytes), ct);
        return new(impact.Records, impact.Sessions, impact.Quotes, impact.Plans, bytes);
    }
    public async Task DeleteAsync(int id, Guid uid, DataEpoch epoch, CancellationToken ct)
    {
        await database.InitializeAsync(); await using var lease = await coordinator.EnterMaintenanceAsync(MaintenanceKind.Delete, ct);
        if (lease.Epoch != epoch) throw new InvalidOperationException("StaleContext：数据集已改变。");
        LastWarning = null;
        var target = await database.ReadAsync(lease, c => new { Book = c.Find<Book>(id), File = c.Find<EbookFile>(id) });
        if (target.Book?.BookUid != uid) throw new InvalidOperationException("StaleContext：书籍已删除或身份已改变。");
        var log = new BookFileJournal(1, Guid.NewGuid(), "delete", "Prepared", target.File?.FileId ?? Guid.Empty, id, uid, target.File?.SourceSha256,
            target.File is null ? [] : await Task.Run(() => operations.PhysicalFiles(target.File.FileId), ct));
        operations.Write(log); var committed = false;
        try
        {
            ct.ThrowIfCancellationRequested(); files.Checkpoint("delete.before-transaction");
            await database.WriteAsync(lease, c => { ct.ThrowIfCancellationRequested(); if (c.Find<Book>(id)?.BookUid != uid) throw new InvalidOperationException("StaleContext：书籍身份已改变。"); c.Delete<Book>(id); files.Checkpoint("delete.in-transaction"); return 0; });
            committed = true; files.Checkpoint("delete.after-commit");
            await database.ReadAsync(lease, c => { operations.Recover(c, log); return 0; });
        }
        catch (Exception ex)
        {
            try { await database.ReadAsync(lease, c => { operations.Recover(c, log); return 0; }); }
            catch (RecoveryCleanupException) { LastWarning = "删除已提交；文件清理未完成，下次启动/刷新会重试。"; }
            catch (Exception)
            { coordinator.RequireRecovery("RecoveryRequired：删除对账未完成，请保留operations/trash并重启。"); LastWarning = "RecoveryRequired：请保留恢复材料并重启。"; }
            if (!committed) throw;
            LastWarning ??= "删除已提交；收尾检查已完成。";
            System.Diagnostics.Trace.WriteLine("Delete cleanup: " + ex.GetType().Name);
        }
        if (target.File is not null)
        {
            var key = new DocumentKey(target.File.FileId, target.File.SourceSha256, target.File.CanonicalSha256, target.File.ParserVersion);
            try { foreach (var document in documents) await document.ReleaseAsync(key); foreach (var layout in layouts) await layout.ReleaseAllAsync(); }
            catch (Exception) { coordinator.RequireRecovery("RecoveryRequired：已删除，需重启释放旧文档缓存。"); LastWarning = "删除已提交，请重启清理旧文档。"; }
        }
    }
}
