using System.Text;
using Avalonia;
using Avalonia.Threading;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
using Document = ReadJournal.Library.Models.Reader.ReaderDocument;
namespace ReadJournal.Reader;

public sealed class AvaloniaReaderLayoutService : IReaderLayoutService, IDisposable
{
    private sealed record Entry(LayoutHandle Handle, LayoutSettings Settings, Document Document, NativeLayout Native);
    private readonly IReaderDocumentService _documents;
    private readonly Dictionary<LayoutId, Entry> _layouts = new();
    public AvaloniaReaderLayoutService(IReaderDocumentService documents) { _documents = documents; }
    private static Task<T> Ui<T>(Func<Task<T>> action) => Dispatcher.UIThread.CheckAccess() ? action() : Dispatcher.UIThread.InvokeAsync(action);
    private static Task<T> Ui<T>(Func<T> action) => Ui(() => Task.FromResult(action()));
    public Task<LayoutHandle> CreateAsync(DocumentKey document, LayoutSettings settings, ReaderPosition anchor, CancellationToken ct) => Ui(async () =>
    {
        ct.ThrowIfCancellationRequested(); ReaderContractValidator.Validate(document); ReaderContractValidator.Validate(anchor);
        if (document != anchor.Document || settings.RendererVersion != 1 || string.IsNullOrWhiteSpace(settings.FontFallbackFingerprint) ||
            settings.Appearance.FirstLineIndent != 0 || settings.Appearance.ParagraphSpacing != 0 ||
            settings.Appearance.MarginHorizontal < 0 || settings.Appearance.MarginVertical < 0 || settings.Appearance.Palette is not ("paper" or "light" or "dark"))
            throw new ArgumentException("布局配置无效；P2 引擎暂仅支持零首行附加缩进/段距，文本内缩进仍保留。");
        var options = new LayoutOptions(settings.ViewportWidth - settings.Appearance.MarginHorizontal * 2,
            settings.ViewportHeight - settings.Appearance.MarginVertical * 2, settings.Appearance.FontSize,
            settings.Appearance.LineHeight, settings.Appearance.FontFamily, settings.DpiScale, settings.Appearance.Palette == "dark" ? "#F0F0F0" : "#000000");
        options.Validate();
        if (_layouts.Count >= 2) throw new InvalidOperationException("请先释放旧布局，最多同时保留两个重排上下文。");
        var descriptor = await _documents.DescribeAsync(document, ct);
        var text = new StringBuilder(); var chapters = new List<Chapter>();
        foreach (var chapter in descriptor.Chapters)
        {
            var start = text.Length; var offset = 0;
            while (offset < chapter.Utf16Length)
            {
                var slice = await _documents.ReadSliceAsync(document, chapter.Key, offset, 4096, ct);
                if (slice.StartOffset != offset || slice.Text.Length == 0 || slice.Text.Length > chapter.Utf16Length - offset || slice.Text.Contains('\ufffc'))
                    throw new InvalidOperationException("正文片段无进展、范围不一致或含 P7 才支持的图片。");
                text.Append(slice.Text); offset += slice.Text.Length;
            }
            chapters.Add(new Chapter(chapter.Key, chapter.Title, start, text.Length));
        }
        ct.ThrowIfCancellationRequested();
        var native = new NativeLayout(new NativeTextDocument(text.ToString(), chapters), options);
        var handle = new LayoutHandle(new LayoutId(native.Id), document);
        var entry = new Entry(handle, settings, descriptor, native);
        try { native.Locate(Absolute(entry, anchor)); _layouts.Add(handle.Id, entry); return handle; }
        catch { native.Dispose(); throw; }
    });
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
        if (position.Document != entry.Handle.Document || chapter is null || position.NonTextBlockKey is not null || position.Utf16Offset > chapter.End - chapter.Start ||
            !entry.Native.Document.IsBoundary(chapter.Start + position.Utf16Offset)) throw new ArgumentException("正文锚点不属于当前文档或字符边界。");
        return chapter.Start + position.Utf16Offset;
    }
    private static ReaderPosition Position(Entry entry, int chapterIndex, int absolute)
    {
        var chapter = entry.Native.Document.Chapters[chapterIndex];
        return new ReaderPosition(entry.Handle.Document, chapter.Key, absolute - chapter.Start);
    }
    private static ReaderPage ConvertPage(Entry entry, NativePage page)
    {
        var section = entry.Native.Document.Sections[page.Key.Section]; var chapter = entry.Native.Document.Chapters[section.ChapterIndex];
        return new ReaderPage(new PageRef(entry.Handle.Id, chapter.Key, entry.Native.ChapterPageNumber(page),
            section.Start - chapter.Start, page.Key.Number + 1),
            [new DocumentRange(Position(entry, section.ChapterIndex, page.Start), Position(entry, section.ChapterIndex, page.End))],
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
    public Task<ReaderPage> LocateAsync(LayoutHandle layout, ReaderPosition anchor, CancellationToken ct) => Ui(() =>
    { ct.ThrowIfCancellationRequested(); var entry = Get(layout); var page = entry.Native.Locate(Absolute(entry, anchor)); entry.Native.PinnedSection = page.Key.Section; return ConvertPage(entry, page); });
    public Task<ReaderPage?> GetAdjacentAsync(PageRef page, PageDirection direction, CancellationToken ct) => Ui(() =>
    { ct.ThrowIfCancellationRequested(); var entry = Get(page.Layout); var next = entry.Native.Adjacent(NativePage(entry, page), (int)direction); if (next is null) return null; entry.Native.PinnedSection = next.Key.Section; return (ReaderPage?)ConvertPage(entry, next); });
    private static LayoutIndexStatus Status(Entry entry) => new(entry.Handle.Id,
        entry.Native.Document.Sections.GroupBy(s => s.ChapterIndex).Count(g => g.All(s => entry.Native.IsSectionIndexed(s.Index))),
        entry.Document.Chapters.Count, entry.Native.Status.ExactPages);
    public Task<LayoutIndexStatus> GetIndexStatusAsync(LayoutHandle layout, CancellationToken ct) => Ui(() => { ct.ThrowIfCancellationRequested(); return Status(Get(layout)); });
    public Task<PageRangeSnapshot> ResolveGlobalPagesAsync(LayoutHandle layout, PageSpan pages, IProgress<LayoutIndexStatus>? progress, CancellationToken ct) => Ui(async () =>
    {
        var entry = Get(layout);
        var scope = await entry.Native.ResolvePagesAsync(pages.StartInclusive, pages.EndInclusive, ct);
        ct.ThrowIfCancellationRequested(); Get(layout);
        var resolved = scope.Ranges.Select(range => ConvertPage(entry, entry.Native.Locate(range.Start))).ToArray();
        var spans = resolved.SelectMany(p => p.ContentRanges).Select(range => new TextSpan(range,
            entry.Native.Document.Text[Absolute(entry, range.Start)..Absolute(entry, range.End)])).ToArray();
        progress?.Report(Status(entry));
        return new PageRangeSnapshot(layout.Id, pages, Array.AsReadOnly(resolved),
            new TextSnapshot(layout.Document, Array.AsReadOnly(spans), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(scope.Text))).ToLowerInvariant()));
    });
    public Task<ReaderPosition?> HitTestAsync(PageRef page, PointD point, CancellationToken ct) => Ui(() =>
    {
        ct.ThrowIfCancellationRequested();
        if (!double.IsFinite(point.X + point.Y)) throw new ArgumentException("命中坐标无效。");
        var entry = Get(page.Layout); var native = NativePage(entry, page);
        return (ReaderPosition?)Position(entry, entry.Native.Document.Sections[native.Key.Section].ChapterIndex, entry.Native.Hit(native, new Point(point.X, point.Y)));
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
    { if (_layouts.TryGetValue(layout.Id, out var entry)) { if (entry.Handle != layout) throw new ArgumentException("布局文档不一致。"); _layouts.Remove(layout.Id); entry.Native.Dispose(); } return true; });
    public Task ReleaseAllAsync() => Ui(() => { Dispose(); return true; });
    public void Dispose() { Dispatcher.UIThread.VerifyAccess(); foreach (var entry in _layouts.Values) entry.Native.Dispose(); _layouts.Clear(); }
}
