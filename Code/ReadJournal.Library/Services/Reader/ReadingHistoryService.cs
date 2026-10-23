using System.Globalization;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Reader;
public sealed class ReadingHistoryService(IDatabaseService database, IApplicationDataCoordinator data, IReadingService reading) : IReadingHistoryService
{
    public async Task<PageResult<ReadingHistoryItem>> GetAsync(int? bookId, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 200) throw new ArgumentException("历史分页参数无效。");
        ct.ThrowIfCancellationRequested();
        return await database.ReadAsync(c =>
        {
            var books = c.Table<Book>().ToDictionary(b => b.Id);
            var manual = c.Table<ReadingRecord>().ToList().Where(r => (!bookId.HasValue || r.BookId == bookId) && books.ContainsKey(r.BookId)).Select(r =>
            {
                var b = books[r.BookId]; var date = DateTime.SpecifyKind(r.ReadingDate.Date, DateTimeKind.Local);
                return new ReadingHistoryItem(new(HistorySource.ManualRecord, r.Id.ToString(CultureInfo.InvariantCulture)), b.Id, b.BookUid, b.Title, new DateTimeOffset(date), DateOnly.FromDateTime(date), checked((long)r.Minutes * 60000), r.Pages, null, null, true, $"{r.ReadingDate:MM-dd} · 《{b.Title}》 · {r.Pages} 页 / {r.Minutes} 分钟");
            });
            var automatic = c.Table<EbookReadingSession>().ToList().Where(s => (!bookId.HasValue || s.BookId == bookId) && books.TryGetValue(s.BookId, out var b) && b.BookUid == s.BookUid).Select(s =>
                new ReadingHistoryItem(new(HistorySource.EbookSession, s.SessionId.ToString("D")), s.BookId, s.BookUid, books[s.BookId].Title, new DateTimeOffset(s.ClosedUtcTicks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(s.ClosedOffsetMinutes)), DateOnly.ParseExact(s.CloseLocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), s.DurationMilliseconds, null, s.StartPercent, s.EndPercent, false, $"《{books[s.BookId].Title}》 · {s.DisplayText}"));
            var all = manual.Concat(automatic).OrderByDescending(x => x.SortTime.UtcTicks).ThenBy(x => x.Key.Source).ThenByDescending(x => x.Key.Source == HistorySource.ManualRecord ? int.Parse(x.Key.SourceId, CultureInfo.InvariantCulture) : 0).ThenByDescending(x => x.Key.SourceId, StringComparer.Ordinal).ToArray();
            return new PageResult<ReadingHistoryItem> { TotalCount = all.Length, Items = all.Skip(checked((page - 1) * pageSize)).Take(pageSize).ToArray() };
        });
    }
    public async Task DeleteAsync(HistoryKey key, DataEpoch expectedEpoch, CancellationToken ct)
    {
        await database.InitializeAsync(); await using var lease = await data.EnterOperationAsync(expectedEpoch, ct);
        if (key.Source == HistorySource.EbookSession && Guid.TryParseExact(key.SourceId, "D", out var session))
        { await database.WriteAsync(lease, c => { ct.ThrowIfCancellationRequested(); return c.Delete<EbookReadingSession>(session); }); return; }
        if (key.Source == HistorySource.ManualRecord && int.TryParse(key.SourceId, out var id) && id > 0)
        {
            await database.WriteAsync(lease, c =>
            {
                ct.ThrowIfCancellationRequested(); var row = c.Find<ReadingRecord>(id); if (row is null) return 0;
                ReadingPlanConsistency.Validate(reading, c.Find<Book>(row.BookId)!, c.Table<ReadingRecord>().ToList().Where(r => r.Id != id), c.Table<ReadingPlan>().ToList());
                return c.Delete<ReadingRecord>(id);
            }); return;
        }
        throw new ArgumentException("历史记录身份无效。");
    }
}
