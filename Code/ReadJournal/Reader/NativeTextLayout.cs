using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace ReadJournal.Reader;

internal sealed record LayoutOptions(double Width, double Height, double FontSize = 22,
    double LineHeight = 34, string FontFamily = "Noto Sans CJK SC", double DpiScale = 1, string Foreground = "#000000")
{
    public void Validate()
    {
        if (!double.IsFinite(Width + Height + FontSize + LineHeight + DpiScale) ||
            Width < 100 || Height < LineHeight || FontSize < 10 || FontSize > 72 || LineHeight < FontSize ||
            Width > 4000 || Height > 4000 || DpiScale <= 0 || string.IsNullOrWhiteSpace(FontFamily))
            throw new ArgumentException("视口、字体、行高或缩放无效。");
    }
}
internal sealed record PageKey(Guid Layout, int Section, int Number);
internal sealed record NativePage(PageKey Key, int Start, int End, int FirstLine, int LineCount);
internal sealed record IndexStatus(int IndexedSections, int TotalSections, int? ExactPages);
internal sealed record ScopeSnapshot(Guid Layout, string DocumentHash, int FirstPage, int LastPage,
    IReadOnlyList<(int Start, int End)> Ranges, string Text);

internal sealed class NativeLayout : IDisposable
{
    private sealed record Entry(TextLayout? Layout, IReadOnlyList<NativePage> Pages);
    public Guid Id { get; } = Guid.NewGuid();
    public NativeTextDocument Document { get; }
    public LayoutOptions Options { get; }
    private readonly Dictionary<int, Entry> _cache = new();
    private readonly LinkedList<int> _lru = new();
    private readonly Dictionary<int, IReadOnlyList<NativePage>> _index = new();
    private bool _disposed;
    public int PinnedSection { get; set; } = -1;
    public int CacheLimit { get; } = 8;
    public int CachedSections => _cache.Count;
    public IndexStatus Status => new(_index.Count, Document.Sections.Count,
        _index.Count == Document.Sections.Count ? _index.Values.Sum(p => p.Count) : null);

