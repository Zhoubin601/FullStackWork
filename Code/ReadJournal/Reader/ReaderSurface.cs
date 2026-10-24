using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Reader;

// Native rendering and presentation boundary; business timing starts after the first draw.
public sealed class ReaderSurface : Control
{
    public AvaloniaReaderLayoutService? LayoutService { get; set; }
    public static readonly StyledProperty<ReaderPage?> PageProperty = AvaloniaProperty.Register<ReaderSurface, ReaderPage?>(nameof(Page));
    public ReaderPage? Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }
    private LayoutId? _presented;
    public Func<LayoutId, Task>? Presented { get; set; }
    static ReaderSurface() => AffectsRender<ReaderSurface>(PageProperty);
    public static readonly StyledProperty<DocumentRange?> SelectionProperty = AvaloniaProperty.Register<ReaderSurface, DocumentRange?>(nameof(Selection));
    public DocumentRange? Selection { get => GetValue(SelectionProperty); set => SetValue(SelectionProperty, value); }
    public Func<DocumentRange?, Task>? SelectionChanged { get; set; }
    private ReaderPosition? _anchor;
    public ReaderSurface()
    {
        Focusable = true;
        PointerPressed += async (_, e) =>
        {
            Focus(); if (Page is null || LayoutService is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            var page = Page; var hit = await HitAsync(e.GetPosition(this)); if (hit is null || Page != page) return;
            _anchor = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Selection is not null ? Selection.Start : hit;
            e.Pointer.Capture(this); await UpdateSelectionAsync(hit);
        };
        PointerMoved += async (_, e) => { if (_anchor is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return; var hit = await HitAsync(e.GetPosition(this)); if (hit is not null) await UpdateSelectionAsync(hit); };
        PointerReleased += (_, e) => { _anchor = null; e.Pointer.Capture(null); };
        AffectsRender<ReaderSurface>(SelectionProperty);
    }
    private async Task<ReaderPosition?> HitAsync(Point point)
    {
        var page = Page; if (page is null || LayoutService is null) return null;
        try { var settings = LayoutService.RenderPlan(page.Ref).Settings; return await LayoutService.HitTestAsync(page.Ref, new(point.X - settings.Appearance.MarginHorizontal, point.Y - settings.Appearance.MarginVertical), default); }
        catch (InvalidOperationException) { return null; }
    }
    private async Task UpdateSelectionAsync(ReaderPosition hit)
    {
        if (_anchor is null || _anchor.Document != hit.Document || Page is null || LayoutService is null || SelectionChanged is null) return;
        if (_anchor == hit) { await SelectionChanged(null); return; }
        var chapters = LayoutService.RenderPlan(Page.Ref).Layout.Document.Chapters;
        var a = chapters.ToList().FindIndex(c => c.Key == _anchor.ChapterKey); var b = chapters.ToList().FindIndex(c => c.Key == hit.ChapterKey);
        await SelectionChanged(a < b || a == b && _anchor.Utf16Offset < hit.Utf16Offset ? new(_anchor, hit) : new(hit, _anchor));
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (LayoutService is null || Page is null) return;
        var (layout, page, settings) = LayoutService.RenderPlan(Page.Ref);
        var appearance = settings.Appearance;
        var color = appearance.Palette == "dark" ? "#202520" : appearance.Palette == "light" ? "#FFFFFF" : "#F8F3E7";
        var borderColor = appearance.Palette == "dark" ? "#33FFFFFF" : "#18000000";
        var selectionBrush = new SolidColorBrush(Color.Parse(appearance.Palette == "dark" ? "#66389B7A" : "#40246650"));
        var boundsRect = new Rect(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(Color.Parse(color)), boundsRect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse(borderColor)), 1), boundsRect.Deflate(0.5));
        var origin = new Vector(appearance.MarginHorizontal, appearance.MarginVertical);
        using (context.PushClip(new Rect(origin.X, origin.Y, layout.Options.Width, layout.Options.Height)))
        {
            if (Selection is not null)
            {
                var chapter = layout.Document.Chapters.First(c => c.Key == Selection.Start.ChapterKey);
                var endChapter = layout.Document.Chapters.First(c => c.Key == Selection.End.ChapterKey);
                if (Selection.Start.Document != Selection.End.Document || Selection.Start.Document != Page.ContentRanges[0].Start.Document) throw new ArgumentException("选区文档不一致。");
                foreach (var rect in layout.SelectionRects(page, chapter.Start + Selection.Start.Utf16Offset, endChapter.Start + Selection.End.Utf16Offset))
                    context.FillRectangle(selectionBrush, rect.Translate(origin), 3f);
            }
            if (LayoutService.Image(Page.Ref) is { } bitmap && layout.Document.Images.TryGetValue(page.Start, out var image))
            {
                var ratio = Math.Min(1, Math.Min(layout.Options.Width / image.Resource.PixelWidth, layout.Options.Height / image.Resource.PixelHeight));
                var width = image.Resource.PixelWidth * ratio; var height = image.Resource.PixelHeight * ratio;
                context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), new Rect(origin.X + (layout.Options.Width - width) / 2, origin.Y + (layout.Options.Height - height) / 2, width, height));
            }
            else foreach (var (line, y) in layout.Lines(page)) line.Draw(context, new Point(origin.X, origin.Y + y));
        }
        if (_presented != Page.Ref.Layout)
        {
            var id = Page.Ref.Layout; _presented = id;
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            { try { if (Page?.Ref.Layout == id && Presented is not null) await Presented(id); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); } }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}
