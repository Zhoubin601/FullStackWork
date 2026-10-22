using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace ReaderPrototype;

public static class Program
{
    public static readonly System.Diagnostics.Stopwatch StartupClock = System.Diagnostics.Stopwatch.StartNew();
    public static string[] Arguments { get; private set; } = [];
    [STAThread]
    public static void Main(string[] args)
    {
        Arguments = args;
        AppBuilder.Configure<PrototypeApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
    public static string? Argument(string key)
    {
        var index = Array.IndexOf(Arguments, key);
        return index >= 0 && index + 1 < Arguments.Length ? Arguments[index + 1] : null;
    }
}
public sealed class PrototypeApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new PrototypeWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
public sealed class PrototypeWindow : Window
{
    public ReaderController Reader { get; } = new();
    public ReaderSurface Surface { get; }
    public double InitialFirstScreenMs { get; private set; }
    public NumericUpDown FontInput => _font;
    public ComboBox FontChoice => _family;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _selection = new() { Height = 40, TextWrapping = TextWrapping.Wrap };
    private readonly NumericUpDown _font = new() { Minimum = 10, Maximum = 48, Value = 22, Text = "22", FormatString = "0", Increment = 2, Width = 110 };
    private readonly NumericUpDown _line = new() { Minimum = 20, Maximum = 80, Value = 34, Text = "34", FormatString = "0", Increment = 2, Width = 110 };
    private readonly ComboBox _family = new() { ItemsSource = new[] { "Noto Sans CJK SC", "DejaVu Sans", "MissingFont-P1" }, SelectedIndex = 0, Width = 180 };
    private readonly ComboBox _chapters = new() { Width = 160 };
    private bool _ready;
    private readonly TaskCompletionSource _startupInput = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public PrototypeWindow()
    {
        Title = "阅记 P1 · 原生分页实验"; Width = 1160; Height = 820; MinWidth = 700; MinHeight = 500;
        FontFamily = new FontFamily("Noto Sans CJK SC"); FontSize = 16;
        Surface = new ReaderSurface(Reader);
        AddHandler(PointerPressedEvent, (_, _) => _startupInput.TrySetResult(), RoutingStrategies.Tunnel, true);
        var root = new DockPanel { Margin = new Thickness(12), LastChildFill = true };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        void Button(string text, Action action)
        { var b = new Button { Content = text }; b.Click += (_, _) => Safe(action); toolbar.Children.Add(b); }
        Button("上一页", () => Reader.Flip(-1)); Button("下一页", () => Reader.Flip(1));
        toolbar.Children.Add(new TextBlock { Text = "字号", VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(_font);
        toolbar.Children.Add(new TextBlock { Text = "行高", VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(_line);
        toolbar.Children.Add(_family); toolbar.Children.Add(_chapters);
        Button("计算页数", Reader.StartIndex); Button("取消", Reader.CancelIndex);
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        var bottom = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        bottom.Children.Add(_status); bottom.Children.Add(_selection);
        bottom.Children.Add(new TextBlock { Text = "正文焦点：←/→ 翻页，Shift+方向键跨页选区，Ctrl+C 复制。修改字号/窗口保留正文锚点。", FontSize = 12 });
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); root.Children.Add(Surface); Content = root;
        Reader.Changed += () => { _status.Text = Reader.PageLabel; Surface.InvalidateVisual(); };
        Surface.Copied += text => _selection.Text = "已复制：" + text;
        _font.ValueChanged += (_, _) => Reflow(); _line.ValueChanged += (_, _) => Reflow(); _family.SelectionChanged += (_, _) => Reflow();
        _chapters.SelectionChanged += (_, _) => { if (_ready && _chapters.SelectedItem is Chapter chapter) Reader.Locate(chapter.Start); };
        Surface.SizeChanged += (_, _) => Reflow();
        ScalingChanged += (_, _) => Reflow();
        Closed += (_, _) => Reader.Dispose();
        Opened += async (_, _) =>
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                var file = Program.Argument("--file");
                await Reader.OpenAsync(ct => Task.Run(() => file is null
                    ? new ReaderDocument(string.Concat(Enumerable.Range(1, 8).Select(i => $"第{i}章 星光\n" + string.Concat(Enumerable.Repeat("　　星光落在书页上。English text，emoji 📖 与 e\u0301。\n", 80)))))
                    : ReaderDocument.Load(file, Program.Argument("--encoding")), ct), CurrentOptions());
                _ready = true; _chapters.ItemsSource = Reader.Document!.Chapters; _chapters.DisplayMemberBinding = new Avalonia.Data.Binding("Title");
                _chapters.SelectedIndex = 0;
                if (int.TryParse(Program.Argument("--anchor"), out var anchor)) Reader.Locate(anchor);
                Surface.Focus();
                ProbeRunner.Paint(Surface);
                InitialFirstScreenMs = Program.StartupClock.Elapsed.TotalMilliseconds;
                if (Program.Argument("--probe-output") is string output)
                {
                    if (Program.Arguments.Contains("--await-input")) await _startupInput.Task.WaitAsync(TimeSpan.FromSeconds(20));
                    var result = await ProbeRunner.RunAsync(this, output, Program.Argument("--samples"));
                    (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.Shutdown(result ? 0 : 1);
                }
                else Reader.StartIndex();
            }
            catch (Exception ex)
            {
                _status.Text = ex.ToString(); Console.Error.WriteLine(ex);
                if (Program.Argument("--probe-output") is not null)
                    (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.Shutdown(1);
            }
        };
    }
    public LayoutOptions CurrentOptions() => new(Math.Max(100, Surface.Bounds.Width - 2 * ReaderSurface.Inset),
        Math.Max(80, Surface.Bounds.Height - 2 * ReaderSurface.Inset), (double)(_font.Value ?? 22),
        Math.Max((double)(_font.Value ?? 22), (double)(_line.Value ?? 34)), _family.SelectedItem as string ?? "Noto Sans CJK SC", RenderScaling);
    private void Reflow()
    {
        if (!_ready || Surface.Bounds.Width < 200 || Surface.Bounds.Height < 150) return;
        Safe(() => { Reader.Reflow(CurrentOptions()); if (Program.Argument("--probe-output") is null) Reader.StartIndex(); });
    }
    private void Safe(Action action)
    { try { action(); } catch (Exception ex) { _status.Text = ex.Message; } }
}
