using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using SQLite;
using Xunit;
namespace ReadJournal.UnitTest;

public sealed class ReaderMigrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalP2", Guid.NewGuid().ToString("N"));
    private DataPaths Paths => new(_directory);
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Reader", name);
    private void SeedV1()
    {
        Directory.CreateDirectory(_directory);
        using var c = new SQLiteConnection(Paths.DatabasePath);
        foreach (var sql in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Fixture("schema1-sql.json")))!) c.Execute(sql);
    }
    [Theory]
    [InlineData("identity")]
    [InlineData("tables")]
    [InlineData("version")]
    public async Task MigrationFailureRollsBackEveryDdlAndFieldThenRetries(string step)
    {
        SeedV1(); var original = File.ReadAllBytes(Fixture("schema1.json"));
        using var coordinator = new ApplicationDataCoordinator(Paths);
        using (var broken = new DatabaseService(Paths, coordinator, new BrokenMigration(step)))
        { await Assert.ThrowsAsync<IOException>(() => broken.InitializeAsync()); Assert.False(broken.IsInitialized); }
        using (var c = new SQLiteConnection(Paths.DatabasePath))
        {
            Assert.Equal(1, c.ExecuteScalar<int>("PRAGMA user_version"));
            Assert.DoesNotContain(c.GetTableInfo("Book"), x => x.Name == "BookUid" || x.Name == "Kind");
            Assert.DoesNotContain(c.GetTableInfo("Quote"), x => x.Name == "Revision");
            Assert.Equal(0, c.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE name IN('EbookFile','AppMetadata')"));
            Assert.Equal(3, c.ExecuteScalar<int>("SELECT count(*) FROM Book"));
        }
        using var retry = new DatabaseService(Paths, coordinator, new DatabaseMigrationV2());
        await retry.InitializeAsync();
        var books = await retry.ReadAsync(c => c.Table<Book>().ToList());
        Assert.All(books, b => { Assert.NotEqual(Guid.Empty, b.BookUid); Assert.Equal(BookKind.Manual, b.Kind); });
        Assert.Equal(books.Count, books.Select(b => b.BookUid).Distinct().Count());
        Assert.All(await retry.ReadAsync(c => c.Table<Quote>().ToList()), q => Assert.Equal(0, q.Revision));
        var ids = books.ToDictionary(b => b.Id, b => b.BookUid);
        await retry.InitializeAsync(); Assert.Equal(ids.OrderBy(x => x.Key), (await retry.ReadAsync(c => c.Table<Book>().ToList())).ToDictionary(b => b.Id, b => b.BookUid).OrderBy(x => x.Key));
        Assert.Equal(original, File.ReadAllBytes(Fixture("schema1.json")));
    }
    [Fact]
    public async Task FutureSchemaRefusedWithoutMutation()
    {
        SeedV1();
        using (var c = new SQLiteConnection(Paths.DatabasePath)) c.Execute("PRAGMA user_version=99");
        using var db = new DatabaseService(Paths);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.InitializeAsync());
        using var read = new SQLiteConnection(Paths.DatabasePath);
        Assert.Equal(99, read.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.DoesNotContain(read.GetTableInfo("Book"), x => x.Name == "Kind");
    }
    [Fact]
    public async Task IdentityConstraintsKindsForeignKeysAndImmutableAiReceiptAreRealSql()
    {
        using var db = new DatabaseService(Paths); await db.InitializeAsync();
        var book = new Book { Title = "合成电子书", Kind = BookKind.Ebook, TotalPages = 0 };
        await db.WriteAsync(c => c.Insert(book));
        await Assert.ThrowsAnyAsync<Exception>(() => db.WriteAsync(c => c.Insert(new Book { Title = "重复身份", TotalPages = 1, BookUid = book.BookUid })));
        await Assert.ThrowsAnyAsync<Exception>(() => db.WriteAsync(c => c.Execute("UPDATE Book SET Kind=0 WHERE Id=?", book.Id)));
        await Assert.ThrowsAnyAsync<Exception>(() => db.WriteAsync(c => c.Insert(new ReadingRecord { BookId = book.Id, StartPage = 1, EndPage = 2, Minutes = 1 })));
        await Assert.ThrowsAnyAsync<Exception>(() => db.WriteAsync(c => c.Insert(new Quote { BookId = book.Id, Text = "错页码", Page = 1 })));
        await Assert.ThrowsAnyAsync<Exception>(() => db.WriteAsync(c => c.Insert(new EbookProgress { BookId = book.Id, CurrentPercent = 101 })));
        var quote = new Quote { BookId = book.Id, Text = "原句" }; await db.WriteAsync(c => c.Insert(quote));
        var batch = new AiGenerationBatch { BatchId = Guid.NewGuid(), BookId = book.Id, BookUid = book.BookUid,
            ScopeSha256 = new string('a', 64), InsertedCount = 1, InsertedQuoteIdsJson = JsonSerializer.Serialize(new[] { quote.Id }) };
        await db.WriteAsync(c => { c.Insert(batch); return c.Insert(new AiGenerationBatchItem { BatchId = batch.BatchId, QuoteId = quote.Id, CreatedContentHash = new string('b',64) }); });
        await Assert.ThrowsAnyAsync<Exception>(() => db.WriteAsync(c => c.Execute("UPDATE AiGenerationBatch SET InsertedQuoteIdsJson='[]' WHERE BatchId=?", batch.BatchId)));
        await db.WriteAsync(c => c.Delete<Quote>(quote.Id));
        Assert.Empty(await db.ReadAsync(c => c.Table<AiGenerationBatchItem>().ToList()));
        Assert.Equal(batch.InsertedQuoteIdsJson, (await db.ReadAsync(c => c.Find<AiGenerationBatch>(batch.BatchId)))!.InsertedQuoteIdsJson);
        await db.WriteAsync(c => c.Delete<Book>(book.Id));
        Assert.Empty(await db.ReadAsync(c => c.Table<AiGenerationBatch>().ToList()));
        Assert.Equal("ok", await db.ReadAsync(c => c.ExecuteScalar<string>("PRAGMA integrity_check")));
        Assert.Equal(0, await db.ReadAsync(c => c.Query<FkError>("PRAGMA foreign_key_check").Count));
    }
    [Fact]
    public async Task QuoteEditAndFavoriteIncrementCurrentRevision()
    {
        using var db = new DatabaseService(Paths); var reading = new ReadingService();
        var book = new Book { Title = "手动", TotalPages = 10 }; await new BookStorage(db, reading).SaveAsync(book);
        var storage = new QuoteStorage(db, reading); var quote = new Quote { BookId = book.Id, Text = "原句" };
        await storage.SaveAsync(quote); await storage.ToggleFavoriteAsync(quote.Id); quote.Text = "修改"; await storage.SaveAsync(quote);
        Assert.Equal(2, (await storage.GetAsync(quote.Id))!.Revision);
    }
    private sealed class BrokenMigration(string step) : DatabaseMigrationV2
    { protected override void Checkpoint(string current) { if (current == step) throw new IOException("injected migration interruption"); } }
    private sealed class FkError { public string table { get; set; } = ""; }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
