using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Reader;
using Xunit;
namespace ReadJournal.UnitTest;
public sealed class EpubRenderingTests
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(EpubRenderingTests)));
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ReaderLayoutContractTests.LayoutTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
    private static Task Ui(Func<Task> action) => Session.Value.Dispatch(async () => { await action(); return true; }, default);
    private static LayoutSettings Settings(double width = 500) => new(width, 350, 1, ReaderPreferencesStorage.Default, 1, "p7-native-test");
    [Theory][InlineData("large-image")][InlineData("many-images")]
    public Task NativeImagesDecodeScalePaginateAndReleaseBoundedCaches(string variant) => Ui(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "ReadJournalP7Render", Guid.NewGuid().ToString("N"));
        try
        {
            var source = EpubTestBooks.Write(Path.Combine(root, "source"), variant); using var db = new DatabaseService(new DataPaths(Path.Combine(root, "data"))); var imported = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); Assert.True(imported.State == ImportState.Imported, imported.Message);
            var data = await db.ReadAsync(BackupDataStore.Snapshot); var file = data.Files.Single(); var book = data.Books.Single(); var key = ReaderContractValidator.ReadDocument(data.Progress.Single().DocumentKeyJson);
            var docs = new ReaderDocumentService(db, db.Coordinator); var document = await docs.OpenAsync(new(book.Book.Id, book.BookUid, key, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1), default);
            using var layouts = new AvaloniaReaderLayoutService(docs); var handle = await layouts.CreateAsync(key, Settings(), new(key, document.Chapters[0].Key, 0), default); var page = await layouts.LocateAsync(handle, new(key, document.Chapters[0].Key, 0), default); var images = 0; var pages = 0; ReaderPage? firstImage = null;
            while (true)
            {
                pages++; var content = await docs.ExtractAsync(page.ContentRanges[0], default);
                if (content.Spans.Count == 0)
                {
                    images++; firstImage ??= page; Assert.NotNull(page.ContentRanges[0].Start.NonTextBlockKey); Assert.InRange(layouts.DecodedImageCount, 1, 4);
                    Assert.Equal(await docs.GetPercentAsync(page.ContentRanges[0].Start, default), await docs.GetPercentAsync(page.ContentRanges[0].End, default));
                    Assert.Empty(await layouts.GetSelectionRectsAsync(page.Ref, page.ContentRanges[0], default)); var hit = await layouts.HitTestAsync(page.Ref, new(0, 0), default); Assert.Equal(page.ContentRanges[0].Start, hit);
                }
                var next = await layouts.GetAdjacentAsync(page.Ref, PageDirection.Next, default); if (next is null) break;
                if (content.Spans.Count > 0 && next.ContentRanges[0].Start.NonTextBlockKey is not null) { Assert.Null(page.ContentRanges[0].End.NonTextBlockKey); Assert.Null((await layouts.HitTestAsync(page.Ref, new(10000, 10000), default))!.NonTextBlockKey); }
                page = next; Assert.InRange(pages, 1, 200);
            }
            Assert.Equal(variant == "many-images" ? 12 : 1, images); Assert.Equal(pages, (await layouts.GetIndexStatusAsync(handle, default)).ExactGlobalPageCount);
            Assert.NotNull(firstImage); page = await layouts.LocateAsync(handle, firstImage!.ContentRanges[0].Start, default);
            var surface = new ReaderSurface { LayoutService = layouts, Page = page, Width = 500, Height = 350 }; var window = new Window { Content = surface, Width = 500, Height = 350 };
            try { window.Show(); surface.Measure(new Size(500, 350)); surface.Arrange(new Rect(0, 0, 500, 350)); using var bitmap = new RenderTargetBitmap(new PixelSize(500, 350)); bitmap.Render(surface); Assert.True(layouts.DecodedImageCount > 0); }
            finally { window.Close(); }
            var frozen = await layouts.ResolveGlobalPagesAsync(handle, new(1, pages), null, default); Assert.All(frozen.Text.Spans, s => Assert.DoesNotContain("\ufffc", s.Text));
            await layouts.ReleaseAllAsync(); Assert.Equal(0, layouts.ActiveLayoutCount); Assert.Equal(0, layouts.DecodedImageCount); await docs.ReleaseAllAsync();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });
    [Fact]
    public Task EpubQuoteReflowSessionAndBackupRestoreUseSameCanonicalIdentity() => Ui(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "ReadJournalP7Flow", Guid.NewGuid().ToString("N"));
        try
        {
            var source = EpubTestBooks.Write(Path.Combine(root, "source"), "image-styles"); using var db = new DatabaseService(new DataPaths(Path.Combine(root, "data"))); var imported = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); Assert.Equal(ImportState.Imported, imported.State);
            var initial = await db.ReadAsync(BackupDataStore.Snapshot); var b = initial.Books.Single(); var f = initial.Files.Single(); var key = ReaderContractValidator.ReadDocument(initial.Progress.Single().DocumentKeyJson); var docs = new ReaderDocumentService(db, db.Coordinator); var identity = new ReaderOpenIdentity(b.Book.Id, b.BookUid, key, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1); var document = await docs.OpenAsync(identity, default);
            var start = new ReaderPosition(key, document.Chapters[0].Key, 0); var sessions = new ReaderSessionService(db, db.Coordinator, docs, new EbookProgressMaintenance(db, db.Coordinator), new ReaderClock(), new ReaderSessionCheckpoints()); var now = DateTimeOffset.Now; var context = await sessions.BeginAsync(identity, start, now, default);
            var quotes = new ReaderQuoteService(db, db.Coordinator, docs, sessions, new ReadingService(), new ReaderQuoteCheckpoints()); var range = new DocumentRange(start, start with { Utf16Offset = 5 }); var quoteId = await quotes.SaveSelectionAsync(context, range, "EPUB实际书摘", default);
            var manifest = BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(ManagedBookFiles.SafePath(db.Paths.DirectoryPath, f.CanonicalRelativePath))); var image = manifest.Chapters[0].Blocks.Single(b => b.Kind == ReaderBlockKind.Image);
            var acrossImage = new DocumentRange(start with { Utf16Offset = image.StartOffset - 2 }, start with { Utf16Offset = image.StartOffset + 3 }); Assert.Equal(2, (await docs.ExtractAsync(acrossImage, default)).Spans.Count); await Assert.ThrowsAsync<ArgumentException>(() => quotes.SaveSelectionAsync(context, acrossImage, "", default));
            using var layouts = new AvaloniaReaderLayoutService(docs); var handle = await layouts.CreateAsync(key, Settings(), range.Start, default); await layouts.LocateAsync(handle, range.Start, default); await layouts.ReleaseAsync(handle); var reflow = await layouts.CreateAsync(key, Settings(390), range.Start, default); Assert.Equal(range.Start, (await quotes.ResolveQuoteAsync(quoteId, default)).Position); await layouts.ReleaseAsync(reflow);
            await sessions.MarkCompletedAsync(context, default); await sessions.CloseAsync(new(context, start, range.End, now, now.AddMilliseconds(1750), false, ReaderExitReason.WindowClose), default); await docs.ReleaseAllAsync();
            var before = await db.ReadAsync(BackupDataStore.Snapshot); var inventory = ManagedBookInventory.Inspect(db.Paths.DirectoryPath, before, new()); var backup = BackupRuntime.CreateService(db, new ReadingService(), db.Paths); var zip = Path.Combine(root, "epub.zip"); await backup.ExportAsync(zip, default);
            await EbookRuntime.CreateLibrary(db).DeleteBookAsync(b.Book.Id, b.BookUid, db.Coordinator.CurrentEpoch, default); await backup.RestoreAsync(zip, default); Assert.Equal(BackupDataStore.Hash(before), BackupDataStore.Hash(await db.ReadAsync(BackupDataStore.Snapshot))); Assert.True(ManagedBookInventory.Same(inventory, ManagedBookInventory.Inspect(db.Paths.DirectoryPath, await db.ReadAsync(BackupDataStore.Snapshot), new()))); Assert.True((await quotes.ResolveQuoteAsync(quoteId, default)).IsExact); Assert.Equal("已完成", (await new BookStorage(db, new ReadingService()).GetProgressAsync(b.Book.Id))!.State);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });
}
