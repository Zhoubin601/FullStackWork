using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
using Document = ReadJournal.Library.Models.Reader.ReaderDocument;
namespace ReadJournal.Library.Services.Reader;

// Synthetic publication remains for layout fixtures; managed TXT loads immutable canonical chapters on demand.
public sealed class ReaderDocumentService : IReaderDocumentService
{
    private sealed class Entry(Document document, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<string, int[]> boundaries,
        CanonicalManifest? manifest = null, string? managedDirectory = null)
    {
        public Document Document => document;
        public bool IsManaged => manifest is not null;
        public IReadOnlyDictionary<string, string> Text => text;
        private readonly Dictionary<string, (string Text, int[] Boundaries)> _cache = new();
        public bool TryText(string key, out string value)
        {
            lock (_cache)
            {
                if (manifest is null) return text.TryGetValue(key, out value!);
                var chapter = manifest.Chapters.FirstOrDefault(c => c.Key == key);
                if (chapter is null) { value = ""; return false; }
                if (!_cache.TryGetValue(key, out var cached))
                {
                    var path = ManagedBookFiles.SafePath(managedDirectory!, chapter.TextPath);
                    if (!File.Exists(path) || new FileInfo(path).Length != (long)chapter.Utf16Length * 2 || ManagedBookFiles.HashFile(path) != chapter.Sha256) throw new InvalidOperationException("FileMissing/DocumentMismatch：章节缺失或已改变，请刷新书库。");
                    value = new UnicodeEncoding(false, false, true).GetString(File.ReadAllBytes(path));
                    cached = (value, StringInfo.ParseCombiningCharacters(value).Append(value.Length).ToArray());
                    if (_cache.Count >= 2) _cache.Remove(_cache.Keys.First());
                    _cache.Add(key, cached);
                }
                value = cached.Text; return true;
            }
        }
        public string Value(string key) => TryText(key, out var value) ? value : throw new ArgumentException("章节不存在。");
        public int[] Boundaries(string key)
        { if (manifest is null) return boundaries[key]; lock (_cache) { Value(key); return _cache[key].Boundaries; } }
        public IReadOnlyList<ReaderBlockDescriptor> Blocks(string key) => manifest?.Chapters.Single(c => c.Key == key).Blocks ?? [new(key, ReaderBlockKind.Paragraph, 0, Value(key).Length, null, [])];
    }
    private readonly object _sync = new();
    private readonly Dictionary<DocumentKey, Entry> _documents = new();
    private readonly IDatabaseService _database;
    private readonly IApplicationDataCoordinator _coordinator;
    private readonly DataPaths? _paths;
    public ReaderDocumentService(IDatabaseService database, IApplicationDataCoordinator coordinator) { _database = database; _coordinator = coordinator; _paths = (database as DatabaseService)?.Paths; }
    public ReaderDocumentService(IDatabaseService database, IApplicationDataCoordinator coordinator, DataPaths paths) : this(database, coordinator) { _paths = paths; }
    public void PublishText(Document document, IReadOnlyDictionary<string, string> chapterText)
    {
        ReaderContractValidator.Validate(document.Key);
        if (document.Chapters.Count == 0 || document.Chapters.Select(c => c.Key).Distinct().Count() != document.Chapters.Count ||
            !document.Chapters.Select(c => c.Order).SequenceEqual(Enumerable.Range(0, document.Chapters.Count)) ||
            document.TotalTextUnits <= 0 || document.TotalTextUnits > 20_000_000 || document.Chapters.Count != chapterText.Count)
            throw new ArgumentException("章节清单无效或超出 P2 文本预算。");
        var text = new Dictionary<string, string>(); var boundaries = new Dictionary<string, int[]>();
        foreach (var chapter in document.Chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.Key) || !chapterText.TryGetValue(chapter.Key, out var value) || value.Length != chapter.Utf16Length || value.Length == 0 || value.Contains('\r') || value.Contains('\ufffc'))
                throw new ArgumentException("P2 章节必须为已规范化非空纯文本，图片由 P7 实现。");
            var offsets = StringInfo.ParseCombiningCharacters(value).Append(value.Length).ToArray();
            if (offsets.Zip(offsets.Skip(1), (a, b) => b - a).Any(n => n > 1024)) throw new ArgumentException("单字符簇超限。");
            text.Add(chapter.Key, value); boundaries.Add(chapter.Key, offsets);
        }
        if (document.TotalTextUnits != text.Values.Sum(v => (long)v.Length)) throw new ArgumentException("正文长度不一致。");
        // Caller collections cannot mutate stored contracts after publication.
        var copy = document with { Chapters = Array.AsReadOnly(document.Chapters.ToArray()), TableOfContents = Array.AsReadOnly(document.TableOfContents.Select(CloneToc).ToArray()) };
        lock (_sync)
        {
            if (_documents.TryGetValue(document.Key, out var old) && !old.Text.OrderBy(x => x.Key).SequenceEqual(text.OrderBy(x => x.Key)))
                throw new ArgumentException("不能替换同一文档身份的正文。");
            if (!_documents.ContainsKey(document.Key) && _documents.Count >= 2) throw new InvalidOperationException("请先释放旧文档，文本目录最多保留两本文档。");
            _documents[document.Key] = new(copy, text, boundaries);
        }
    }
    private static TocEntry CloneToc(TocEntry entry) => entry with { Children = Array.AsReadOnly(entry.Children.Select(CloneToc).ToArray()) };
    private Entry Get(DocumentKey key)
    {
        ReaderContractValidator.Validate(key);
        lock (_sync) return _documents.TryGetValue(key, out var entry) ? entry : throw new InvalidOperationException("文档尚未加载或已释放。");
    }
    public Task<Document> DescribeAsync(DocumentKey document, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Get(document).Document); }
    public async Task<Document> OpenAsync(ReaderOpenIdentity identity, CancellationToken ct)
    {
        await _database.InitializeAsync();
        await using var lease = await _coordinator.EnterOperationAsync(identity.Epoch, ct);
        var data = await _database.ReadAsync(lease, c =>
        {
            ct.ThrowIfCancellationRequested();
            var book = c.Find<Book>(identity.BookId); var file = c.Find<EbookFile>(identity.BookId);
            if (book?.BookUid != identity.BookUid || book.Kind != BookKind.Ebook || file is null ||
                new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, file.ParserVersion) != identity.Document)
                throw new InvalidOperationException("StaleContext：书籍或文档身份不匹配。");
            if (file.Status != (int)EbookFileStatus.Ready) throw new InvalidOperationException("FileMissing：书籍原文未就绪，请恢复备份或重新导入。");
            return BookFileOperations.Single(BackupDataStore.Snapshot(c), identity.BookId);
        });
        bool loaded; lock (_sync) loaded = _documents.ContainsKey(identity.Document);
        if (loaded && Get(identity.Document).IsManaged)
            await Task.Run(() => ManagedBookInventory.Inspect(_paths!.DirectoryPath, data, new(), false), ct);
        if (!loaded)
        {
            if (_paths is null) throw new InvalidOperationException("受管书库路径未配置。");
            var file = data.Files.Single();
            var entry = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested(); ManagedBookInventory.Inspect(_paths.DirectoryPath, data, new(), false);
                var manifest = BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(ManagedBookFiles.SafePath(_paths.DirectoryPath, file.CanonicalRelativePath)));
                if (file.Format != EbookFormat.Txt || manifest.Resources.Count != 0) throw new ArgumentException("当前文档服务仅开放受管TXT；EPUB由P7实现。");
                TocEntry Toc(CanonicalTocEntry t) => new(t.Key, t.Title, new(identity.Document, t.ChapterKey, t.Utf16Offset, t.NonTextBlockKey), t.Children.Select(Toc).ToArray());
                var document = new Document(identity.Document, manifest.Title, manifest.Chapters.Select(c => new ChapterDescriptor(c.Key, c.Title, c.Order, c.Utf16Length)).ToArray(), manifest.TableOfContents.Select(Toc).ToArray(), manifest.TotalTextUnits);
                return new Entry(document, new Dictionary<string, string>(), new Dictionary<string, int[]>(), manifest, ManagedBookFiles.SafePath(_paths.DirectoryPath, ManagedBookFiles.BookPrefix(file.FileId).TrimEnd('/')));
            }, ct);
            lock (_sync)
            {
                if (!_documents.ContainsKey(identity.Document) && _documents.Count >= 2) throw new InvalidOperationException("请先释放旧文档，目录最多保留两本。");
                _documents[identity.Document] = entry;
            }
        }
        ct.ThrowIfCancellationRequested(); return Get(identity.Document).Document;
    }
    private static int Snap(int[] boundaries, int value, bool forward)
    { var index = Array.BinarySearch(boundaries, value); return index >= 0 ? value : boundaries[forward ? ~index : ~index - 1]; }
    private static int ValidatePosition(Entry entry, ReaderPosition position)
    {
        ReaderContractValidator.Validate(position);
        if (position.Document != entry.Document.Key || position.NonTextBlockKey is not null || !entry.TryText(position.ChapterKey, out var text) ||
            position.Utf16Offset > text.Length || Array.BinarySearch(entry.Boundaries(position.ChapterKey), position.Utf16Offset) < 0)
            throw new ArgumentException("位置不是该文档的字符簇边界。");
        return entry.Document.Chapters.First(c => c.Key == position.ChapterKey).Order;
    }
    public Task<ChapterSlice> ReadSliceAsync(DocumentKey document, string chapterKey, int startOffset, int maxUtf16Units, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var entry = Get(document);
        if (!entry.TryText(chapterKey, out var text) || startOffset < 0 || startOffset > text.Length || maxUtf16Units < 1 || maxUtf16Units > 65536)
            throw new ArgumentException("正文片段范围无效。");
        var boundaries = entry.Boundaries(chapterKey); var start = Snap(boundaries, startOffset, false);
        var end = Snap(boundaries, Math.Min(text.Length, start + maxUtf16Units), false);
        if (end == start && start < text.Length) end = Snap(boundaries, start + 1, true);
        return Task.FromResult(new ChapterSlice(chapterKey, start, text[start..end],
            entry.Blocks(chapterKey)));
    }
    public Task<TextSnapshot> ExtractAsync(DocumentRange range, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ReaderContractValidator.Validate(range); var entry = Get(range.Start.Document);
        var first = ValidatePosition(entry, range.Start); var last = ValidatePosition(entry, range.End);
        if (last < first) throw new ArgumentException("正文范围逆序。");
        var spans = new List<TextSpan>();
        foreach (var chapter in entry.Document.Chapters.Skip(first).Take(last - first + 1))
        {
            var start = chapter.Order == first ? range.Start.Utf16Offset : 0;
            var end = chapter.Order == last ? range.End.Utf16Offset : chapter.Utf16Length;
            if (end > start) spans.Add(new TextSpan(new DocumentRange(new(entry.Document.Key, chapter.Key, start), new(entry.Document.Key, chapter.Key, end)), entry.Value(chapter.Key)[start..end]));
        }
        if (spans.Count == 0) throw new ArgumentException("范围为空。");
        return Task.FromResult(new TextSnapshot(entry.Document.Key, Array.AsReadOnly(spans.ToArray()), Hash(string.Concat(spans.Select(s => s.Text)))));
    }
    public Task<Stream> OpenResourceAsync(DocumentKey document, string resourceKey, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Get(document); throw new ArgumentException("P2 文本文档没有声明图片资源。"); }
    public Task<double> GetPercentAsync(ReaderPosition position, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var entry = Get(position.Document); var order = ValidatePosition(entry, position);
        return Task.FromResult(100.0 * (entry.Document.Chapters.Take(order).Sum(c => (long)c.Utf16Length) + position.Utf16Offset) / entry.Document.TotalTextUnits);
    }
    public async Task<PositionResolution> ResolveAsync(ReaderPosition position, string? expectedOriginalText, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var entry = Get(position.Document); ValidatePosition(entry, position);
            if (expectedOriginalText is not null && !entry.Value(position.ChapterKey)[position.Utf16Offset..].StartsWith(expectedOriginalText, StringComparison.Ordinal))
                return new(false, null, "原文不匹配，拒绝猜测位置。");
            return await Task.FromResult(new PositionResolution(true, position, null));
        }
        catch (ArgumentException ex) { return new(false, null, ex.Message); }
    }
    public Task ReleaseAllAsync() { lock (_sync) _documents.Clear(); return Task.CompletedTask; }
    public Task ReleaseAsync(DocumentKey document) { lock (_sync) _documents.Remove(document); return Task.CompletedTask; }
    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