    public NativeLayout(NativeTextDocument document, LayoutOptions options)
    {
        options.Validate(); Document = document; Options = options;
    }
    private Entry Ensure(int sectionIndex)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sectionIndex < 0 || sectionIndex >= Document.Sections.Count) throw new ArgumentOutOfRangeException(nameof(sectionIndex));
        if (_cache.TryGetValue(sectionIndex, out var cached))
        {
            _lru.Remove(sectionIndex); _lru.AddFirst(sectionIndex); return cached;
        }
        var section = Document.Sections[sectionIndex];
        if (Document.Images.ContainsKey(section.Start))
        {
            var imagePages = new[] { new NativePage(new PageKey(Id, sectionIndex, 0), section.Start, section.End, 0, 0) };
            _index[sectionIndex] = imagePages; return Install(sectionIndex, new Entry(null, imagePages));
        }
        var text = Document.Text[section.Start..section.End];
        var overrides = new List<ValueSpan<TextRunProperties>>(); var relevant = Document.Styles(sectionIndex);
        var events = new SortedDictionary<int, List<(NativeStyle Style, int Delta)>>();
        foreach (var style in relevant)
        {
            var left = Math.Max(section.Start, style.Start); var right = Math.Min(section.End, style.Start + style.Length);
            if (!events.TryGetValue(left, out var begin)) events.Add(left, begin = []); begin.Add((style, 1));
            if (!events.TryGetValue(right, out var end)) events.Add(right, end = []); end.Add((style, -1));
        }
        var cuts = events.Keys.ToArray(); var boldCount = 0; var italicCount = 0; var headings = new int[7];
        for (var i = 0; i + 1 < cuts.Length; i++)
        {
            foreach (var (style, delta) in events[cuts[i]]) { if (style.Bold) boldCount += delta; if (style.Italic) italicCount += delta; headings[style.Heading] += delta; }
            var start = cuts[i]; var end = cuts[i + 1]; var heading = Enumerable.Range(1, 6).LastOrDefault(h => headings[h] > 0);
            var bold = boldCount > 0 || heading > 0; var italic = italicCount > 0; if (!bold && !italic && heading == 0) continue;
            var typeface = new Typeface(new FontFamily($"{Options.FontFamily}, Noto Sans CJK SC, Microsoft YaHei, DejaVu Sans"), italic ? FontStyle.Italic : FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal);
            overrides.Add(new(start - section.Start, end - start, new GenericTextRunProperties(typeface, heading > 0 ? Math.Min(Options.FontSize * 1.2, Options.LineHeight * .9) : Options.FontSize, null, new SolidColorBrush(Color.Parse(Options.Foreground)), null)));
        }
        var layout = new TextLayout(text, new Typeface(new FontFamily($"{Options.FontFamily}, Noto Sans CJK SC, Microsoft YaHei, DejaVu Sans")), Options.FontSize,
            new SolidColorBrush(Color.Parse(Options.Foreground)), textWrapping: TextWrapping.Wrap, maxWidth: Options.Width, lineHeight: Options.LineHeight, textStyleOverrides: overrides);
        var pages = new List<NativePage>();
        var first = 0; var count = 0; double height = 0; var pageStart = section.Start; var pageEnd = pageStart;
        void Commit()
        {
            if (pageEnd <= pageStart) return;
            if (!Document.IsBoundary(pageStart) || !Document.IsBoundary(pageEnd))
                throw new InvalidOperationException("原生换行切断字符簇，无法继续排版。");
            pages.Add(new NativePage(new PageKey(Id, sectionIndex, pages.Count), pageStart, pageEnd, first, count));
            pageStart = pageEnd; first += count; count = 0; height = 0;
        }
        foreach (var line in layout.TextLines)
        {
            if (section.Start + line.FirstTextSourceIndex >= section.End) break; // Synthetic trailing empty line.
            if (line.Height > Options.Height + .01) { layout.Dispose(); throw new ArgumentException("视口不足以容纳一行。"); }
            if (height + line.Height > Options.Height + .01 && count > 0) Commit();
            count++; height += line.Height;
            pageEnd = Math.Min(section.End, section.Start + line.FirstTextSourceIndex + line.Length);
        }
        Commit();
        if (pageEnd != section.End || pages.Count == 0)
        { layout.Dispose(); throw new InvalidOperationException("原生布局未覆盖完整正文。"); }
        if (_index.TryGetValue(sectionIndex, out var known) && !known.SequenceEqual(pages))
        { layout.Dispose(); throw new InvalidOperationException("相同布局重建得到不同页边界。"); }
        _index[sectionIndex] = pages;
        return Install(sectionIndex, new Entry(layout, pages));
    }
    private Entry Install(int sectionIndex, Entry entry)
    {
        _cache.Add(sectionIndex, entry); _lru.AddFirst(sectionIndex);
        while (_cache.Count > CacheLimit)
        {
            var victim = _lru.Last!;
            while (victim.Value == PinnedSection || victim.Value == sectionIndex) victim = victim.Previous!;
            _cache.Remove(victim.Value, out var old); old!.Layout?.Dispose(); _lru.Remove(victim);
        }
        return entry;
    }
    internal bool IsSectionIndexed(int index) => _index.ContainsKey(index);
    internal NativePage GetPage(int section, int number) => Ensure(section).Pages[number];
    internal int? ChapterPageNumber(NativePage page)
    {
        var chapter = Document.Sections[page.Key.Section].ChapterIndex;
        var number = page.Key.Number + 1;
        for (var i = page.Key.Section - 1; i >= 0 && Document.Sections[i].ChapterIndex == chapter; i--)
        { if (!_index.TryGetValue(i, out var pages)) return null; number += pages.Count; }
        return number;
    }
    public NativePage Locate(int anchor)
    {
        anchor = Document.Snap(anchor);
        var section = Document.SectionAt(anchor);
        return Ensure(section).Pages.FirstOrDefault(p => p.Start <= anchor && anchor < p.End) ?? Ensure(section).Pages.Last();
    }
    private NativePage Check(NativePage page)
    {
        if (page.Key.Layout != Id) throw new InvalidOperationException("布局已失效。");
        var pages = Ensure(page.Key.Section).Pages;
        if (page.Key.Number < 0 || page.Key.Number >= pages.Count || pages[page.Key.Number] != page)
            throw new InvalidOperationException("页引用无效。");
        return page;
    }
    public NativePage? Adjacent(NativePage page, int direction)
    {
        Check(page);
        if (direction != 1 && direction != -1) throw new ArgumentOutOfRangeException(nameof(direction));
        var pages = Ensure(page.Key.Section).Pages;
        var number = page.Key.Number + direction;
        if (number >= 0 && number < pages.Count) return pages[number];
        var section = page.Key.Section + direction;
        if (section < 0 || section >= Document.Sections.Count) return null;
        var next = Ensure(section).Pages;
        return direction > 0 ? next.First() : next.Last();
    }
    public IReadOnlyList<(TextLine Line, double Top)> Lines(NativePage page)
    {
        Check(page);
        if (page.LineCount == 0) return [];
        var lines = Ensure(page.Key.Section).Layout!.TextLines.Skip(page.FirstLine).Take(page.LineCount);
        var result = new List<(TextLine, double)>(); double y = 0;
        foreach (var line in lines) { result.Add((line, y)); y += line.Height; }
        return result;
    }
    public int Hit(NativePage page, Point point)
    {
        if (Document.Images.ContainsKey(page.Start)) return point.X <= Options.Width / 2 ? page.Start : page.End;
        var lines = Lines(page);
        var item = lines.FirstOrDefault(p => point.Y < p.Top + p.Line.Height);
        if (item.Line is null) return page.End;
        var hit = item.Line.GetCharacterHitFromDistance(Math.Max(0, point.X));
        return Document.Snap(Math.Clamp(Document.Sections[page.Key.Section].Start + hit.FirstCharacterIndex + hit.TrailingLength, page.Start, page.End));
    }
    public IReadOnlyList<Rect> SelectionRects(NativePage page, int a, int b)
    {
        var start = Math.Max(page.Start, Math.Min(Document.Snap(a), Document.Snap(b)));
        var end = Math.Min(page.End, Math.Max(Document.Snap(a), Document.Snap(b)));
        var rects = new List<Rect>();
        if (start >= end) return rects;
        var sectionStart = Document.Sections[page.Key.Section].Start;
        foreach (var (line, y) in Lines(page))
        {
            var l = Math.Max(start - sectionStart, line.FirstTextSourceIndex);
            var r = Math.Min(end - sectionStart, line.FirstTextSourceIndex + line.Length);
            if (l >= r) continue;
            foreach (var bound in line.GetTextBounds(l, r - l)) rects.Add(bound.Rectangle.Translate(new Vector(0, y)));
        }
        return rects;
    }
    public int? GlobalNumber(NativePage page)
    {
        Check(page); var count = page.Key.Number + 1;
        for (var i = 0; i < page.Key.Section; i++)
        { if (!_index.TryGetValue(i, out var pages)) return null; count += pages.Count; }
        return count;
    }
    public async Task BuildIndexAsync(IProgress<IndexStatus>? progress, CancellationToken ct)
    {
        for (var i = 0; i < Document.Sections.Count; i++)
        {
            ct.ThrowIfCancellationRequested(); if (!_index.ContainsKey(i)) Ensure(i); progress?.Report(Status);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        ct.ThrowIfCancellationRequested();
    }
    public async Task<ScopeSnapshot> ResolvePagesAsync(int first, int last, CancellationToken ct)
    {
        if (first < 1 || last < first) throw new ArgumentException("页范围无效。");
        var selected = new List<NativePage>(); var seen = 0;
        for (var i = 0; i < Document.Sections.Count && seen < last; i++)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var page in _index.TryGetValue(i, out var indexed) ? indexed : Ensure(i).Pages)
            { seen++; if (seen >= first && seen <= last) selected.Add(page); }
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        ct.ThrowIfCancellationRequested();
        if (seen < last || selected.Count != last - first + 1) throw new ArgumentException("页范围超出正文。");
        return new ScopeSnapshot(Id, Document.Hash, first, last, selected.Select(p => (p.Start, p.End)).ToArray(),
            string.Concat(selected.Select(p => Document.Text[p.Start..p.End])));
    }
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return; _disposed = true;
        foreach (var entry in _cache.Values) entry.Layout?.Dispose();
        _cache.Clear(); _lru.Clear(); _index.Clear();
    }
}
