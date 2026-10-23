using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using Xunit;
namespace ReadJournal.UnitTest;
public sealed class EpubImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ReadJournalP7", Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("basic")][InlineData("toc-order")][InlineData("duplicate-titles")][InlineData("nested-toc")][InlineData("image-styles")][InlineData("large-image")][InlineData("inline-style")][InlineData("unicode-path")][InlineData("epub2-ncx")][InlineData("long-chapter")][InlineData("many-images")]
    public async Task ActualAdapterImportsSpineAndStableCanonicalWithIndependentNestedNavigation(string variant)
    {
        var source = EpubTestBooks.Write(Path.Combine(_root, "source"), variant); var sha = ManagedBookFiles.HashFile(source); var hashes = new List<string>();
        foreach (var name in new[] { "first", "second" })
        {
            using var db = new DatabaseService(new DataPaths(Path.Combine(_root, name))); var result = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); Assert.True(result.State == ImportState.Imported, result.Message);
            var data = await db.ReadAsync(BackupDataStore.Snapshot); var f = Assert.Single(data.Files); Assert.Equal(EbookFormat.Epub, f.Format); Assert.Equal(sha, f.SourceSha256); Assert.Equal(sha, ManagedBookFiles.HashFile(source));
            var manifest = BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(ManagedBookFiles.SafePath(db.Paths.DirectoryPath, f.CanonicalRelativePath))); hashes.Add(f.CanonicalSha256); Assert.Equal(2, manifest.Chapters.Count); Assert.NotEqual(manifest.Chapters[0].Key, manifest.Chapters[1].Key); ManagedBookInventory.Inspect(db.Paths.DirectoryPath, data, new());
            if (variant == "toc-order") Assert.Equal(manifest.Chapters[1].Key, manifest.TableOfContents[0].ChapterKey);
            if (variant == "nested-toc") Assert.Equal(2, manifest.TableOfContents[0].Children.Count);
            if (variant == "duplicate-titles") Assert.Equal(manifest.Chapters[0].Title, manifest.Chapters[1].Title);
            if (variant.Contains("style")) Assert.Contains(manifest.Chapters.SelectMany(c => c.Blocks).SelectMany(b => b.Styles), s => s.Bold && s.Italic);
            var documents = new ReaderDocumentService(db, db.Coordinator); var book = data.Books.Single(); var key = new DocumentKey(f.FileId, f.SourceSha256, f.CanonicalSha256, 1); await documents.OpenAsync(new(book.Book.Id, book.BookUid, key, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1), default);
            var first = manifest.Chapters[0]; var last = manifest.Chapters[^1]; var text = await documents.ExtractAsync(new(new(key, first.Key, 0), new(key, last.Key, last.Utf16Length)), default); Assert.All(text.Spans, s => Assert.DoesNotContain("\ufffc", s.Text)); Assert.StartsWith(variant == "duplicate-titles" ? "同名章节" : "第一章", text.Spans[0].Text);
            Assert.Equal(100, await documents.GetPercentAsync(new(key, last.Key, last.Utf16Length), default));
            foreach (var resource in manifest.Resources) { await using var stream = await documents.OpenResourceAsync(key, resource.Key, default); Assert.True(stream.CanRead); Assert.False(stream.CanWrite); }
            await Assert.ThrowsAsync<ArgumentException>(() => documents.OpenResourceAsync(key, "../../etc/passwd", default)); await documents.ReleaseAllAsync();
        }
        Assert.Equal(hashes[0], hashes[1]);
    }
    [Theory]
    [InlineData("missing-image")][InlineData("traversal")][InlineData("remote-image")][InlineData("external-css")][InlineData("dtd")][InlineData("script")][InlineData("fixed-layout")][InlineData("drm")][InlineData("broken-xhtml")][InlineData("oversize-image")][InlineData("bad-image")][InlineData("encoded-parent")][InlineData("duplicate-path")][InlineData("symlink")][InlineData("entry-limit")]
    public async Task UnsafeUnsupportedAndCorruptBooksNeverCreateBusinessRowsOrChangeSource(string variant)
    {
        var source = EpubTestBooks.Write(Path.Combine(_root, "source"), variant); var sha = ManagedBookFiles.HashFile(source); using var db = new DatabaseService(new DataPaths(Path.Combine(_root, "data")));
        var result = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); Assert.True(result.State == ImportState.Rejected, result.Message); Assert.NotEmpty(result.Message); Assert.Equal(sha, ManagedBookFiles.HashFile(source)); Assert.Empty((await db.ReadAsync(BackupDataStore.Snapshot)).Books); Assert.False(File.Exists(Path.Combine(_root, "escape.txt")));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
