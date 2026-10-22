using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ReaderPrototype;

public sealed class ReaderSurface : Control
{
    public const double Inset = 28;
    public ReaderController Controller { get; }
    private bool _dragging;
    private long _lastEdgeFlip;
    public event Action<string>? Copied;
    public string? LastCopyText { get; private set; }
    public ReaderSurface(ReaderController controller)
    {
        Controller = controller; Focusable = true; ClipToBounds = true;
        controller.Changed += InvalidateVisual;
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#F8F3E7")), new Rect(Bounds.Size));
        var layout = Controller.Layout; var page = Controller.Page;
        if (layout is null || page is null) return;
        using (context.PushClip(new Rect(Inset, Inset, layout.Options.Width, layout.Options.Height)))
        {
            foreach (var rect in layout.SelectionRects(page, Controller.SelectionAnchor, Controller.SelectionFocus))
                context.FillRectangle(new SolidColorBrush(Color.Parse("#A6CFC5")), rect.Translate(new Vector(Inset, Inset)));
            foreach (var (line, y) in layout.Lines(page)) line.Draw(context, new Point(Inset, Inset + y));
        }
    }
    public int Hit(Point point) => Controller.Layout is { } layout && Controller.Page is { } page
        ? layout.Hit(page, point - new Vector(Inset, Inset)) : 0;
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Controller.Page is null) return;
        Focus();
        var hit = Hit(e.GetPosition(this));
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Controller.SelectionAnchor = hit;
        Controller.SelectionFocus = hit; _dragging = true; e.Pointer.Capture(this);
        Controller.NotifySelection(); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || Controller.Layout is null) return;
        var point = e.GetPosition(this);
        var now = Environment.TickCount64;
        if (now - _lastEdgeFlip >= 200 && (point.Y > Bounds.Height || point.Y < 0))
        {
            if (Controller.Flip(point.Y > Bounds.Height ? 1 : -1, true)) _lastEdgeFlip = now;
        }
        Controller.SelectionFocus = Hit(point); Controller.NotifySelection(); e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    { base.OnPointerReleased(e); _dragging = false; e.Pointer.Capture(null); }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    { base.OnPointerCaptureLost(e); _dragging = false; }
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        try
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.C)
            { await CopyAsync(); e.Handled = true; }
            else if (e.Key is Key.Left or Key.Right)
            { Controller.Flip(e.Key == Key.Right ? 1 : -1, e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled = true; }
        }
        catch (Exception ex) { Copied?.Invoke("复制失败：" + ex.Message); }
    }
    public async Task CopyAsync()
    {
        var text = Controller.SelectedText;
        if (text.Length == 0) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard ?? throw new InvalidOperationException("剪贴板不可用。");
        await clipboard.SetTextAsync(text); LastCopyText = text; Copied?.Invoke(text);
    }
}
