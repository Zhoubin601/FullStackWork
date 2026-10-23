using System.Globalization;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using Xunit;
namespace ReadJournal.UnitTest;

// Report unavailable OS privileges as a skipped case, never as a successful security check.
public sealed class SymlinkFactAttribute : FactAttribute
{
    public SymlinkFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "ReadJournalSymlinkCapability", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var link = Path.Combine(root, "probe-link");
        try { File.CreateSymbolicLink(link, Path.Combine(root, "absent-target")); }
        catch (IOException ex) when ((ex.HResult & 0xffff) == 1314)
        { Skip = "Windows account cannot create symbolic links (ERROR_PRIVILEGE_NOT_HELD); run with symlink capability to verify this case."; }
        finally { File.Delete(link); Directory.Delete(root); }
    }
}

public sealed class TxtImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReadJournalP4Tests", Guid.NewGuid().ToString("N"));
    private DataPaths Paths(string name = "data") => new(Path.Combine(_root, name));
    private async Task<string> Source(string text = "第一章 起点\r\n汉字 e\u0301 👩‍💻。\r下一行\n\n第二章 终点\n保留末尾", string name = "source/book.txt", Encoding? encoding = null)
    { var path = Path.Combine(_root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, text, encoding ?? new UTF8Encoding(false)); return path; }
    private static Task<BackupDataV2> Data(DatabaseService db) => db.ReadAsync(BackupDataStore.Snapshot);
    private static CanonicalManifest Manifest(DataPaths paths, EbookFile file) => BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(ManagedBookFiles.SafePath(paths.DirectoryPath, file.CanonicalRelativePath)));
    private static string Text(DataPaths paths, EbookFile file) => string.Concat(Manifest(paths, file).Chapters.Select(c => new UnicodeEncoding(false, false, true).GetString(File.ReadAllBytes(ManagedBookFiles.SafePath(paths.DirectoryPath, ManagedBookFiles.BookPrefix(file.FileId) + c.TextPath)))));
    private static string[] Journals(DataPaths paths) => Directory.Exists(Path.Combine(paths.DirectoryPath, "operations")) ? Directory.GetFiles(Path.Combine(paths.DirectoryPath, "operations"), "*.json") : [];
    [Theory]
    [InlineData("utf8")][InlineData("bom")][InlineData("gb18030")]
    public async Task StrictEncodingAndMixedNewlinesPreserveSourceAndInitialProgress(string kind)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var original = "第一章 起点\r\n中文。\r下一行\n\n第二章 终点\n末尾";
        var source = await Source(original, encoding: kind == "gb18030" ? Encoding.GetEncoding("gb18030") : new UTF8Encoding(kind == "bom")); var sha = ManagedBookFiles.HashFile(source);
        var paths = Paths(); using var db = new DatabaseService(paths); var library = EbookRuntime.CreateLibrary(db);
        var result = await library.ImportAsync(new(source, kind == "gb18030" ? kind : null), null, default);
        Assert.True(result.State == ImportState.Imported, result.Message); var data = await Data(db); var file = Assert.Single(data.Files); var p = Assert.Single(data.Progress);
        Assert.Equal(sha, ManagedBookFiles.HashFile(source)); Assert.Equal(sha, ManagedBookFiles.HashFile(ManagedBookFiles.SafePath(paths.DirectoryPath, file.SourceRelativePath)));
        Assert.Equal(original.Replace("\r\n", "\n").Replace('\r', '\n'), Text(paths, file));
        Assert.False(p.HasStarted); Assert.Equal(0, p.CurrentPercent); Assert.Equal(0, ReaderContractValidator.ReadPosition(p.LastPositionJson).Utf16Offset);
        Assert.Equal(BookKind.Ebook, data.Books.Single().Kind); Assert.Equal(0, data.Books.Single().Book.TotalPages); Assert.Equal(2, Manifest(paths, file).Chapters.Count);
        Assert.Equal((int)EbookFileStatus.Ready, file.Status); Assert.Empty(Journals(paths));
        Assert.NotEmpty(ManagedBookInventory.Inspect(paths.DirectoryPath, data, new()));
    }
    [Theory]
    [InlineData("invalid-utf8")][InlineData("invalid-gb18030")][InlineData("utf16")][InlineData("binary")][InlineData("empty")][InlineData("epub")][InlineData("source-limit")]
    public async Task InvalidInputNeverCreatesVisibleBookOrChangesSource(string kind)
    {
        var source = await Source(name: kind == "epub" ? "source/test.epub" : "source/book.txt");
        if (kind == "invalid-utf8") await File.WriteAllBytesAsync(source, [0xff, 0xff, 0x61]);
        if (kind == "invalid-gb18030") await File.WriteAllBytesAsync(source, [0x81]);
        if (kind == "utf16") await File.WriteAllTextAsync(source, "正文", Encoding.Unicode);
        if (kind == "binary") await File.WriteAllTextAsync(source, "正文\0二进制");
        if (kind == "empty") await File.WriteAllBytesAsync(source, []);
        if (kind == "source-limit") { using var stream = File.OpenWrite(source); stream.SetLength(104857601); }
        var sha = ManagedBookFiles.HashFile(source); var paths = Paths(); using var db = new DatabaseService(paths);
        var result = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source, kind == "invalid-gb18030" ? "gb18030" : null), null, default);
        Assert.Equal(ImportState.Rejected, result.State); Assert.Equal(sha, ManagedBookFiles.HashFile(source)); Assert.Empty((await Data(db)).Books); Assert.Empty(Journals(paths));
        Assert.False(Directory.Exists(Path.Combine(paths.DirectoryPath, "books", "managed")) && Directory.EnumerateDirectories(Path.Combine(paths.DirectoryPath, "books", "managed")).Any());
        if (kind == "invalid-utf8") Assert.Contains("EncodingRequired", result.Message);
    }
    [Fact]
    public async Task LongUnheadedTextHasStableCanonicalAndNoLostOrSplitGraphemes()
    {
        var original = string.Concat(Enumerable.Repeat("汉 e\u0301 👩‍💻。", 100000)); var source = await Source(original); var hashes = new List<string>();
        foreach (var name in new[] { "first", "second" })
        {
            var paths = Paths(name); using var db = new DatabaseService(paths); var result = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default);
            Assert.True(result.State == ImportState.Imported, result.Message); var file = (await Data(db)).Files.Single(); var manifest = Manifest(paths, file); hashes.Add(file.CanonicalSha256);
            Assert.Equal(original, Text(paths, file)); Assert.True(manifest.Chapters.Count > 1); Assert.All(manifest.Chapters, c => Assert.InRange(c.Utf16Length, 1, 262144));
            Assert.All(manifest.Chapters.SelectMany(c => c.Blocks), b => Assert.InRange(b.Utf16Length, 1, 8192));
            ManagedBookInventory.Inspect(paths.DirectoryPath, await Data(db), new());
            var documents = new ReaderDocumentService(db, db.Coordinator); var book = (await Data(db)).Books.Single(); var key = new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, 1);
            var loaded = await documents.OpenAsync(new(book.Book.Id, book.BookUid, key, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1), default);
            var last = loaded.Chapters.Last(); var slice = await documents.ReadSliceAsync(key, last.Key, 0, 1024, default); Assert.NotEmpty(slice.Text); Assert.NotEmpty(slice.Blocks);
            var position = new ReaderPosition(key, last.Key, last.Utf16Length); Assert.Equal(100, await documents.GetPercentAsync(position, default)); await documents.ReleaseAllAsync();
        }
        Assert.Equal(hashes[0], hashes[1]);
    }
    [Theory]
    [InlineData("cluster")][InlineData("text")][InlineData("blocks")]
    public async Task ParserBudgetsRejectBeforeInstallingFiles(string kind)
    {
        var source = await Source(kind == "cluster" ? "a" + new string('\u0301', 1025) : "正文\n正文\n正文\n"); var paths = Paths(); using var db = new DatabaseService(paths);
        var result = await EbookRuntime.CreateLibrary(db, txtLimits: kind == "text" ? new(MaxTextUtf16: 5) : kind == "blocks" ? new(MaxBlocks: 1) : new()).ImportAsync(new(source), null, default);
        Assert.Equal(ImportState.Rejected, result.State); Assert.Contains("LimitExceeded", result.Message); Assert.Empty((await Data(db)).Books); Assert.Empty(Journals(paths));
    }
    [Fact]
    public async Task DuplicateBytesAndSameNameDifferentContentsKeepCorrectBooksAndSources()
    {
        var a = await Source(name: "outside/book.txt"); var copy = await Source(name: "data/books/copy.txt"); var b = await Source("另一本正文。", "other/book.txt"); var paths = Paths(); using var db = new DatabaseService(paths); var library = EbookRuntime.CreateLibrary(db);
        Assert.Equal(ImportState.Imported, (await library.ImportAsync(new(a), null, default)).State);
        Assert.Equal(ImportState.Duplicate, (await library.ImportAsync(new(copy), null, default)).State);
        Assert.Equal(ImportState.Imported, (await library.ImportAsync(new(b), null, default)).State);
        Assert.Equal(2, (await Data(db)).Books.Count); Assert.Equal(ManagedBookFiles.HashFile(a), ManagedBookFiles.HashFile(copy)); Assert.True(File.Exists(b)); Assert.Empty(Journals(paths));
    }
    [Fact]
    public async Task SimultaneousIdenticalImportsUseSqlUniqueConstraintAndCleanLosingDirectory()
    {
        var source = await Source(); var paths = Paths(); using var db = new DatabaseService(paths); var files = new ImportBarrier(); var library = EbookRuntime.CreateLibrary(db, files);
        var first = library.ImportAsync(new(source), null, default); var second = library.ImportAsync(new(source), null, default);
        Assert.True(await Task.Run(() => files.Ready.Wait(TimeSpan.FromSeconds(15)))); files.Continue.Set();
        var results = await Task.WhenAll(first, second); Assert.Single(results.Where(r => r.State == ImportState.Imported)); Assert.Single(results.Where(r => r.State == ImportState.Duplicate));
        Assert.Equal(2, files.Prepared); Assert.Single((await Data(db)).Books); Assert.Single((await Data(db)).Files);
        Assert.Single(Directory.GetDirectories(Path.Combine(paths.DirectoryPath, "books", "managed"))); Assert.Empty(Journals(paths));
        Assert.Contains("SourceSha256 TEXT NOT NULL UNIQUE", await db.ReadAsync(c => c.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE name='EbookFile'")));
    }
    [Theory]
    [InlineData("import.copying")][InlineData("import.parsing")][InlineData("import.before-install")][InlineData("import.after-install")][InlineData("import.before-transaction")][InlineData("import.in-transaction")][InlineData("book-journal.before-write")]
    public async Task PreCommitIoFailuresLeaveNoReadyRowsOrUnreferencedManagedFiles(string point)
    {
        var source = await Source(); var paths = Paths(); using var db = new DatabaseService(paths); var sha = ManagedBookFiles.HashFile(source);
        var result = await EbookRuntime.CreateLibrary(db, new Fault(point)).ImportAsync(new(source), null, default);
        Assert.Equal(ImportState.Rejected, result.State); Assert.Empty((await Data(db)).Books); Assert.Empty(Journals(paths)); Assert.Equal(sha, ManagedBookFiles.HashFile(source));
        ManagedBookInventory.Inspect(paths.DirectoryPath, await Data(db), new());
    }
    [Theory]
    [InlineData("import.after-install")][InlineData("import.in-transaction")][InlineData("import.after-commit")]
    public async Task CancellationReportsActualCommitState(string point)
    {
        var source = await Source(); var paths = Paths(); using var db = new DatabaseService(paths); using var cts = new CancellationTokenSource();
        var result = await EbookRuntime.CreateLibrary(db, new ActionFiles(p => { if (p == point) cts.Cancel(); })).ImportAsync(new(source), null, cts.Token);
        // A cancellation after the transaction begins cannot turn a committed insert into Cancelled.
        var committed = point is "import.in-transaction" or "import.after-commit";
        Assert.Equal(committed ? ImportState.Imported : ImportState.Cancelled, result.State); Assert.Equal(committed ? 1 : 0, (await Data(db)).Books.Count); Assert.Empty(Journals(paths));
    }
    [Fact]
    public async Task CommittedImportCleanupCanRetryAfterOrdinaryMetadataEdits()
    {
        var source = await Source(); var paths = Paths();
        using (var db = new DatabaseService(paths))
        {
            var library = EbookRuntime.CreateLibrary(db, new Fault("import.cleanup")); var result = await library.ImportAsync(new(source), null, default);
            Assert.True(result.State == ImportState.Imported, result.Message); Assert.Contains("清理", result.Message); Assert.Single(Journals(paths));
            var book = (await Data(db)).Books.Single().Book; book.Title = "之后正常编辑"; await new BookStorage(db, new ReadingService()).SaveAsync(book);
        }
        using var reopened = new DatabaseService(paths); await reopened.InitializeAsync(); Assert.Equal("之后正常编辑", (await Data(reopened)).Books.Single().Book.Title); Assert.Empty(Journals(paths));
    }
    [Fact]
    public async Task RootOnlyScanSkipsChangingFilesAndOneBadFileDoesNotBlockOthers()
    {
        var paths = Paths(); var good = await Source("正常正文", "data/books/good.txt"); var changing = await Source("变化前", "data/books/changing.txt");
        var bad = await Source(name: "data/books/bad.txt"); await File.WriteAllBytesAsync(bad, [0xff]);
        await Source(name: "data/books/nested/ignored.txt"); await Source(name: "data/books/ignored.tmp");
        using var db = new DatabaseService(paths); var once = false;
        var library = EbookRuntime.CreateLibrary(db, new ActionFiles(p => { if (p == "scan.observed" && !once) { once = true; File.AppendAllText(changing, "新增"); } }));
        var first = await library.ScanInboxAsync(default); Assert.Equal(1, first.Imported); Assert.Equal(2, first.Rejected); Assert.Contains(first.Items, r => r.Message.Contains("SourceChanging"));
        var second = await library.ScanInboxAsync(default); Assert.Equal(1, second.Imported); Assert.Equal(1, second.Duplicates); Assert.Equal(1, second.Rejected);
        var third = await library.ScanInboxAsync(default); Assert.Equal(0, third.Imported); Assert.Equal(2, third.Duplicates); Assert.Equal(2, (await Data(db)).Books.Count); Assert.True(File.Exists(good));
    }
    [Theory]
    [InlineData("source")][InlineData("manifest")][InlineData("chapter")]
    public async Task MissingFilesAreMarkedAndCannotOpenOrExport(string missing)
    {
        var source = await Source(); var paths = Paths(); using var db = new DatabaseService(paths); var library = EbookRuntime.CreateLibrary(db); await library.ImportAsync(new(source), null, default);
        var data = await Data(db); var file = data.Files.Single(); var manifest = Manifest(paths, file);
        File.Delete(ManagedBookFiles.SafePath(paths.DirectoryPath, missing == "source" ? file.SourceRelativePath : missing == "manifest" ? file.CanonicalRelativePath : ManagedBookFiles.BookPrefix(file.FileId) + manifest.Chapters[0].TextPath));
        await library.ReconcileAsync(default); Assert.Equal((int)EbookFileStatus.Missing, (await Data(db)).Files.Single().Status);
        Assert.Contains("Missing", (await new BookStorage(db, new ReadingService()).GetProgressAsync(file.BookId))!.ProgressText);
        var documents = new ReaderDocumentService(db, db.Coordinator); var book = data.Books.Single();
        await Assert.ThrowsAsync<InvalidOperationException>(() => documents.OpenAsync(new(book.Book.Id, book.BookUid, new(file.FileId, file.SourceSha256, file.CanonicalSha256, 1), db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1), default));
        await Assert.ThrowsAnyAsync<Exception>(() => BackupRuntime.CreateService(db, new ReadingService(), paths).ExportAsync(Path.Combine(_root, "missing.zip"), default));
        await library.DeleteBookAsync(book.Book.Id, book.BookUid, db.Coordinator.CurrentEpoch, default); Assert.Empty((await Data(db)).Books); Assert.True(File.Exists(source));
    }
    [Fact]
    public async Task UnreferencedManagedFolderIsQuarantinedWithoutDeletingItsBytes()
    {
        var paths = Paths(); using var db = new DatabaseService(paths); await db.InitializeAsync(); var id = Guid.NewGuid();
        var orphan = ManagedBookFiles.SafePath(paths.DirectoryPath, ManagedBookFiles.BookPrefix(id) + "unknown.txt"); Directory.CreateDirectory(Path.GetDirectoryName(orphan)!); await File.WriteAllTextAsync(orphan, "保留恢复材料");
        var hash = ManagedBookFiles.HashFile(orphan); await EbookRuntime.CreateLibrary(db).ReconcileAsync(default);
        Assert.False(File.Exists(orphan)); var retained = Directory.GetFiles(Path.Combine(paths.DirectoryPath, "trash"), "unknown.txt", SearchOption.AllDirectories).Single(); Assert.Equal(hash, ManagedBookFiles.HashFile(retained)); Assert.Empty(Journals(paths));
    }
    [SymlinkFact]
    public async Task SymbolicSourceAndBrokenInboxLinkAreRejectedWithoutBlockingValidInboxBook()
    {
        var paths = Paths(); var real = await Source(); var good = await Source("合法正文", "data/books/good.txt"); var link = Path.Combine(paths.DirectoryPath, "books", "link.txt"); File.CreateSymbolicLink(link, real);
        File.CreateSymbolicLink(Path.Combine(paths.DirectoryPath, "books", "broken.txt"), Path.Combine(_root, "absent.txt"));
        using var db = new DatabaseService(paths); var library = EbookRuntime.CreateLibrary(db); Assert.Equal(ImportState.Rejected, (await library.ImportAsync(new(link), null, default)).State);
        var scan = await library.ScanInboxAsync(default); Assert.Equal(1, scan.Imported); Assert.Equal(2, scan.Rejected); Assert.True(File.Exists(good)); Assert.Single((await Data(db)).Books);
    }
    [Fact]
    public async Task SourceChangingDuringCopyIsDeferredAndCanBeRetriedWithoutAlteringBytes()
    {
        var source = await Source(); var sha = ManagedBookFiles.HashFile(source); using var db = new DatabaseService(Paths()); var changed = false;
        var library = EbookRuntime.CreateLibrary(db, new ActionFiles(p => { if (p == "import.copying" && !changed) { changed = true; File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(2)); } }));
        var first = await library.ImportAsync(new(source), null, default); Assert.Equal(ImportState.Rejected, first.State); Assert.Contains("SourceChanging", first.Message); Assert.Empty((await Data(db)).Books);
        Assert.Equal(sha, ManagedBookFiles.HashFile(source)); var second = await library.ImportAsync(new(source), null, default); Assert.True(second.State == ImportState.Imported, second.Message); Assert.Equal(sha, ManagedBookFiles.HashFile(source));
    }
    [Fact]
    public async Task CachedManagedDocumentCannotReopenAfterChapterDisappears()
    {
        var source = await Source(); var paths = Paths(); using var db = new DatabaseService(paths); var result = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); var file = (await Data(db)).Files.Single();
        var documents = new ReaderDocumentService(db, db.Coordinator); var key = new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, 1);
        var identity = new ReaderOpenIdentity(file.BookId, result.BookUid!.Value, key, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1); await documents.OpenAsync(identity, default);
        var chapter = Manifest(paths, file).Chapters.First(); File.Delete(ManagedBookFiles.SafePath(paths.DirectoryPath, ManagedBookFiles.BookPrefix(file.FileId) + chapter.TextPath));
        var error = await Assert.ThrowsAnyAsync<Exception>(() => documents.OpenAsync(identity, default)); Assert.Contains("FileMissing", error.Message); await documents.ReleaseAllAsync();
    }
    [Fact]
    public async Task InboxScanStartedBeforeRestoreCannotImportIntoReplacementDataset()
    {
        var source = await Source(name: "data/books/inbox.txt"); var paths = Paths(); using var db = new DatabaseService(paths); await db.InitializeAsync();
        var zip = Path.Combine(_root, "empty.zip"); var backup = BackupRuntime.CreateService(db, new ReadingService(), paths); await backup.ExportAsync(zip, default);
        using var observed = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        var library = EbookRuntime.CreateLibrary(db, new ActionFiles(p => { if (p == "scan.observed") { observed.Set(); if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); } }));
        var scanning = library.ScanInboxAsync(default); Assert.True(await Task.Run(() => observed.Wait(TimeSpan.FromSeconds(5))));
        await backup.RestoreAsync(zip, default); resume.Set();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => scanning); Assert.Contains("StaleContext", error.Message); Assert.Empty((await Data(db)).Books); Assert.True(File.Exists(source));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class Fault(string point) : ManagedBookFiles { public override void Checkpoint(string current) { if (current == point) throw new IOException("Injected " + point); } }
    private sealed class ActionFiles(Action<string> action) : ManagedBookFiles { public override void Checkpoint(string current) => action(current); }
    private sealed class ImportBarrier : ManagedBookFiles
    {
        public CountdownEvent Ready { get; } = new(2); public ManualResetEventSlim Continue { get; } = new(); public int Prepared;
        public override void Checkpoint(string current) { if (current != "import.before-transaction") return; Interlocked.Increment(ref Prepared); Ready.Signal(); if (!Continue.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException(); }
    }
}
