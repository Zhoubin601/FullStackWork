using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Ai;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;

public partial class QuoteStorage
{
    private readonly IReaderSessionService? _sessions; private readonly IReaderDocumentService? _documents;
    private readonly IApplicationDataCoordinator? _data; private readonly AiBatchCheckpoints _checkpoints;
    public async Task<QuoteBatchResult> SaveAiBatchAsync(VerifiedAiBatchCommand command, CancellationToken ct)
    {
        if (_sessions is null || _documents is null || _data is null) throw new InvalidOperationException("AI存储尚未配置。");
        if (command.BatchId == Guid.Empty || command.RejectedCount < 0 || command.Quotes.Count > AiIntegrity.MaxCandidates || command.Scope is null ||
            command.Scope.Context != command.Context || command.Scope.ScopeSha256 != command.ScopeSha256) throw new AiException("InvalidBatch", "批次身份无效。");
        // Snapshot caller-owned collections before any await. Neither verification nor receipt can be changed mid-write.
        command = command with { Scope = AiIntegrity.Freeze(command.Scope), Quotes = Array.AsReadOnly(command.Quotes.ToArray()) };
        await using var action = await _sessions.EnterActiveActionAsync(command.Context, ct);
        await using var lease = await _data.EnterOperationAsync(command.Context.Identity.Epoch, ct);
        await AiIntegrity.ValidateOriginalAsync(command.Scope, _documents, ct);
        foreach (var quote in command.Quotes)
        {
            if (quote.Explanation is null || quote.Explanation.Length > 4000 || string.IsNullOrWhiteSpace(quote.OriginalText) || quote.OriginalText.Length > 8000 ||
                !command.Scope.Original.Spans.Any(span => span.Range.Start.ChapterKey == quote.Range.Start.ChapterKey && quote.Range.Start.ChapterKey == quote.Range.End.ChapterKey &&
                    quote.Range.Start.Document == span.Range.Start.Document && quote.Range.End.Document == span.Range.End.Document &&
                    quote.Range.Start.Utf16Offset >= span.Range.Start.Utf16Offset && quote.Range.End.Utf16Offset <= span.Range.End.Utf16Offset &&
                    quote.Range.End.Utf16Offset > quote.Range.Start.Utf16Offset &&
                    span.Text.Substring(quote.Range.Start.Utf16Offset - span.Range.Start.Utf16Offset, quote.Range.End.Utf16Offset - quote.Range.Start.Utf16Offset) == quote.OriginalText))
                throw new AiException("InvalidBatch", "候选原句不属于冻结范围。");
            var original = await _documents.ExtractAsync(quote.Range, ct);
            if (original.Spans.Count != 1 || original.Spans[0].Text != quote.OriginalText) throw new AiException("InvalidBatch", "候选原句与正文不一致。");
        }
        return await _database.WriteAsync(lease, c =>
        {
            ct.ThrowIfCancellationRequested(); var identity = command.Context.Identity;
            var book = c.Find<Book>(identity.BookId); var file = c.Find<EbookFile>(identity.BookId);
            if (book?.BookUid != identity.BookUid || file is not { Status: 0 } || new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, file.ParserVersion) != identity.Document)
                throw new AiException("StaleContext", "书籍或正文已改变，请重新打开。");
            var old = c.Find<AiGenerationBatch>(command.BatchId);
            if (old is not null)
            {
                if (old.BookUid != identity.BookUid || old.DocumentKeyJson != ReaderContractValidator.WriteDocument(identity.Document) || old.ScopeSha256 != command.ScopeSha256)
                    throw new AiException("InvalidBatch", "批次编号与原提交不一致。");
                return new QuoteBatchResult(old.BatchId, Array.AsReadOnly(JsonSerializer.Deserialize<int[]>(old.InsertedQuoteIdsJson)!), old.DuplicateCount, old.RejectedCount, old.Status == 1);
            }
            if (command.Quotes.Count == 0) return new(command.BatchId, [], 0, command.RejectedCount);
            var inserted = new List<(Quote Quote, VerifiedQuote Verified, string Key)>(); var keys = new HashSet<string>(); var duplicate = 0;
            foreach (var verified in command.Quotes)
            {
                ct.ThrowIfCancellationRequested(); var key = AiIntegrity.Dedupe(identity.BookUid, verified.Range);
                if (!keys.Add(key) || c.Table<QuoteOrigin>().FirstOrDefault(x => x.DedupeKey == key) is not null) { duplicate++; continue; }
                var now = DateTime.Now; var quote = new Quote { BookId = book.Id, Text = verified.OriginalText, Thoughts = verified.Explanation, CreatedAt = now, UpdatedAt = now };
                _reading.ValidateQuote(quote, book); c.Insert(quote); inserted.Add((quote, verified, key)); _checkpoints.Checkpoint("ai.after-quote");
            }
            var ids = inserted.Select(x => x.Quote.Id).ToArray();
            c.Insert(new AiGenerationBatch { BatchId = command.BatchId, BookId = book.Id, BookUid = book.BookUid, DocumentKeyJson = ReaderContractValidator.WriteDocument(identity.Document), ScopeSha256 = command.ScopeSha256,
                CreatedUtcTicks = DateTimeOffset.UtcNow.Ticks, InsertedCount = ids.Length, DuplicateCount = duplicate, RejectedCount = command.RejectedCount, InsertedQuoteIdsJson = JsonSerializer.Serialize(ids) });
            _checkpoints.Checkpoint("ai.after-receipt");
            foreach (var item in inserted)
            {
                var range = item.Verified.Range;
                c.Insert(new QuoteOrigin { QuoteId = item.Quote.Id, Source = QuoteSource.AiGenerated, OriginalText = item.Verified.OriginalText, DocumentKeyJson = ReaderContractValidator.WriteDocument(identity.Document), StartPositionJson = ReaderContractValidator.WritePosition(range.Start), EndPositionJson = ReaderContractValidator.WritePosition(range.End), BatchId = command.BatchId, DedupeKey = item.Key });
                c.Insert(new AiGenerationBatchItem { BatchId = command.BatchId, QuoteId = item.Quote.Id, CreatedRevision = item.Quote.Revision, CreatedContentHash = AiIntegrity.ContentHash(item.Quote) });
            }
            _checkpoints.Checkpoint("ai.before-commit"); ct.ThrowIfCancellationRequested();
            return new QuoteBatchResult(command.BatchId, Array.AsReadOnly(ids), duplicate, command.RejectedCount);
        });
    }
    public async Task<UndoBatchResult> UndoAiBatchAsync(Guid batchId, DataEpoch expectedEpoch, CancellationToken ct)
    {
        var data = _data ?? (_database as DatabaseService)?.Coordinator ?? throw new InvalidOperationException("AI存储尚未配置。");
        await _database.InitializeAsync(); await using var lease = await data.EnterOperationAsync(expectedEpoch, ct);
        return await _database.WriteAsync(lease, c =>
        {
            ct.ThrowIfCancellationRequested(); var batch = c.Find<AiGenerationBatch>(batchId) ?? throw new AiException("BatchMissing", "批次不存在或书籍已删除。");
            if (batch.Status == 1) return new UndoBatchResult(batchId, 0, 0, true);
            var deleted = 0; var retained = 0;
            foreach (var item in c.Table<AiGenerationBatchItem>().Where(i => i.BatchId == batchId).ToArray())
            {
                var quote = c.Find<Quote>(item.QuoteId); if (quote is null) continue;
                if (quote.IsFavorite || quote.Revision != item.CreatedRevision || AiIntegrity.ContentHash(quote) != item.CreatedContentHash) { retained++; continue; }
                c.Delete<Quote>(item.QuoteId); deleted++; _checkpoints.Checkpoint("ai.undo-after-delete");
            }
            c.Execute("UPDATE AiGenerationBatch SET Status=1 WHERE BatchId=?", batchId);
            _checkpoints.Checkpoint("ai.undo-before-commit"); ct.ThrowIfCancellationRequested(); return new UndoBatchResult(batchId, deleted, retained, false);
        });
    }
}
