using System.Globalization;
using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Backup;

public sealed class BackupV2Validator
{
    private readonly IReadingService _reading;
    private readonly IReadingStateCalculator _state;
    public BackupV2Validator(IReadingService reading, IReadingStateCalculator state) { _reading = reading; _state = state; }
    public BackupDataV2 Validate(BackupDataV2 input)
    {
        var data = BackupDataStore.Normalize(input);
        if (data.FormatVersion != 2) Fail("不支持的业务格式。");
        Unique(data.Books.Select(x => x.Book.Id)); Unique(data.Books.Select(x => x.BookUid)); Unique(data.Quotes.Select(x => x.Quote.Id));
        Unique(data.ReadingRecords.Select(x => x.Id)); Unique(data.ReadingPlans.Select(x => x.Id)); Unique(data.Files.Select(x => x.BookId));
        Unique(data.Files.Select(x => x.FileId)); Unique(data.Files.Select(x => x.SourceSha256)); Unique(data.Progress.Select(x => x.BookId));
        Unique(data.Sessions.Select(x => x.SessionId)); Unique(data.Origins.Select(x => x.QuoteId)); Unique(data.Batches.Select(x => x.BatchId));
        Unique(data.BatchItems.Select(x => (x.BatchId, x.QuoteId)));
        var books = data.Books.ToDictionary(x => x.Book.Id, x => x.Book); var quotes = data.Quotes.ToDictionary(x => x.Quote.Id, x => x.Quote);
        var files = data.Files.ToDictionary(x => x.BookId); var batches = data.Batches.ToDictionary(x => x.BatchId);
        Book Parent(int id) => books.TryGetValue(id, out var b) ? b : throw new ArgumentException("InvalidFormat：孤立书籍关联。");
        DocumentKey Document(int id)
        { if (!files.TryGetValue(id, out var f)) throw new ArgumentException("InvalidFormat：缺少原书文件。"); return new(f.FileId, f.SourceSha256, f.CanonicalSha256, f.ParserVersion); }
        foreach (var b in books.Values)
        {
            if (b.Id <= 0 || b.BookUid == Guid.Empty || !Enum.IsDefined(b.Kind) || b.Author is null || b.Category is null || b.Description is null || b.CreatedAt == default || b.UpdatedAt < b.CreatedAt) Fail("书籍字段无效。");
            _reading.ValidateBook(b, 0);
            if ((b.Kind == BookKind.Ebook) != files.ContainsKey(b.Id)) Fail("书籍种类与文件不一致。");
        }
        foreach (var r in data.ReadingRecords) { if (r.Id <= 0) Fail("记录ID无效。"); _reading.ValidateRecord(r, Parent(r.BookId)); }
        foreach (var q in quotes.Values)
        { if (q.Id <= 0 || q.Revision < 0 || q.Thoughts is null || q.CreatedAt == default || q.UpdatedAt < q.CreatedAt) Fail("书摘字段无效。"); _reading.ValidateQuote(q, Parent(q.BookId)); }
        foreach (var f in files.Values)
        {
            if (Parent(f.BookId).Kind != BookKind.Ebook || f.FileId == Guid.Empty || !Enum.IsDefined(f.Format) || f.SourceBytes < 0 || f.SourceBytes > 104857600 || f.Status is < 0 or > 2 || f.ImportedUtcTicks <= 0) Fail("原书字段无效。");
            ReaderContractValidator.Validate(Document(f.BookId)); ValidateTicks(f.ImportedUtcTicks);
            var prefix = ManagedBookFiles.BookPrefix(f.FileId);
            if (f.SourceRelativePath != prefix + (f.Format == EbookFormat.Txt ? "source.txt" : "source.epub") || f.CanonicalRelativePath != prefix + "document-v1.json") Fail("原书路径与身份不一致。");
        }
        foreach (var p in data.Progress)
        {
            Parent(p.BookId); BackupEntityValidator.ValidateProgress(p);
            if (ReaderContractValidator.ReadDocument(p.DocumentKeyJson) != Document(p.BookId) || (!p.HasStarted && p.CurrentPercent != 0)) Fail("正文进度身份无效。");
            ValidateTicks(p.LastOpenedUtcTicks); ValidateTicks(p.LastSavedUtcTicks);
        }
        foreach (var s in data.Sessions)
        {
            var b = Parent(s.BookId); var key = Document(s.BookId);
            if (s.SessionId == Guid.Empty || s.BookUid != b.BookUid || s.DurationMilliseconds < 0 ||
                !ValidPercent(s.StartPercent) || !ValidPercent(s.EndPercent) || Math.Abs((long)s.OpenedOffsetMinutes) > 840 || Math.Abs((long)s.ClosedOffsetMinutes) > 840) Fail("会话字段无效。");
            ValidateTicks(s.OpenedUtcTicks); ValidateTicks(s.ClosedUtcTicks);
            if (ReaderContractValidator.ReadDocument(s.DocumentKeyJson) != key || ReaderContractValidator.ReadPosition(s.StartPositionJson).Document != key || ReaderContractValidator.ReadPosition(s.EndPositionJson).Document != key) Fail("会话文档不一致。");
            var closed = new DateTimeOffset(s.ClosedUtcTicks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(s.ClosedOffsetMinutes));
            if (s.CloseLocalDate != closed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) || (s.ClosedUtcTicks < s.OpenedUtcTicks && !s.ClockAnomaly)) Fail("会话时间无效。");
        }
        foreach (var p in data.ReadingPlans)
        { Parent(p.BookId); if (p.Id <= 0 || p.StartDate == default || p.StartDate.Date > DateTime.Today || p.TargetDate.Date < p.StartDate.Date) Fail("计划字段无效。"); }
        foreach (var group in data.ReadingPlans.GroupBy(p => p.BookId))
        {
            var b = Parent(group.Key); var p = data.Progress.SingleOrDefault(p => p.BookId == b.Id); var sessions = data.Sessions.Where(s => s.BookId == b.Id).ToArray();
            var completed = p?.CompletedUtcTicks is long ticks ? new DateTimeOffset(ticks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(p.CompletedOffsetMinutes!.Value)) : (DateTimeOffset?)null;
            if (group.Count() > 1 && _state.Calculate(b, data.ReadingRecords, new(p?.HasStarted ?? false, completed, p?.CurrentPercent ?? 0, sessions.Sum(s => s.DurationMilliseconds), sessions.Length)).State != "已完成") Fail("多个历史计划不能同时未完成。");
        }
        foreach (var batch in batches.Values)
        {
            if (batch.BatchId == Guid.Empty || batch.BookUid != Parent(batch.BookId).BookUid || ReaderContractValidator.ReadDocument(batch.DocumentKeyJson) != Document(batch.BookId) ||
                !ReaderContractValidator.IsSha256(batch.ScopeSha256) || batch.Status is < 0 or > 1 || batch.InsertedCount < 0 || batch.DuplicateCount < 0 || batch.RejectedCount < 0) Fail("AI批次字段无效。");
            ValidateTicks(batch.CreatedUtcTicks);
            var ids = JsonSerializer.Deserialize<int[]>(batch.InsertedQuoteIdsJson) ?? throw new ArgumentException("InvalidFormat：批次回执为空。");
            if (ids.Length != batch.InsertedCount || ids.Distinct().Count() != ids.Length || ids.Any(x => x <= 0)) Fail("批次回执与计数不一致。");
        }
        foreach (var o in data.Origins)
        {
            if (!quotes.TryGetValue(o.QuoteId, out var q) || !Enum.IsDefined(o.Source) || o.OriginalText is null || o.LocationStatus is < 0 or > 2) Fail("书摘来源字段无效。");
            var hasPosition = o.DocumentKeyJson is not null;
            if (hasPosition != (o.StartPositionJson is not null) || hasPosition != (o.EndPositionJson is not null) || hasPosition != (o.DedupeKey is not null) || (o.Source != QuoteSource.Manual && !hasPosition)) Fail("书摘定位字段缺失。");
            if (hasPosition)
            {
                if (Parent(q!.BookId).Kind != BookKind.Ebook || !ReaderContractValidator.IsSha256(o.DedupeKey)) Fail("书摘定位关联无效。");
                var key = ReaderContractValidator.ReadDocument(o.DocumentKeyJson!); var start = ReaderContractValidator.ReadPosition(o.StartPositionJson!); var end = ReaderContractValidator.ReadPosition(o.EndPositionJson!);
                ReaderContractValidator.Validate(new DocumentRange(start, end));
                if (key != start.Document || key != end.Document || (o.LocationStatus == 0 && key != Document(q.BookId))) Fail("书摘定位身份不一致。");
            }
            if (o.PrefixHash is not null && !ReaderContractValidator.IsSha256(o.PrefixHash) || o.SuffixHash is not null && !ReaderContractValidator.IsSha256(o.SuffixHash)) Fail("上下文摘要无效。");
            if (o.BatchId is Guid batchId && (!batches.TryGetValue(batchId, out var batch) || batch.BookId != q!.BookId)) Fail("书摘批次关联无效。");
        }
        Unique(data.Origins.Where(o => o.DedupeKey is not null).Select(o => o.DedupeKey));
        foreach (var item in data.BatchItems)
        {
            if (!batches.TryGetValue(item.BatchId, out var batch) || !quotes.TryGetValue(item.QuoteId, out var quote) || quote.BookId != batch.BookId || item.CreatedRevision < 0 || item.CreatedRevision > quote.Revision || !ReaderContractValidator.IsSha256(item.CreatedContentHash)) Fail("批次明细关联无效。");
            if (!JsonSerializer.Deserialize<int[]>(batch!.InsertedQuoteIdsJson)!.Contains(item.QuoteId)) Fail("批次明细不属于原提交回执。");
        }
        return data;
    }
    private static bool ValidPercent(double value) => double.IsFinite(value) && value >= 0 && value <= 100;
    private static void ValidateTicks(long ticks) { if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks) Fail("UTC时间无效。"); }
    private static void Unique<T>(IEnumerable<T> values) { var all = values.ToArray(); if (all.Any(x => x is null) || all.Distinct().Count() != all.Length) Fail("实体包含空值或重复身份。"); }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn] private static void Fail(string reason) => throw new ArgumentException("InvalidFormat：" + reason);
}
