using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace ReaderPrototype.Tests;

public sealed class NativeReaderTests
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(NativeReaderTests)));
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<PrototypeApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
    private static Task Ui(Func<Task> run) => Session.Value.Dispatch(async () => { await run(); return true; }, CancellationToken.None);
    private static Task Ui(Action run) => Session.Value.Dispatch(run, CancellationToken.None);
    private static readonly LayoutOptions Settings = new(420, 240);

    [Theory]
    [InlineData("中文。English!\r\n\r\n　　段首缩进。\r文末\n")]
    [InlineData("emoji 📖👩🏽‍🚀 与 e\u0301 ❤️ 家庭 👨‍👩‍👧‍👦\n")]
    [InlineData("\n\n\n ")]
    public Task PageCoverageAndGraphemeBoundaries(string sample) => Ui(() =>
    {
        var document = new ReaderDocument(string.Concat(Enumerable.Repeat(sample, 1200)));
        using var layout = new NativeLayout(document, Settings);
        var parts = new List<string>(); var page = layout.Locate(0); var expected = 0;
        do
        {
            Assert.Equal(expected, page.Start); Assert.True(page.End > page.Start);
            Assert.True(document.IsBoundary(page.Start)); Assert.True(document.IsBoundary(page.End));
            parts.Add(document.Text[page.Start..page.End]); expected = page.End;
            Assert.True(layout.CachedSections <= 8);
            page = layout.Adjacent(page, 1);
        } while (page is not null);
        Assert.Equal(document.Text, string.Concat(parts));
    });

    [Fact]
    public Task LongParagraphHasBoundedSectionsAndProgressesBothDirections() => Ui(() =>
    {
        var document = new ReaderDocument(string.Concat(Enumerable.Repeat("没有章节的超长段落 abcdef。", 10000)));
        Assert.All(document.Sections, section => Assert.InRange(section.End - section.Start, 1, 4096));
        using var layout = new NativeLayout(document, Settings);
        var nearEnd = layout.Locate(document.Text.Length - 3);
        Assert.Equal(1, layout.Status.IndexedSections);
        Assert.Null(layout.Status.ExactPages);
        var previous = layout.Adjacent(nearEnd, -1)!;
        Assert.Equal(nearEnd.Start, previous.End);
        Assert.Equal(nearEnd, layout.Adjacent(previous, 1));
        Assert.Null(layout.Adjacent(layout.Locate(0), -1));
    });

    [Fact]
    public Task UnicodeHitTestingAndSelectionUseNativeLineGeometry() => Ui(() =>
    {
        var document = new ReaderDocument("abcdef 中文标点 📖 e\u0301👩🏽‍🚀\n" + new string('甲', 1000));
        using var layout = new NativeLayout(document, Settings);
        var page = layout.Locate(0);
        var line = layout.Lines(page).First().Line;
        var point = new Point(line.GetDistanceFromCharacterHit(new CharacterHit(3, 0)) + .01, line.Height / 2);
        Assert.Equal(3, layout.Hit(page, point));
        Assert.NotEmpty(layout.SelectionRects(page, 6, 1));
        Assert.Equal("bcdef", document.Extract(6, 1));
        foreach (var (textLine, y) in layout.Lines(page))
            for (double x = 0; x <= Settings.Width; x += 5)
                Assert.True(document.IsBoundary(layout.Hit(page, new Point(x, y + textLine.Height / 2))));
    });

    [Fact]
    public Task StableAnchorSurvivesReflowAndOldPagesAreRejected() => Ui(async () =>
    {
        using var reader = new ReaderController();
        var document = new ReaderDocument(string.Concat(Enumerable.Repeat("第一章 合成\n正文📖与 e\u0301，后续文字。\n", 1000)));
        await reader.OpenAsync(_ => Task.FromResult(document), Settings);
        var offset = document.Snap(document.Text.Length * 4 / 5); reader.Locate(offset);
        var old = reader.Page!;
        foreach (var font in new[] { "Noto Sans CJK SC", "DejaVu Sans", "MissingFont-P1" })
            foreach (var scale in new[] { 1.0, 1.5, 2.0 })
            {
                reader.Reflow(Settings with { FontFamily = font, DpiScale = scale, FontSize = 25, LineHeight = 39, Width = 310 });
                Assert.Equal(offset, reader.Anchor);
                Assert.True(reader.Page!.Start <= offset && offset < reader.Page.End);
            }
        Assert.Throws<InvalidOperationException>(() => reader.Layout!.Adjacent(old, 1));
        // Logical scale variations are not Windows physical DPI acceptance.
    });

    [Fact]
    public Task MissingFamilyUsesExplicitFallbackLineMetrics() => Ui(() =>
    {
        var document = new ReaderDocument("第一章 字符边界\nemoji 📖👩🏽‍🚀，组合字符 e\u0301，变体 ❤️，家庭 👨‍👩‍👧‍👦。\n");
        using var expected = new NativeLayout(document, Settings);
        using var missing = new NativeLayout(document, Settings with { FontFamily = "MissingFont-P1" });
        var reference = expected.Lines(expected.Locate(0));
        var actual = missing.Lines(missing.Locate(0));
        Assert.Equal(reference.Count, actual.Count);
        for (var i = 0; i < reference.Count; i++)
        {
            Assert.Equal(reference[i].Line.Baseline, actual[i].Line.Baseline, 3);
            Assert.Equal(reference[i].Line.Length, actual[i].Line.Length);
            Assert.InRange(actual[i].Line.Baseline, 0, Settings.LineHeight);
        }
    });

    [Fact]
    public Task CancelledIndexAndOldOpenCannotOverwriteNewState() => Ui(async () =>
    {
        using var reader = new ReaderController();
        var first = new TaskCompletionSource<ReaderDocument>();
        var pending = reader.OpenAsync(_ => first.Task, Settings);
        var newBook = new ReaderDocument("新书\n当前正文。");
        await reader.OpenAsync(_ => Task.FromResult(newBook), Settings);
        first.SetResult(new ReaderDocument("旧书"));
        Assert.False(await pending); Assert.Same(newBook, reader.Document);
        var large = new ReaderDocument(new string('文', 200000));
        await reader.OpenAsync(_ => Task.FromResult(large), Settings);
        reader.StartIndex(); var oldIndex = reader.IndexTask;
        reader.Reflow(Settings with { Width = 390 });
        var id = reader.Layout!.Id; await oldIndex;
        Assert.Equal(id, reader.Layout.Id); Assert.Equal(1, reader.Layout.Status.IndexedSections);
    });

    [Fact]
    public Task FrozenPageScopeIsExactAndCancellationDoesNotReturnPartialSnapshot() => Ui(async () =>
    {
        var document = new ReaderDocument(string.Concat(Enumerable.Repeat("正文 abcdef。\n", 5000)));
        using var layout = new NativeLayout(document, Settings);
        var scope = await layout.ResolvePagesAsync(1, 3, CancellationToken.None);
        Assert.Equal(document.Text[scope.Ranges.First().Start..scope.Ranges.Last().End], scope.Text);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => layout.ResolvePagesAsync(1, 3, cancelled.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => layout.ResolvePagesAsync(2, 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => layout.ResolvePagesAsync(1, 100000, CancellationToken.None));
    });

    [Fact]
    public Task ActualPointerDragReverseCopyAndCrossPageKeyboardSelection() => Ui(async () =>
    {
        using var reader = new ReaderController();
        var surface = new ReaderSurface(reader);
        var window = new Window { Width = 620, Height = 420, Content = surface };
        try
        {
            window.Show();
            await reader.OpenAsync(_ => Task.FromResult(new ReaderDocument(string.Concat(Enumerable.Repeat("abcdef 中文📖与 e\u0301。\n", 1000)))), new LayoutOptions(564, 364));
            var start = new Point(ReaderSurface.Inset + 15, ReaderSurface.Inset + 15);
            var end = new Point(ReaderSurface.Inset + 180, ReaderSurface.Inset + 80);
            window.MouseDown(end, MouseButton.Left); window.MouseMove(start, RawInputModifiers.LeftMouseButton); window.MouseUp(start, MouseButton.Left);
            Assert.NotEqual(reader.SelectionAnchor, reader.SelectionFocus);
            Assert.True(reader.SelectionFocus < reader.SelectionAnchor);
            Assert.Equal(reader.Document!.Extract(reader.SelectionAnchor, reader.SelectionFocus), reader.SelectedText);
            await surface.CopyAsync(); Assert.Equal(reader.SelectedText, await window.Clipboard!.GetTextAsync());
            reader.SelectionAnchor = reader.SelectionFocus = 0; surface.Focus();
            window.KeyPress(Key.Right, RawInputModifiers.Shift, PhysicalKey.ArrowRight, "ArrowRight");
            window.KeyRelease(Key.Right, RawInputModifiers.Shift, PhysicalKey.ArrowRight, "ArrowRight");
            Assert.True(reader.Page!.Start > 0);
            Assert.Equal(reader.Document.Text[0..reader.Page.End], reader.SelectedText);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(0, 200, 22, 34)]
    [InlineData(400, 0, 22, 34)]
    [InlineData(400, 200, 30, 10)]
    public void InvalidViewportIsRejected(double width, double height, double font, double line)
    { Assert.Throws<ArgumentException>(() => new NativeLayout(new ReaderDocument("正文"), new LayoutOptions(width, height, font, line))); }

    [Fact]
    public void EmptyInputAndUnboundedCombiningClusterFailExplicitly()
    {
        Assert.Throws<ArgumentException>(() => new ReaderDocument(""));
        Assert.Throws<ArgumentException>(() => new ReaderDocument("e" + new string('\u0301', 2000)));
    }
}
