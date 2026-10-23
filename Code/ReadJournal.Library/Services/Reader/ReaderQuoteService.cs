using System.Security.Cryptography;
using System.Text;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Reader;

public class ReaderQuoteCheckpoints { public virtual void Checkpoint(string point) { } }
public sealed class ReaderQuoteService(IDatabaseService database, IApplicationDataCoordinator data,
    IReaderDocumentService documents, IReaderSessionService sessions, IReadingService reading, ReaderQuoteCheckpoints checkpoints) : IReaderQuoteService
{
    public async Task<int> SaveSelectionAsync(ReaderContext context, DocumentRange range, string thoughts, CancellationToken ct)
    {
        ReaderContractValidator.Validate(range);
        if (range.Start.Document != context.Identity.Document) throw new ArgumentException("选区正文身份不一致。");
        await using var action = await sessions.EnterActiveActionAsync(context, ct);
        await using var lease = await data.EnterOperationAsync(context.Identity.Epoch, ct);
        var snapshot = await documents.ExtractAsync(range, ct);
        if (!await IsPureRangeAsync(range, snapshot, ct) || string.IsNullOrWhiteSpace(string.Concat(snapshot.Spans.Select(s => s.Text))))
            throw new ArgumentException("请选择连续纯文本；图片不作为原文保存，跨图片请分别选择。");
        var original = string.Concat(snapshot.Spans.Select(s => s.Text));
        var dedupe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.Identity.BookUid.ToString("D") + "\n" + ReaderContractValidator.WritePosition(range.Start) + "\n" + ReaderContractValidator.WritePosition(range.End)))).ToLowerInvariant();
        return await database.WriteAsync(lease, c =>
        {
            ct.ThrowIfCancellationRequested(); var book = c.Find<Book>(context.Identity.BookId); var file = c.Find<EbookFile>(context.Identity.BookId);
            if (book?.BookUid != context.Identity.BookUid || file is null || new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, file.ParserVersion) != range.Start.Document) throw new InvalidOperationException("StaleContext：书籍正文版本已改变。");
            var old = c.Table<QuoteOrigin>().FirstOrDefault(o => o.DedupeKey == dedupe); if (old is not null) return old.QuoteId;
            var now = DateTime.Now; var quote = new Quote { BookId = book.Id, Text = original, Thoughts = thoughts, Page = null, CreatedAt = now, UpdatedAt = now };
            reading.ValidateQuote(quote, book); c.Insert(quote); checkpoints.Checkpoint("quote.after-insert");
            c.Insert(new QuoteOrigin { QuoteId = quote.Id, Source = QuoteSource.ReaderSelection, OriginalText = original, DocumentKeyJson = ReaderContractValidator.WriteDocument(range.Start.Document), StartPositionJson = ReaderContractValidator.WritePosition(range.Start), EndPositionJson = ReaderContractValidator.WritePosition(range.End), DedupeKey = dedupe });
            checkpoints.Checkpoint("quote.after-origin"); return quote.Id;
        });
    }
    private async Task<bool> IsPureRangeAsync(DocumentRange range, TextSnapshot text, CancellationToken ct)
    {
        if (text.Spans.Count == 0 || text.Spans[0].Range.Start != range.Start || text.Spans[^1].Range.End != range.End || text.Spans.Any(s => s.Text.Contains('\ufffc'))) return false;
        var document = await documents.DescribeAsync(range.Start.Document, ct);
        var chapters = document.Chapters.SkipWhile(c => c.Key != range.Start.ChapterKey).TakeWhile(c => c.Order <= document.Chapters.Single(t => t.Key == range.End.ChapterKey).Order).ToArray();
        var expected = chapters.Sum(c => (long)c.Utf16Length) - range.Start.Utf16Offset - (chapters[^1].Utf16Length - range.End.Utf16Offset);
        return text.Spans.Sum(s => (long)s.Text.Length) == expected;
    }
    public async Task<PositionResolution> ResolveQuoteAsync(int quoteId, CancellationToken ct)
    {
        await database.InitializeAsync(); var epoch = data.CurrentEpoch; await using var lease = await data.EnterOperationAsync(epoch, ct);
        var info = await database.ReadAsync(lease, c => { var q = c.Find<Quote>(quoteId); return (Quote: q, Origin: c.Find<QuoteOrigin>(quoteId), Book: q is null ? null : c.Find<Book>(q.BookId), File: q is null ? null : c.Find<EbookFile>(q.BookId)); });
        if (info.Quote is null) return new(false, null, "书摘已被删除。");
        if (info.Origin?.StartPositionJson is null || info.Origin.EndPositionJson is null || info.Origin.DocumentKeyJson is null) return new(false, null, "此书摘没有正文定位来源。");
        var key = ReaderContractValidator.ReadDocument(info.Origin.DocumentKeyJson);
        if (info.Book is null || info.File is not { Status: 0 } file) return new(false, null, "原文缺失，请先修复书籍文件。");
        if (key != new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, file.ParserVersion)) return new(false, null, "正文版本不匹配，原书摘仍保留，无法精确回跳。");
        await documents.OpenAsync(new(info.Book.Id, info.Book.BookUid, key, epoch, Guid.NewGuid(), 0), ct);
        var range = new DocumentRange(ReaderContractValidator.ReadPosition(info.Origin.StartPositionJson), ReaderContractValidator.ReadPosition(info.Origin.EndPositionJson));
        try
        {
            var text = await documents.ExtractAsync(range, ct);
            return await IsPureRangeAsync(range, text, ct) && string.Concat(text.Spans.Select(s => s.Text)) == info.Origin.OriginalText
                ? new(true, range.Start, null) : new(false, null, "原文校验不一致，无法精确回跳。");
        }
        catch (ArgumentException ex) { return new(false, null, ex.Message); }
    }
}
