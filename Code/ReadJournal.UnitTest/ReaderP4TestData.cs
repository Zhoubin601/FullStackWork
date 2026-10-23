using System.Globalization;
using System.Text;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.UnitTest;

public static class ReaderP4TestData
{
    public const string Text = "第一章 合成起点\r\n这是P4实际导入的中文正文，包含 e\u0301 与 👩‍💻。\r下一段。\n\n第二章 合成终点\n保留原书和所有关联数据。";
    public static async Task AddRelations(DatabaseService db, int id)
    {
        await db.WriteAsync(c =>
        {
            var book = c.Find<Book>(id)!; var f = c.Find<EbookFile>(id)!; var p = c.Find<EbookProgress>(id)!;
            var key = ReaderContractValidator.ReadDocument(p.DocumentKeyJson); var start = ReaderContractValidator.ReadPosition(p.LastPositionJson);
            var end = start with { Utf16Offset = 1 }; var now = DateTimeOffset.UtcNow; var local = now.ToOffset(TimeSpan.FromHours(8));
            var manifest = BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(ManagedBookFiles.SafePath(db.Paths.DirectoryPath, f.CanonicalRelativePath)));
            var chapter = manifest.Chapters.Single(ch => ch.Key == start.ChapterKey);
            var original = Encoding.Unicode.GetString(File.ReadAllBytes(ManagedBookFiles.SafePath(db.Paths.DirectoryPath, ManagedBookFiles.BookPrefix(f.FileId) + chapter.TextPath)))[..1];
            p.HasStarted = true; c.Update(p);
            c.Insert(new EbookReadingSession { SessionId = Guid.NewGuid(), BookId = id, BookUid = book.BookUid, DocumentKeyJson = p.DocumentKeyJson,
                StartPositionJson = p.LastPositionJson, EndPositionJson = ReaderContractValidator.WritePosition(end), StartPercent = 0, EndPercent = 100.0 / manifest.TotalTextUnits,
                OpenedUtcTicks = now.AddMilliseconds(-65000).UtcTicks, ClosedUtcTicks = now.UtcTicks, OpenedOffsetMinutes = 480, ClosedOffsetMinutes = 480, DurationMilliseconds = 65000, CloseLocalDate = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
            c.Insert(new ReadingPlan { BookId = id, StartDate = DateTime.Today, TargetDate = DateTime.Today.AddDays(3) });
            var quote = new Quote { BookId = id, Text = original, Thoughts = "合成关联", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now }; c.Insert(quote);
            var batch = Guid.NewGuid(); c.Insert(new AiGenerationBatch { BatchId = batch, BookId = id, BookUid = book.BookUid, DocumentKeyJson = p.DocumentKeyJson, ScopeSha256 = new string('a', 64), CreatedUtcTicks = now.UtcTicks, InsertedCount = 1, InsertedQuoteIdsJson = "[" + quote.Id + "]" });
            c.Insert(new QuoteOrigin { QuoteId = quote.Id, Source = QuoteSource.AiGenerated, OriginalText = original, DocumentKeyJson = p.DocumentKeyJson, StartPositionJson = p.LastPositionJson, EndPositionJson = ReaderContractValidator.WritePosition(end), BatchId = batch, DedupeKey = new string('b', 64) });
            c.Insert(new AiGenerationBatchItem { BatchId = batch, QuoteId = quote.Id, CreatedContentHash = new string('c', 64), CreatedRevision = 0 });
            return 0;
        });
    }
}
