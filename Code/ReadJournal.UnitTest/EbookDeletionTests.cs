using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using Xunit;
namespace ReadJournal.UnitTest;

public sealed class EbookDeletionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReadJournalP4DeleteTests", Guid.NewGuid().ToString("N"));
    private DataPaths Paths => new(Path.Combine(_root, "data"));
    private async Task<string> Source()
    { var source = Path.Combine(_root, "data", "books", "inbox.txt"); Directory.CreateDirectory(Path.GetDirectoryName(source)!); await File.WriteAllTextAsync(source, ReaderP4TestData.Text); return source; }
    private static Task<BackupDataV2> Data(DatabaseService db) => db.ReadAsync(BackupDataStore.Snapshot);
    [Fact]
    public async Task ActualImportedTxtBackupDeleteRestorePreservesAllRowsFilesAndSource()
    {
        var source = await Source(); var paths = Paths; using var db = new DatabaseService(paths); var library = EbookRuntime.CreateLibrary(db);
        var imported = await library.ImportAsync(new(source), null, default); Assert.True(imported.State == ImportState.Imported, imported.Message);
        var initial = await Data(db); Assert.False(initial.Progress.Single().HasStarted); Assert.Equal(0, initial.Progress.Single().CurrentPercent);
        await ReaderP4TestData.AddRelations(db, imported.BookId!.Value);
        var before = await Data(db); var hash = BackupDataStore.Hash(before); var file = before.Files.Single(); var originalSha = ManagedBookFiles.HashFile(source);
        var inventory = ManagedBookInventory.Inspect(paths.DirectoryPath, before, new()); var epoch = db.Coordinator.CurrentEpoch;
        var impact = await library.GetDeleteImpactAsync(file.BookId, default); Assert.Equal(0, impact.ManualRecords); Assert.Equal(1, impact.Sessions); Assert.Equal(1, impact.Quotes); Assert.Equal(1, impact.Plans); Assert.Equal(inventory.Sum(f => f.Bytes), impact.ManagedFileBytes);
        var books = new BookStorage(db, new ReadingService()); Assert.Contains("1 次电子阅读会话", await books.GetDeleteImpactAsync(file.BookId));
        var backup = BackupRuntime.CreateService(db, new ReadingService(), paths); var zip = Path.Combine(_root, "real-txt.zip"); await backup.ExportAsync(zip, default);
        await books.DeleteAsync(file.BookId); var empty = await Data(db);
        Assert.Empty(empty.Books); Assert.Empty(empty.Files); Assert.Empty(empty.Progress); Assert.Empty(empty.Sessions); Assert.Empty(empty.Quotes); Assert.Empty(empty.Origins); Assert.Empty(empty.Batches); Assert.Empty(empty.BatchItems); Assert.Empty(empty.ReadingPlans);
        Assert.False(Directory.Exists(ManagedBookFiles.SafePath(paths.DirectoryPath, ManagedBookFiles.BookPrefix(file.FileId).TrimEnd('/')))); Assert.Equal(originalSha, ManagedBookFiles.HashFile(source));
        await backup.RestoreAsync(zip, default); Assert.Equal(hash, BackupDataStore.Hash(await Data(db))); Assert.Equal(inventory, ManagedBookInventory.Inspect(paths.DirectoryPath, await Data(db), new()));
        Assert.Equal(originalSha, ManagedBookFiles.HashFile(source));
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.DeleteBookAsync(file.BookId, before.Books.Single().BookUid, epoch, default));
        Assert.Single((await Data(db)).Books);
    }
    [Theory]
    [InlineData("delete.before-transaction")][InlineData("delete.in-transaction")][InlineData("delete.after-commit")][InlineData("delete.before-trash")][InlineData("delete.after-trash")][InlineData("delete.cleanup")]
    public async Task DeleteFailuresKeepCommitFactAndRetriableCleanup(string point)
    {
        var source = await Source(); var paths = Paths; var original = ManagedBookFiles.HashFile(source);
        using (var db = new DatabaseService(paths))
        {
            var added = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); Assert.True(added.State == ImportState.Imported, added.Message);
            var library = EbookRuntime.CreateLibrary(db, new Fault(point));
            var deleting = library.DeleteBookAsync(added.BookId!.Value, added.BookUid!.Value, db.Coordinator.CurrentEpoch, default);
            if (point is "delete.before-transaction" or "delete.in-transaction") { await Assert.ThrowsAsync<IOException>(() => deleting); Assert.Single((await Data(db)).Books); }
            else { await deleting; Assert.Empty((await Data(db)).Books); Assert.NotNull(library.LastWarning); }
            Assert.Equal(original, ManagedBookFiles.HashFile(source));
        }
        using var reopened = new DatabaseService(paths); await reopened.InitializeAsync(); var remaining = await Data(reopened);
        Assert.Equal(point is "delete.before-transaction" or "delete.in-transaction" ? 1 : 0, remaining.Books.Count);
        Assert.Empty(Directory.GetFiles(Path.Combine(paths.DirectoryPath, "operations"), "*.json")); ManagedBookInventory.Inspect(paths.DirectoryPath, remaining, new());
    }
    [Fact]
    public async Task ActiveReaderOrWrongBookIdentityCannotDelete()
    {
        var source = await Source(); using var db = new DatabaseService(Paths); var library = EbookRuntime.CreateLibrary(db); var result = await library.ImportAsync(new(source), null, default); var file = (await Data(db)).Files.Single();
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.DeleteBookAsync(result.BookId!.Value, Guid.NewGuid(), db.Coordinator.CurrentEpoch, default));
        await using (var reader = await db.Coordinator.RegisterReaderAsync(new(file.BookId, result.BookUid!.Value, new(file.FileId, file.SourceSha256, file.CanonicalSha256, 1), db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1), default))
            await Assert.ThrowsAsync<InvalidOperationException>(() => library.DeleteBookAsync(result.BookId!.Value, result.BookUid!.Value, db.Coordinator.CurrentEpoch, default));
        Assert.Single((await Data(db)).Books); Assert.True(File.Exists(source));
    }
    [Fact]
    public async Task TamperedCompleteImportLogStopsStartupAndPreservesEveryFile()
    {
        var source = await Source(); var paths = Paths;
        using (var db = new DatabaseService(paths))
        {
            var result = await EbookRuntime.CreateLibrary(db, new Fault("import.cleanup")).ImportAsync(new(source), null, default); Assert.True(result.State == ImportState.Imported, result.Message);
        }
        var journal = Directory.GetFiles(Path.Combine(paths.DirectoryPath, "operations"), "*.json").Single(); var envelope = BackupJson.Read<BookFileJournalEnvelope>(await File.ReadAllBytesAsync(journal));
        var altered = envelope.Payload with { Files = envelope.Payload.Files.Select((f, i) => i == 0 ? f with { Sha256 = new string('0', 64) } : f).ToArray() };
        await File.WriteAllBytesAsync(journal, BackupJson.Write(new BookFileJournalEnvelope(1, altered, ManagedBookFiles.Sha256(BackupJson.Write(altered)))));
        var before = Directory.GetFiles(paths.DirectoryPath, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith("library.lock")).ToDictionary(p => p, ManagedBookFiles.HashFile);
        using var reopened = new DatabaseService(paths); var error = await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.InitializeAsync()); Assert.Contains("RecoveryRequired", error.Message); Assert.False(reopened.IsInitialized);
        Assert.All(before, pair => Assert.Equal(pair.Value, ManagedBookFiles.HashFile(pair.Key)));
    }
    [Fact]
    public async Task DeletedInboxBookStaysDeletedAfterScanAndRestartButExplicitImportStillWorks()
    {
        var source = await Source(); var paths = Paths; var sha = ManagedBookFiles.HashFile(source);
        using (var db = new DatabaseService(paths))
        {
            var library = EbookRuntime.CreateLibrary(db); Assert.Equal(1, (await library.ScanInboxAsync(default)).Imported);
            var book = (await Data(db)).Books.Single(); await library.DeleteBookAsync(book.Book.Id, book.BookUid, db.Coordinator.CurrentEpoch, default);
            Assert.Equal(0, (await library.ScanInboxAsync(default)).Imported); Assert.Empty((await Data(db)).Books);
        }
        using var reopened = new DatabaseService(paths); var again = EbookRuntime.CreateLibrary(reopened);
        Assert.Equal(0, (await again.ScanInboxAsync(default)).Imported); Assert.Empty((await Data(reopened)).Books); Assert.Equal(sha, ManagedBookFiles.HashFile(source));
        var explicitImport = await again.ImportAsync(new(source), null, default); Assert.True(explicitImport.State == ImportState.Imported, explicitImport.Message);
    }
    [Fact]
    public async Task MissingEntryCanBeExplicitlyRemovedThenRecoveredFromLastCompleteBackup()
    {
        var source = await Source(); var paths = Paths; using var db = new DatabaseService(paths); var library = EbookRuntime.CreateLibrary(db);
        var imported = await library.ImportAsync(new(source), null, default); Assert.True(imported.State == ImportState.Imported, imported.Message); var original = await Data(db);
        var backup = BackupRuntime.CreateService(db, new ReadingService(), paths); var zip = Path.Combine(_root, "before-loss.zip"); await backup.ExportAsync(zip, default);
        File.Delete(ManagedBookFiles.SafePath(paths.DirectoryPath, original.Files.Single().SourceRelativePath)); await library.ReconcileAsync(default);
        // P3 must still refuse to replace an incomplete current library without a complete automatic backup.
        await Assert.ThrowsAnyAsync<Exception>(() => backup.RestoreAsync(zip, default));
        await library.DeleteBookAsync(imported.BookId!.Value, imported.BookUid!.Value, db.Coordinator.CurrentEpoch, default); await backup.RestoreAsync(zip, default);
        Assert.Equal(BackupDataStore.Hash(original), BackupDataStore.Hash(await Data(db))); Assert.True(File.Exists(source));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class Fault(string point) : ManagedBookFiles { public override void Checkpoint(string current) { if (current == point) throw new IOException("Injected " + point); } }
}
