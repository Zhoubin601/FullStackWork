using SQLite;
namespace ReadJournal.Library.Services;

public class DatabaseMigrationV2
{
    public const int CurrentVersion = 2;
    protected virtual void Checkpoint(string step) { }
    public bool Apply(SQLiteConnection c)
    {
        var version = c.ExecuteScalar<int>("PRAGMA user_version");
        if (version > CurrentVersion) throw new InvalidOperationException("数据库版本高于当前应用支持的版本。");
        if (version == CurrentVersion) return false;
        c.RunInTransaction(() =>
        {
            // Explicit schema1, independent of the new ORM model's properties.
            c.Execute("CREATE TABLE IF NOT EXISTS Book (Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT, Author TEXT, Category TEXT, TotalPages INTEGER NOT NULL, Description TEXT, CreatedAt BIGINT NOT NULL, UpdatedAt BIGINT NOT NULL)");
            c.Execute("CREATE TABLE IF NOT EXISTS ReadingRecord (Id INTEGER PRIMARY KEY AUTOINCREMENT, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, ReadingDate BIGINT NOT NULL, StartPage INTEGER NOT NULL CHECK(StartPage>=1), EndPage INTEGER NOT NULL CHECK(EndPage>=StartPage), Minutes INTEGER NOT NULL CHECK(Minutes>0))");
            c.Execute("CREATE TABLE IF NOT EXISTS Quote (Id INTEGER PRIMARY KEY AUTOINCREMENT, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, Text TEXT NOT NULL, Thoughts TEXT, Page INTEGER, IsFavorite INTEGER NOT NULL, CreatedAt BIGINT NOT NULL, UpdatedAt BIGINT NOT NULL)");
            c.Execute("CREATE TABLE IF NOT EXISTS ReadingPlan (Id INTEGER PRIMARY KEY AUTOINCREMENT, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, StartDate BIGINT NOT NULL, TargetDate BIGINT NOT NULL)");
            c.Execute("ALTER TABLE Book ADD COLUMN Kind INTEGER NOT NULL DEFAULT 0 CHECK(Kind IN (0,1))");
            c.Execute("ALTER TABLE Book ADD COLUMN BookUid TEXT NOT NULL DEFAULT ''");
            foreach (var id in c.Query<LegacyId>("SELECT Id FROM Book"))
                c.Execute("UPDATE Book SET BookUid=? WHERE Id=?", Guid.NewGuid().ToString(), id.Id);
            c.Execute("CREATE UNIQUE INDEX IX_Book_BookUid ON Book(BookUid)");
            c.Execute("ALTER TABLE Quote ADD COLUMN Revision INTEGER NOT NULL DEFAULT 0 CHECK(Revision>=0)");
            Checkpoint("identity");
            c.Execute("CREATE TABLE EbookFile (BookId INTEGER PRIMARY KEY REFERENCES Book(Id) ON DELETE CASCADE, FileId TEXT NOT NULL UNIQUE, Format INTEGER NOT NULL CHECK(Format IN (1,2)), SourceRelativePath TEXT NOT NULL, CanonicalRelativePath TEXT NOT NULL, SourceSha256 TEXT NOT NULL UNIQUE CHECK(length(SourceSha256)=64), CanonicalSha256 TEXT NOT NULL CHECK(length(CanonicalSha256)=64), SourceBytes INTEGER NOT NULL CHECK(SourceBytes>=0), ParserVersion INTEGER NOT NULL CHECK(ParserVersion>0), ImportedUtcTicks INTEGER NOT NULL, Status INTEGER NOT NULL CHECK(Status IN(0,1,2)))");
            c.Execute("CREATE TABLE EbookProgress (BookId INTEGER PRIMARY KEY REFERENCES Book(Id) ON DELETE CASCADE, DocumentKeyJson TEXT NOT NULL, LastPositionJson TEXT NOT NULL, HasStarted INTEGER NOT NULL CHECK(HasStarted IN(0,1)), CurrentPercent REAL NOT NULL DEFAULT 0 CHECK(CurrentPercent>=0 AND CurrentPercent<=100), CompletedUtcTicks INTEGER NULL, CompletedOffsetMinutes INTEGER NULL, LastOpenedUtcTicks INTEGER NOT NULL, LastSavedUtcTicks INTEGER NOT NULL, CHECK(CompletedUtcTicks IS NULL OR HasStarted=1), CHECK((CompletedUtcTicks IS NULL)=(CompletedOffsetMinutes IS NULL)), CHECK((CompletedUtcTicks IS NULL AND CompletedOffsetMinutes IS NULL) OR (CompletedUtcTicks IS NOT NULL AND CompletedOffsetMinutes BETWEEN -840 AND 840)))");
            c.Execute("CREATE TABLE EbookReadingSession (SessionId TEXT PRIMARY KEY NOT NULL, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, BookUid TEXT NOT NULL, DocumentKeyJson TEXT NOT NULL, StartPositionJson TEXT NOT NULL, EndPositionJson TEXT NOT NULL, StartPercent REAL NOT NULL CHECK(StartPercent>=0 AND StartPercent<=100), EndPercent REAL NOT NULL CHECK(EndPercent>=0 AND EndPercent<=100), OpenedUtcTicks INTEGER NOT NULL, ClosedUtcTicks INTEGER NOT NULL, OpenedOffsetMinutes INTEGER NOT NULL CHECK(OpenedOffsetMinutes BETWEEN -840 AND 840), ClosedOffsetMinutes INTEGER NOT NULL CHECK(ClosedOffsetMinutes BETWEEN -840 AND 840), DurationMilliseconds INTEGER NOT NULL CHECK(DurationMilliseconds>=0), CloseLocalDate TEXT NOT NULL, ClockAnomaly INTEGER NOT NULL CHECK(ClockAnomaly IN(0,1)))");
            c.Execute("CREATE INDEX IX_EbookSession_BookClosed ON EbookReadingSession(BookId,ClosedUtcTicks)");
            c.Execute("CREATE TABLE AiGenerationBatch (BatchId TEXT PRIMARY KEY NOT NULL, BookId INTEGER NOT NULL REFERENCES Book(Id) ON DELETE CASCADE, BookUid TEXT NOT NULL, DocumentKeyJson TEXT NOT NULL, ScopeSha256 TEXT NOT NULL CHECK(length(ScopeSha256)=64), CreatedUtcTicks INTEGER NOT NULL, Status INTEGER NOT NULL CHECK(Status IN(0,1)), InsertedCount INTEGER NOT NULL CHECK(InsertedCount>=0), DuplicateCount INTEGER NOT NULL CHECK(DuplicateCount>=0), RejectedCount INTEGER NOT NULL CHECK(RejectedCount>=0), InsertedQuoteIdsJson TEXT NOT NULL)");
            c.Execute("CREATE TABLE QuoteOrigin (QuoteId INTEGER PRIMARY KEY REFERENCES Quote(Id) ON DELETE CASCADE, Source INTEGER NOT NULL CHECK(Source IN(0,1,2)), OriginalText TEXT NOT NULL, DocumentKeyJson TEXT NULL, StartPositionJson TEXT NULL, EndPositionJson TEXT NULL, PrefixHash TEXT NULL, SuffixHash TEXT NULL, LocationStatus INTEGER NOT NULL CHECK(LocationStatus IN(0,1,2)), BatchId TEXT NULL REFERENCES AiGenerationBatch(BatchId) ON DELETE SET NULL, DedupeKey TEXT NULL UNIQUE, CHECK((DocumentKeyJson IS NULL AND StartPositionJson IS NULL AND EndPositionJson IS NULL AND DedupeKey IS NULL) OR (DocumentKeyJson IS NOT NULL AND StartPositionJson IS NOT NULL AND EndPositionJson IS NOT NULL AND DedupeKey IS NOT NULL)))");
            c.Execute("CREATE TABLE AiGenerationBatchItem (BatchId TEXT NOT NULL REFERENCES AiGenerationBatch(BatchId) ON DELETE CASCADE, QuoteId INTEGER NOT NULL REFERENCES Quote(Id) ON DELETE CASCADE, CreatedRevision INTEGER NOT NULL CHECK(CreatedRevision>=0), CreatedContentHash TEXT NOT NULL CHECK(length(CreatedContentHash)=64), PRIMARY KEY(BatchId,QuoteId))");
            c.Execute("CREATE TABLE AppMetadata (Key TEXT PRIMARY KEY NOT NULL, Value TEXT NOT NULL)");
            c.Execute("INSERT INTO AppMetadata(Key,Value) VALUES ('LastRestoreId','')");
            // Enforce new writes without rebuilding old parent tables and their references.
            foreach (var operation in new[] { "INSERT", "UPDATE" })
            {
                c.Execute($"CREATE TRIGGER TR_Book_{operation}_Identity BEFORE {operation} ON Book WHEN length(NEW.BookUid)<>36 OR NEW.BookUid='00000000-0000-0000-0000-000000000000' OR (NEW.Kind=0 AND NEW.TotalPages<=0) OR (NEW.Kind=1 AND NEW.TotalPages<>0) BEGIN SELECT RAISE(ABORT,'invalid book identity/kind/pages'); END");
                foreach (var table in new[] { "EbookFile", "EbookProgress", "EbookReadingSession", "AiGenerationBatch" })
                    c.Execute($"CREATE TRIGGER TR_{table}_{operation}_Kind BEFORE {operation} ON {table} WHEN NOT EXISTS(SELECT 1 FROM Book WHERE Id=NEW.BookId AND Kind=1) BEGIN SELECT RAISE(ABORT,'ebook requires ebook book'); END");
                foreach (var table in new[] { "EbookReadingSession", "AiGenerationBatch" })
                    c.Execute($"CREATE TRIGGER TR_{table}_{operation}_Identity BEFORE {operation} ON {table} WHEN NOT EXISTS(SELECT 1 FROM Book WHERE Id=NEW.BookId AND BookUid=NEW.BookUid) BEGIN SELECT RAISE(ABORT,'book identity mismatch'); END");
                c.Execute($"CREATE TRIGGER TR_ReadingRecord_{operation}_Manual BEFORE {operation} ON ReadingRecord WHEN NOT EXISTS(SELECT 1 FROM Book WHERE Id=NEW.BookId AND Kind=0) BEGIN SELECT RAISE(ABORT,'manual record requires manual book'); END");
                c.Execute($"CREATE TRIGGER TR_Quote_{operation}_Page BEFORE {operation} ON Quote WHEN NEW.Page IS NOT NULL AND EXISTS(SELECT 1 FROM Book WHERE Id=NEW.BookId AND Kind=1) BEGIN SELECT RAISE(ABORT,'ebook quote has no physical page'); END");
            }
            c.Execute("CREATE TRIGGER TR_AiBatch_Receipt_Immutable BEFORE UPDATE ON AiGenerationBatch WHEN NEW.BatchId<>OLD.BatchId OR NEW.BookId<>OLD.BookId OR NEW.BookUid<>OLD.BookUid OR NEW.DocumentKeyJson<>OLD.DocumentKeyJson OR NEW.ScopeSha256<>OLD.ScopeSha256 OR NEW.CreatedUtcTicks<>OLD.CreatedUtcTicks OR NEW.InsertedQuoteIdsJson<>OLD.InsertedQuoteIdsJson OR NEW.InsertedCount<>OLD.InsertedCount OR NEW.DuplicateCount<>OLD.DuplicateCount OR NEW.RejectedCount<>OLD.RejectedCount BEGIN SELECT RAISE(ABORT,'immutable batch receipt'); END");
            c.Execute("CREATE TRIGGER TR_Book_Update_Immutable BEFORE UPDATE ON Book WHEN NEW.Kind<>OLD.Kind OR NEW.BookUid<>OLD.BookUid BEGIN SELECT RAISE(ABORT,'immutable book kind/uid'); END");
            Checkpoint("tables");
            if (c.ExecuteScalar<string>("PRAGMA integrity_check") != "ok" || c.Query<ForeignKeyError>("PRAGMA foreign_key_check").Count != 0)
                throw new InvalidOperationException("数据库迁移完整性校验失败。");
            c.Execute("PRAGMA user_version=2"); Checkpoint("version");
        });
        return true;
    }
    private sealed class LegacyId { public int Id { get; set; } }
    private sealed class ForeignKeyError { public string table { get; set; } = ""; }
}
