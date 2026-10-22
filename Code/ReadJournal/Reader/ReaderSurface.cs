using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Reader;

// P2 render boundary. P5 binds the Reader VM and input lifecycle; no visible Reader route exists yet.
public sealed class ReaderSurface : Control
{
    public AvaloniaReaderLayoutService? LayoutService { get; set; }
    public ReaderPage? Page { get; set; }
    public DocumentRange? Selection { get; set; }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (LayoutService is null || Page is null) return;
        var (layout, page, settings) = LayoutService.RenderPlan(Page.Ref);
        var appearance = settings.Appearance;
        var color = appearance.Palette == "dark" ? "#202520" : appearance.Palette == "light" ? "#FFFFFF" : "#F8F3E7";
        context.FillRectangle(new SolidColorBrush(Color.Parse(color)), new Rect(Bounds.Size));
        var origin = new Vector(appearance.MarginHorizontal, appearance.MarginVertical);
        using (context.PushClip(new Rect(origin.X, origin.Y, layout.Options.Width, layout.Options.Height)))
        {
            if (Selection is not null)
            {
                var chapter = layout.Document.Chapters.First(c => c.Key == Selection.Start.ChapterKey);
                var endChapter = layout.Document.Chapters.First(c => c.Key == Selection.End.ChapterKey);
                if (Selection.Start.Document != Selection.End.Document || Selection.Start.Document != Page.ContentRanges[0].Start.Document) throw new ArgumentException("选区文档不一致。");
                foreach (var rect in layout.SelectionRects(page, chapter.Start + Selection.Start.Utf16Offset, endChapter.Start + Selection.End.Utf16Offset))
                    context.FillRectangle(Brushes.LightSeaGreen, rect.Translate(origin));
            }
            foreach (var (line, y) in layout.Lines(page)) line.Draw(context, new Point(origin.X, origin.Y + y));
        }
    }
}
