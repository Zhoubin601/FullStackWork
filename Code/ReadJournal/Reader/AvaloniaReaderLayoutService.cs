using System.Text;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
using Document = ReadJournal.Library.Models.Reader.ReaderDocument;
namespace ReadJournal.Reader;

public sealed class AvaloniaReaderLayoutService : IReaderLayoutService, IDisposable
{
    private sealed record Entry(LayoutHandle Handle, LayoutSettings Settings, Document Document, NativeLayout Native)
    { public Dictionary<string, Bitmap> Images { get; } = new(); }
    private readonly IReaderDocumentService _documents;
    private readonly Dictionary<LayoutId, Entry> _layouts = new();
    public int ActiveLayoutCount => _layouts.Count;
    public int DecodedImageCount => _layouts.Values.Sum(e => e.Images.Count);
    public AvaloniaReaderLayoutService(IReaderDocumentService documents) { _documents = documents; }
    private static Task<T> Ui<T>(Func<Task<T>> action) => Dispatcher.UIThread.CheckAccess() ? action() : Dispatcher.UIThread.InvokeAsync(action);
    private static Task<T> Ui<T>(Func<T> action) => Ui(() => Task.FromResult(action()));
    public Task<LayoutHandle> CreateAsync(DocumentKey document, LayoutSettings settings, ReaderPosition anchor, CancellationToken ct) => CreateCoreAsync(document, settings, anchor, ct);
    private async Task<LayoutHandle> CreateCoreAsync(DocumentKey document, LayoutSettings settings, ReaderPosition anchor, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ReaderContractValidator.Validate(document); ReaderContractValidator.Validate(anchor);
        if (document != anchor.Document || settings.RendererVersion != 1 || string.IsNullOrWhiteSpace(settings.FontFallbackFingerprint) ||
            settings.Appearance.FirstLineIndent != 0 || settings.Appearance.ParagraphSpacing != 0 ||
            settings.Appearance.MarginHorizontal < 0 || settings.Appearance.MarginVertical < 0 || settings.Appearance.Palette is not ("paper" or "light" or "dark"))
            throw new ArgumentException("布局配置无效；当前排版仅支持零首行附加缩进/段距，文本内缩进仍保留。");
        var options = new LayoutOptions(settings.ViewportWidth - settings.Appearance.MarginHorizontal * 2,
            settings.ViewportHeight - settings.Appearance.MarginVertical * 2, settings.Appearance.FontSize,
            settings.Appearance.LineHeight, settings.Appearance.FontFamily, settings.DpiScale, settings.Appearance.Palette == "dark" ? "#F0F0F0" : "#000000");
        options.Validate();

        var descriptor = await _documents.DescribeAsync(document, ct);
        var nativeDocument = await Task.Run(async () =>
        {
        var text = new StringBuilder(); var chapters = new List<Chapter>(); var images = new Dictionary<int, NativeImage>(); var styles = new List<NativeStyle>();
        foreach (var chapter in descriptor.Chapters)
        {
            var start = text.Length; var offset = 0; var seen = new HashSet<string>();
            while (offset < chapter.Utf16Length)
            {
                var slice = await _documents.ReadSliceAsync(document, chapter.Key, offset, 4096, ct);
                if (slice.StartOffset != offset || slice.Text.Length == 0 || slice.Text.Length > chapter.Utf16Length - offset)
                    throw new InvalidOperationException("正文片段未推进或范围不一致，请重新打开书籍。");
                foreach (var block in slice.Blocks.Where(b => seen.Add(b.Key)))
                {
                    if (block.Image is not null) images.Add(start + block.StartOffset, new NativeImage(block.Key, block.Image));
                    styles.AddRange(block.Styles.Select(s => new NativeStyle(start + s.StartOffset, s.Utf16Length, s.Bold, s.Italic, s.HeadingLevel)));
                }
                text.Append(slice.Text); offset += slice.Text.Length;
            }
            chapters.Add(new Chapter(chapter.Key, chapter.Title, start, text.Length));
        }
            ct.ThrowIfCancellationRequested(); return new NativeTextDocument(text.ToString(), chapters, images, styles);
        }, ct);
        return await Ui(() =>
        {
        ct.ThrowIfCancellationRequested();
        if (_layouts.Count >= 2) throw new InvalidOperationException("请先释放旧布局，最多同时保留两个重排上下文。");
        var native = new NativeLayout(nativeDocument, options);
        var handle = new LayoutHandle(new LayoutId(native.Id), document);
        var entry = new Entry(handle, settings, descriptor, native);
        try { native.Locate(Absolute(entry, anchor)); _layouts.Add(handle.Id, entry); return handle; }
        catch { native.Dispose(); throw; }
        });
    }
    private Entry Get(LayoutHandle handle)
    {
        var entry = Get(handle.Id);
        if (entry.Handle != handle) throw new InvalidOperationException("布局文档身份不一致。");
        return entry;
    }
    private Entry Get(LayoutId layout) => _layouts.TryGetValue(layout, out var entry) ? entry : throw new InvalidOperationException("布局已释放或失效。");
    private static int Absolute(Entry entry, ReaderPosition position)
    {
        ReaderContractValidator.Validate(position);
        var chapter = entry.Native.Document.Chapters.FirstOrDefault(c => c.Key == position.ChapterKey);
        if (position.Document != entry.Handle.Document || chapter is null || position.Utf16Offset > chapter.End - chapter.Start ||
            !entry.Native.Document.IsBoundary(chapter.Start + position.Utf16Offset)) throw new ArgumentException("正文锚点不属于当前文档或字符边界。");
        var absolute = chapter.Start + position.Utf16Offset;
        if (position.NonTextBlockKey is string block && (!entry.Native.Document.Images.TryGetValue(absolute, out var image) || image.BlockKey != block)) throw new ArgumentException("图片亲和位置无效。");
        return absolute;
    }
    private static ReaderPosition Position(Entry entry, int chapterIndex, int absolute, bool imageAffinity = false)
    {
        var chapter = entry.Native.Document.Chapters[chapterIndex];
        return new ReaderPosition(entry.Handle.Document, chapter.Key, absolute - chapter.Start, imageAffinity && entry.Native.Document.Images.TryGetValue(absolute, out var image) ? image.BlockKey : null);
    }
    private static ReaderPage ConvertPage(Entry entry, NativePage page)
    {
        var section = entry.Native.Document.Sections[page.Key.Section]; var chapter = entry.Native.Document.Chapters[section.ChapterIndex];
        return new ReaderPage(new PageRef(entry.Handle.Id, chapter.Key, entry.Native.ChapterPageNumber(page),
            section.Start - chapter.Start, page.Key.Number + 1),
            [new DocumentRange(Position(entry, section.ChapterIndex, page.Start, true), Position(entry, section.ChapterIndex, page.End))],
            entry.Native.GlobalNumber(page), page.Start == 0, page.End == entry.Native.Document.Text.Length);
    }
    private NativePage NativePage(Entry entry, PageRef page)
    {
        if (page.Layout != entry.Handle.Id || page.PageInBlock < 1) throw new ArgumentException("页引用无效。");
        var position = new ReaderPosition(entry.Handle.Document, page.ChapterKey, page.BlockStartOffset);
        var absolute = Absolute(entry, position); var section = entry.Native.Document.SectionAt(absolute);
        if (entry.Native.Document.Sections[section].Start != absolute) throw new ArgumentException("页块身份无效。");
        var native = entry.Native.GetPage(section, page.PageInBlock - 1);
        var known = entry.Native.ChapterPageNumber(native);
        if (page.PageInChapter.HasValue && known.HasValue && page.PageInChapter != known) throw new ArgumentException("章内页号不一致。");
        return native;
    }
    public Task<ReaderPage> LocateAsync(LayoutHandle layout, ReaderPosition anchor, CancellationToken ct) => Ui(async () =>
    { ct.ThrowIfCancellationRequested(); var entry = Get(layout); var page = entry.Native.Locate(Absolute(entry, anchor)); await LoadImageAsync(entry, page, ct); ct.ThrowIfCancellationRequested(); Get(layout); entry.Native.PinnedSection = page.Key.Section; return ConvertPage(entry, page); });
    public Task<ReaderPage?> GetAdjacentAsync(PageRef page, PageDirection direction, CancellationToken ct) => Ui(async () =>
    { ct.ThrowIfCancellationRequested(); var entry = Get(page.Layout); var next = entry.Native.Adjacent(NativePage(entry, page), (int)direction); if (next is null) return null; await LoadImageAsync(entry, next, ct); ct.ThrowIfCancellationRequested(); Get(entry.Handle); entry.Native.PinnedSection = next.Key.Section; return (ReaderPage?)ConvertPage(entry, next); });
    private static LayoutIndexStatus Status(Entry entry) => new(entry.Handle.Id,
        entry.Native.Document.Sections.GroupBy(s => s.ChapterIndex).Count(g => g.All(s => entry.Native.IsSectionIndexed(s.Index))),
        entry.Document.Chapters.Count, entry.Native.Status.ExactPages);
    public Task<LayoutIndexStatus> GetIndexStatusAsync(LayoutHandle layout, CancellationToken ct) => Ui(() => { ct.ThrowIfCancellationRequested(); return Status(Get(layout)); });
    public Task BuildIndexAsync(LayoutHandle layout, IProgress<LayoutIndexStatus>? progress, CancellationToken ct) => Ui(async () =>
    {
        var entry = Get(layout);
        await entry.Native.BuildIndexAsync(new Progress<IndexStatus>(_ => { if (!ct.IsCancellationRequested && _layouts.ContainsKey(layout.Id)) progress?.Report(Status(entry)); }), ct);
        ct.ThrowIfCancellationRequested(); Get(layout); progress?.Report(Status(entry)); return true;
    });
    public Task<PageRangeSnapshot> ResolveGlobalPagesAsync(LayoutHandle layout, PageSpan pages, IProgress<LayoutIndexStatus>? progress, CancellationToken ct) => Ui(async () =>
    {
        var entry = Get(layout);
        var scope = await entry.Native.ResolvePagesAsync(pages.StartInclusive, pages.EndInclusive, ct);
        ct.ThrowIfCancellationRequested(); Get(layout);
        var resolved = scope.Ranges.Select(range => ConvertPage(entry, entry.Native.Locate(range.Start))).ToArray();
        var spans = new List<TextSpan>();
        foreach (var range in resolved.SelectMany(p => p.ContentRanges)) spans.AddRange((await _documents.ExtractAsync(range, ct)).Spans);
        ct.ThrowIfCancellationRequested(); Get(layout); progress?.Report(Status(entry));
        return new PageRangeSnapshot(layout.Id, pages, Array.AsReadOnly(resolved), new TextSnapshot(layout.Document, spans.AsReadOnly(),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(spans.Select(s => s.Text))))).ToLowerInvariant()));
    });
    private async Task LoadImageAsync(Entry entry, NativePage page, CancellationToken ct)
    {
        if (!entry.Native.Document.Images.TryGetValue(page.Start, out var image) || entry.Images.ContainsKey(image.Resource.Key)) return;
        if ((long)image.Resource.PixelWidth * image.Resource.PixelHeight > 25000000) throw new ArgumentException("LimitExceeded：图片像素预算超限。");
        await using var stream = await _documents.OpenResourceAsync(entry.Handle.Document, image.Resource.Key, ct);
        ct.ThrowIfCancellationRequested(); Get(entry.Handle);
        Bitmap bitmap;
        try { bitmap = Bitmap.DecodeToWidth(stream, Math.Max(1, Math.Min(image.Resource.PixelWidth, (int)Math.Ceiling(entry.Native.Options.Width * entry.Settings.DpiScale)))); }
        catch (Exception ex) { throw new ArgumentException("InvalidImage：图片完整解码失败，无法安全呈现。", ex); }
        if ((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height > 25000000) { bitmap.Dispose(); throw new ArgumentException("LimitExceeded：图片解码后超出像素预算。"); }
        if (entry.Images.Count >= 4) { var old = entry.Images.Keys.First(); entry.Images[old].Dispose(); entry.Images.Remove(old); }
        entry.Images.Add(image.Resource.Key, bitmap);
    }
    internal Bitmap? Image(PageRef page)
    {
        var entry = Get(page.Layout); var native = NativePage(entry, page);
        return entry.Native.Document.Images.TryGetValue(native.Start, out var image) && entry.Images.TryGetValue(image.Resource.Key, out var bitmap) ? bitmap : null;
    }
    public Task<ReaderPosition?> HitTestAsync(PageRef page, PointD point, CancellationToken ct) => Ui(() =>
    {
        ct.ThrowIfCancellationRequested();
        if (!double.IsFinite(point.X + point.Y)) throw new ArgumentException("命中坐标无效。");
        var entry = Get(page.Layout); var native = NativePage(entry, page);
        var hit = entry.Native.Hit(native, new Point(point.X, point.Y));
        return (ReaderPosition?)Position(entry, entry.Native.Document.Sections[native.Key.Section].ChapterIndex, hit, hit == native.Start && entry.Native.Document.Images.ContainsKey(hit));
    });
    public Task<IReadOnlyList<RectD>> GetSelectionRectsAsync(PageRef page, DocumentRange selection, CancellationToken ct) => Ui(() =>
    {
        ct.ThrowIfCancellationRequested(); ReaderContractValidator.Validate(selection); var entry = Get(page.Layout);
        var a = Absolute(entry, selection.Start); var b = Absolute(entry, selection.End);
        if (a >= b) throw new ArgumentException("选区逆序或为空。");
        return (IReadOnlyList<RectD>)entry.Native.SelectionRects(NativePage(entry, page), a, b).Select(r => new RectD(r.X, r.Y, r.Width, r.Height)).ToArray();
    });
    internal (NativeLayout Layout, NativePage Page, LayoutSettings Settings) RenderPlan(PageRef page)
    { Dispatcher.UIThread.VerifyAccess(); var entry = Get(page.Layout); return (entry.Native, NativePage(entry, page), entry.Settings); }
    public Task ReleaseAsync(LayoutHandle layout) => Ui(() =>
    { if (_layouts.TryGetValue(layout.Id, out var entry)) { if (entry.Handle != layout) throw new ArgumentException("布局文档不一致。"); _layouts.Remove(layout.Id); entry.Native.Dispose(); foreach (var image in entry.Images.Values) image.Dispose(); entry.Images.Clear(); } return true; });
    public Task ReleaseAllAsync() => Ui(() => { Dispose(); return true; });
    public void Dispose() { Dispatcher.UIThread.VerifyAccess(); foreach (var entry in _layouts.Values) { entry.Native.Dispose(); foreach (var image in entry.Images.Values) image.Dispose(); entry.Images.Clear(); } _layouts.Clear(); }
}
