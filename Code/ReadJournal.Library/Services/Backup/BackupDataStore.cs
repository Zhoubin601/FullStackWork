using SQLite;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Backup;

public static class BackupDataStore
{
    public static BackupDataV2 Snapshot(SQLiteConnection c) => Normalize(new(2,
        c.Table<Book>().ToList().Select(b => new BackupBookV2(b, b.BookUid, b.Kind)).ToArray(),
        c.Table<ReadingRecord>().ToList(), c.Table<Quote>().ToList().Select(q => new BackupQuoteV2(q, q.Revision)).ToArray(),
        c.Table<ReadingPlan>().ToList(), c.Table<EbookFile>().ToList(), c.Table<EbookProgress>().ToList(),
        c.Table<EbookReadingSession>().ToList(), c.Table<QuoteOrigin>().ToList(), c.Table<AiGenerationBatch>().ToList(), c.Table<AiGenerationBatchItem>().ToList()));
    public static BackupDataV2 Normalize(BackupDataV2 data)
    {
        if (data.Books is null || data.Quotes is null || data.ReadingRecords is null || data.ReadingPlans is null || data.Files is null ||
            data.Progress is null || data.Sessions is null || data.Origins is null || data.Batches is null || data.BatchItems is null ||
            data.Books.Any(x => x?.Book is null) || data.Quotes.Any(x => x?.Quote is null) || data.ReadingRecords.Any(x => x is null) || data.ReadingPlans.Any(x => x is null) || data.Files.Any(x => x is null) || data.Progress.Any(x => x is null) || data.Sessions.Any(x => x is null) || data.Origins.Any(x => x is null) || data.Batches.Any(x => x is null) || data.BatchItems.Any(x => x is null)) throw new ArgumentException("InvalidFormat：缺少业务实体集合。");
        // SQLite stores these legacy local-date/time fields as ticks, without DateTime.Kind.
        // Compare/serialize the persisted semantics rather than JSON timezone suffixes.
        static DateTime LocalTicks(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        foreach (var b in data.Books) { b.Book.CreatedAt = LocalTicks(b.Book.CreatedAt); b.Book.UpdatedAt = LocalTicks(b.Book.UpdatedAt); }
        foreach (var q in data.Quotes) { q.Quote.CreatedAt = LocalTicks(q.Quote.CreatedAt); q.Quote.UpdatedAt = LocalTicks(q.Quote.UpdatedAt); }
        foreach (var r in data.ReadingRecords) r.ReadingDate = LocalTicks(r.ReadingDate);
        foreach (var p in data.ReadingPlans) { p.StartDate = LocalTicks(p.StartDate); p.TargetDate = LocalTicks(p.TargetDate); }
        foreach (var b in data.Books) { b.Book.BookUid = b.BookUid; b.Book.Kind = b.Kind; }
        foreach (var q in data.Quotes) q.Quote.Revision = q.Revision;
        return data with { Books = data.Books.OrderBy(x => x.Book.Id).ToArray(), Quotes = data.Quotes.OrderBy(x => x.Quote.Id).ToArray(),
            ReadingRecords = data.ReadingRecords.OrderBy(x => x.Id).ToArray(), ReadingPlans = data.ReadingPlans.OrderBy(x => x.Id).ToArray(),
            Files = data.Files.OrderBy(x => x.BookId).ToArray(), Progress = data.Progress.OrderBy(x => x.BookId).ToArray(),
            Sessions = data.Sessions.OrderBy(x => x.SessionId.ToString("D"), StringComparer.Ordinal).ToArray(),
            Origins = data.Origins.OrderBy(x => x.QuoteId).ToArray(), Batches = data.Batches.OrderBy(x => x.BatchId.ToString("D"), StringComparer.Ordinal).ToArray(),
            BatchItems = data.BatchItems.OrderBy(x => x.BatchId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.QuoteId).ToArray() };
    }
    public static string Hash(BackupDataV2 data) => ManagedBookFiles.Sha256(BackupJson.Write(Normalize(data)));
    public static string LastRestoreId(SQLiteConnection c) => c.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE type='table' AND name='AppMetadata'") == 0
        ? "" : c.ExecuteScalar<string>("SELECT Value FROM AppMetadata WHERE Key='LastRestoreId'") ?? "";
    public static void Replace(SQLiteConnection c, BackupDataV2 data, Guid restoreId)
    {
        c.DeleteAll<AiGenerationBatchItem>(); c.DeleteAll<QuoteOrigin>(); c.DeleteAll<AiGenerationBatch>();
        c.DeleteAll<EbookReadingSession>(); c.DeleteAll<EbookProgress>(); c.DeleteAll<EbookFile>();
        c.DeleteAll<ReadingPlan>(); c.DeleteAll<Quote>(); c.DeleteAll<ReadingRecord>(); c.DeleteAll<Book>();
        foreach (var x in data.Books) c.InsertOrReplace(x.Book);
        foreach (var x in data.ReadingRecords) c.InsertOrReplace(x);
        foreach (var x in data.Quotes) c.InsertOrReplace(x.Quote);
        foreach (var x in data.ReadingPlans) c.InsertOrReplace(x);
        foreach (var x in data.Files) c.Insert(x);
        foreach (var x in data.Progress) c.Insert(x);
        foreach (var x in data.Sessions) c.Insert(x);
        foreach (var x in data.Batches) c.Insert(x);
        foreach (var x in data.Origins) c.Insert(x);
        foreach (var x in data.BatchItems) c.Insert(x);
        c.InsertOrReplace(new AppMetadata { Key = "LastRestoreId", Value = restoreId.ToString("D") });
        if (c.ExecuteScalar<string>("PRAGMA integrity_check") != "ok" || c.Query<ForeignKeyError>("PRAGMA foreign_key_check").Count != 0 || Hash(Snapshot(c)) != Hash(data))
            throw new InvalidOperationException("StorageFailure：恢复事务校验失败。");
    }
    private sealed class ForeignKeyError { public string table { get; set; } = ""; }
}
