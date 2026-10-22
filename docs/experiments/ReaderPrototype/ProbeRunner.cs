using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ReaderPrototype;

public static class ProbeRunner
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static double P95(IEnumerable<double> values)
    { var sorted = values.Order().ToArray(); return sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]; }
    public static async Task<bool> RunAsync(PrototypeWindow window, string outputDirectory, string? samplesDirectory)
    {
        var output = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(output)) throw new IOException("证据目录必须为新目录。");
        Directory.CreateDirectory(output);
        var evidence = new Dictionary<string, object?>
        {
            ["platform"] = Environment.OSVersion.ToString(), ["runtime"] = Environment.Version.ToString(),
            ["avalonia"] = typeof(Application).Assembly.GetName().Version?.ToString(),
            ["renderScaling"] = window.RenderScaling, ["kind"] = "real_desktop_native_probe",
            ["initialProcessFirstScreenMs"] = window.InitialFirstScreenMs,
            ["initialProcessFirstScreenScope"] = "process entry through first document/layout/raster; OS file cache not cleared",
            ["windows_dpi_acceptance"] = "not_performed_by_this_probe"
        };
        var passed = false;
        try
        {
            var reader = window.Reader;
            Require(window.InitialFirstScreenMs <= 2000, "进程首次首屏超过 2 秒目标。");
            var settings = window.CurrentOptions();
            var sampleChecks = new List<object>();
            var sampleNames = new[] { "mixed-lf.txt", "mixed-crlf.txt", "mixed-cr.txt", "mixed-bom.txt", "gb18030.txt", "unicode.txt", "long-paragraph.txt", "million.txt" };
            foreach (var name in sampleNames)
            {
                var file = Path.Combine(samplesDirectory ?? throw new ArgumentException("--samples is required"), name);
                var begin = Stopwatch.StartNew();
                var document = ReaderDocument.Load(file, name == "gb18030.txt" ? "gb18030" : null);
                using var layout = new NativeLayout(document, settings);
                var first = layout.Locate(0); var firstMs = begin.Elapsed.TotalMilliseconds;
                Require(layout.Status.IndexedSections == 1, "首屏依赖了完整分页。");
                var text = new StringBuilder(); var page = first; var pages = 0; var expectedStart = 0;
                while (true)
                {
                    Require(page.Start == expectedStart && page.End > page.Start, "页范围不连续或未推进。");
                    Require(document.IsBoundary(page.Start) && document.IsBoundary(page.End), "页边界切断字符簇。");
                    Require(layout.Lines(page).Sum(p => p.Line.Height) <= settings.Height + .01, "文字高度越过视口。");
                    text.Append(document.Text[page.Start..page.End]); expectedStart = page.End; pages++;
                    var next = layout.Adjacent(page, 1); if (next is null) break; page = next;
                    Require(layout.CachedSections <= layout.CacheLimit, "缓存超过预算。");
                    // This exhaustive traversal is a probe, yield so it does not freeze the desktop.
                    if (pages % 8 == 0) await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
                Require(text.ToString() == document.Text, "页面拼接与规范化原文不一致。");
                Require(expectedStart == document.Text.Length, "未抵达文末。");
                sampleChecks.Add(new { file = name, document.Hash, utf16Units = document.Text.Length, pages,
                    firstScreenMs = firstMs, cachedSections = layout.CachedSections, layout.CacheLimit,
                    completeTextMatches = true, graphemeBoundaries = true });
            }
            evidence["coverage"] = sampleChecks;

            var millionFile = Path.Combine(samplesDirectory!, "million.txt");
            var firstTimer = Stopwatch.StartNew();
            await reader.OpenAsync(ct => Task.Run(() => ReaderDocument.Load(millionFile), ct), settings);
            Paint(window.Surface); var firstScreen = firstTimer.Elapsed.TotalMilliseconds;
            Require(reader.Layout!.Status.IndexedSections < reader.Document!.Sections.Count, "百万字首屏等待全书索引。");
            evidence["millionFirstScreenLoadLayoutRenderMs"] = firstScreen;
            Require(firstScreen <= 2000, "百万字首屏超过 2 秒目标。");

            var flipMs = new List<double>();
            // Warm the font system, rasterizer and both adjacent pages first.
            reader.Flip(1); Paint(window.Surface); reader.Flip(-1); Paint(window.Surface);
            for (var i = 0; i < 200; i++)
            {
                var timer = Stopwatch.StartNew(); reader.Flip(i % 2 == 0 ? 1 : -1); Paint(window.Surface);
                flipMs.Add(timer.Elapsed.TotalMilliseconds);
            }
            var anchor = reader.Document.Snap(reader.Document.Text.Length * 3 / 4);
            reader.Locate(anchor);
            var reflowMs = new List<double>();
            for (var i = 0; i < 200; i++)
            {
                var timer = Stopwatch.StartNew();
                reader.Reflow(settings with { Width = settings.Width - 40 - i % 3 * 30, FontSize = 20 + i % 4, LineHeight = 34 + i % 3 });
                Paint(window.Surface);
                reflowMs.Add(timer.Elapsed.TotalMilliseconds);
                Require(reader.Anchor == anchor && reader.Page!.Start <= anchor && anchor < reader.Page.End, "重排丢失稳定锚点。");
            }
            evidence["performance"] = new { warmFlipMs = flipMs, reflowAndRasterMs = reflowMs,
                flipP95Ms = P95(flipMs), reflowP95Ms = P95(reflowMs),
                scope = "same UI thread: navigation/reflow + native offscreen rasterization; not physical display latency" };
            Require(P95(flipMs) <= 100 && P95(reflowMs) <= 500, "翻页或重排 P95 未达目标。");

            // Real pointer hit geometry, reverse ranges, cross-page selection and system clipboard.
            reader.Reflow(settings); reader.Locate(0);
            var active = reader.Layout!; var current = reader.Page!;
            foreach (var (line, y) in active.Lines(current))
                for (double x = 0; x < settings.Width; x += 7)
                    Require(reader.Document.IsBoundary(active.Hit(current, new Point(x, y + line.Height / 2))), "命中落在字符簇内部。");
            var beginSelection = reader.Document.Snap(current.Start + 4);
            var endSelection = reader.Document.Snap(current.End - 5);
            reader.SelectionAnchor = endSelection; reader.SelectionFocus = beginSelection;
            Require(reader.SelectedText == reader.Document.Text[beginSelection..endSelection], "反向选区原文错误。");
            Require(active.SelectionRects(current, endSelection, beginSelection).Count > 0, "选区无几何矩形。");
            var expectedClipboard = reader.SelectedText;
            await window.Surface.CopyAsync();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(50);
            var actualClipboard = await window.Clipboard!.GetTextAsync();
            var clipboardFormats = await window.Clipboard.GetFormatsAsync();
            evidence["clipboardDiagnostic"] = new { expected = expectedClipboard, actual = actualClipboard, clipboardFormats,
                type = window.Clipboard.GetType().FullName, layoutChanged = !ReferenceEquals(reader.Layout, active),
                surfaceCopyRan = window.Surface.LastCopyText is not null,
                surfaceCopyMatches = window.Surface.LastCopyText == expectedClipboard };
            string? externalClipboard = null;
            if (Program.Argument("--clipboard-reader") is string readerPath)
            {
                using var process = Process.Start(new ProcessStartInfo(readerPath) { RedirectStandardOutput = true,
                    RedirectStandardError = true, ArgumentList = { "-o", "-selection", "clipboard", "-target", "UTF8_STRING" } })!;
                externalClipboard = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Require(process.ExitCode == 0, "外部剪贴板读取失败：" + await process.StandardError.ReadToEndAsync());
            }
            evidence["clipboardDiagnostic"] = new { expected = expectedClipboard, actual = actualClipboard,
                clipboardFormats, externalClipboard, selectionAfterCopy = reader.SelectedText, reader.SelectionAnchor, reader.SelectionFocus };
            Require(actualClipboard == expectedClipboard && (externalClipboard is null || externalClipboard == expectedClipboard), "系统剪贴板与选区原文不一致。");
            reader.SelectionAnchor = reader.SelectionFocus = current.Start;
            Require(reader.Flip(1, true), "无法跨页选择。");
            Require(reader.SelectedText == reader.Document.Text[current.Start..reader.Page!.End], "跨页选区原文错误。");
            evidence["selection"] = new { realGlyphHitGrid = true, reverseRange = true, crossPageRange = true, systemClipboardRoundtrip = true };
            if (Program.Arguments.Contains("--await-drag"))
            {
                reader.Locate(0); reader.SelectionAnchor = reader.SelectionFocus = 0;
                evidence["realDesktopReverseDrag"] = await AwaitDesktopCopyAsync(window, output, "drag-ready.json");
                Require(reader.SelectionFocus < reader.SelectionAnchor, "真实反向拖选方向错误。");
                reader.Locate(0); reader.SelectionAnchor = reader.SelectionFocus = 0;
                evidence["realDesktopCrossPage"] = await AwaitDesktopCopyAsync(window, output, "cross-page-ready.json");
                Require(reader.Page!.Start > 0 && reader.SelectionAnchor == 0 && reader.SelectionFocus == reader.Page.End, "真实 Shift+翻页选区错误。");
                evidence["realDesktopInputFocus"] = await AwaitDesktopFocusAsync(window, output);
            }

            // Page-range snapshot is immutable after reflow; unknown/out-of-range/cancelled ranges fail.
            var scope = await reader.Layout!.ResolvePagesAsync(1, 2, CancellationToken.None);
            var frozen = scope.Text;
            var obsolete = reader.Page!;
            reader.Reflow(settings with { FontSize = 26, LineHeight = 39 });
            Require(scope.Text == frozen && scope.Layout != reader.Layout!.Id, "已冻结范围被重排修改。");
            bool staleRejected = false;
            try { reader.Layout.Adjacent(obsolete, 1); } catch (InvalidOperationException) { staleRejected = true; }
            Require(staleRejected, "旧布局页号未拒绝。");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); bool rejected = false;
                try { await reader.Layout.ResolvePagesAsync(1, 2, cancel.Token); } catch (OperationCanceledException) { rejected = true; }
                Require(rejected, "取消页范围仍返回快照。");
            }
            evidence["rangeSnapshot"] = new { reflowPreservedFrozenText = true, stalePageRejected = true, cancellationRejected = true,
                scope.FirstPage, scope.LastPage, characters = scope.Text.Length };

            // Old async open and old index must never replace the newer document or viewport.
            var slow = new TaskCompletionSource<ReaderDocument>();
            var oldOpen = reader.OpenAsync(_ => slow.Task, settings);
            var newer = new ReaderDocument("第一章 新书\n新上下文正文。\n");
            await reader.OpenAsync(_ => Task.FromResult(newer), settings);
            slow.SetResult(new ReaderDocument("第一章 旧书\n不得覆盖。\n"));
            Require(!await oldOpen && reader.Document!.Hash == newer.Hash, "旧异步打开覆盖新书。");
            await reader.OpenAsync(_ => Task.FromResult(ReaderDocument.Load(millionFile)), settings);
            reader.StartIndex(); var oldIndex = reader.IndexTask;
            reader.Reflow(settings with { Width = settings.Width - 51 });
            var newId = reader.Layout!.Id; await oldIndex;
            Require(reader.Layout.Id == newId && reader.Layout.Status.IndexedSections == 1, "旧后台索引覆盖新布局。");
            var heartbeatMs = new List<double>(); var lastBeat = Stopwatch.GetTimestamp();
            var heartbeat = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            heartbeat.Tick += (_, _) => { var now = Stopwatch.GetTimestamp(); heartbeatMs.Add(Stopwatch.GetElapsedTime(lastBeat, now).TotalMilliseconds); lastBeat = now; };
            heartbeat.Start();
            reader.StartIndex(); await reader.IndexTask;
            heartbeat.Stop();
            Require(reader.Layout.Status.ExactPages is > 0, "全书精确索引未完成。");
            Require(heartbeatMs.Count > 0 && heartbeatMs.Max() <= 266, "后台索引造成超过 250ms 的额外主线程延迟。");
            evidence["asyncAndIndex"] = new { staleOpenIgnored = true, oldIndexCancelled = true, reader.IgnoredOpenResults,
                exactPages = reader.Layout.Status.ExactPages, heartbeatIntervalMs = 16, heartbeatMs,
                maxHeartbeatGapMs = heartbeatMs.Max(), maxDelayBeyondIntervalMs = Math.Max(0, heartbeatMs.Max() - 16) };

            var memory = new List<long>();
            // Drop each old document/context; only one layout with at most eight shaped sections remains.
            for (var i = 0; i < 20; i++)
            {
                await reader.OpenAsync(ct => Task.Run(() => ReaderDocument.Load(millionFile), ct), settings);
                Require(reader.Layout!.CachedSections <= 8, "切书累积缓存。");
                memory.Add(GC.GetTotalMemory(true));
            }
            var growth = memory.Last() - memory.Skip(5).First();
            Require(growth < 16 * 1024 * 1024, "连续切书出现明显线性保留。");
            Require(memory.Last() < 128 * 1024 * 1024, "实验托管内存超过128MiB本机预算。");
            evidence["memory"] = new { managedBytesAfterEachOf20Opens = memory, retainedGrowthBytes = growth,
                lastOpenManagedBudgetBytes = 128 * 1024 * 1024, shapedSectionCacheLimit = 8,
                caveat = "Local experimental budget; managed heap only. Growth is open 20 minus open 6 (after warmup), not full-run growth or leak freedom." };

            evidence["textBoxComparison"] = await CompareTextBoxAsync(window);
            if (Program.Arguments.Contains("--await-drag"))
            {
                reader.Locate(reader.Document!.Text.Length * 3 / 4);
                var expectedAnchor = reader.Anchor; var beforeSize = window.Bounds.Size;
                var resized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void SizeChanged(object? sender, SizeChangedEventArgs args)
                { if (args.NewSize.Width != beforeSize.Width) resized.TrySetResult(); }
                window.SizeChanged += SizeChanged;
                try
                {
                    await File.WriteAllTextAsync(Path.Combine(output, "resize-ready.json"), "{\"width\":1040,\"height\":760}");
                    await resized.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    Require(reader.Anchor == expectedAnchor && reader.Page!.Start <= expectedAnchor && expectedAnchor < reader.Page.End, "真实窗口缩放丢失锚点。");
                    Require(Math.Abs(reader.Layout!.Options.Width - (window.Surface.Bounds.Width - ReaderSurface.Inset * 2)) < .1, "真实视口与排版宽度不一致。");
                    evidence["realDesktopResize"] = new { expectedAnchor, actualAnchor = reader.Anchor,
                        beforeWidth = beforeSize.Width, afterWidth = window.Bounds.Width, reader.Layout.Options.Width, reader.Layout.Options.DpiScale };
                    await Task.Delay(300); // Let the driver capture the resized real window.
                }
                finally { window.SizeChanged -= SizeChanged; }
            }
            var fontScreens = new List<object>();
            foreach (var (font, fileName) in new[] { ("Noto Sans CJK SC", "unicode-cjk.png"), ("DejaVu Sans", "unicode-fallback.png"), ("MissingFont-P1", "unicode-missing-font.png") })
            {
                window.FontChoice.SelectedItem = font;
                await reader.OpenAsync(ct => Task.Run(() => ReaderDocument.Load(Path.Combine(samplesDirectory!, "unicode.txt")), ct), window.CurrentOptions());
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                var baselines = reader.Layout!.Lines(reader.Page!).Select(p => p.Line.Baseline).ToArray();
                Require(baselines.All(b => b >= 0 && b <= reader.Layout.Options.LineHeight), "字体回退行基线超出行框。");
                SaveWindow(window, Path.Combine(output, fileName));
                fontScreens.Add(new { font, fileName, actualScale = window.RenderScaling, baselines, reader.Document!.Hash });
            }
            evidence["unicodeFontScreens"] = fontScreens;
            window.FontChoice.SelectedIndex = 0;
            await reader.OpenAsync(ct => Task.Run(() => ReaderDocument.Load(millionFile), ct), window.CurrentOptions());
            reader.Locate(0);
            reader.SelectionAnchor = reader.Document!.Snap(7); reader.SelectionFocus = reader.Document.Snap(40); reader.NotifySelection();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            SaveWindow(window, Path.Combine(output, "native-reader.png"));
            evidence["cases"] = new Dictionary<string, string>
            {
                ["P1-01"] = "passed_local_probe", ["P1-02"] = "passed_local_probe",
                ["P1-03"] = Program.Arguments.Contains("--await-drag") ? "passed_real_desktop_drag_crosspage_clipboard" : "geometry_and_clipboard_passed; actual_drag_checked_separately",
                ["P1-04"] = "passed_local_probe", ["P1-05"] = "passed_local_probe",
                ["P1-06"] = "passed_on_recorded_machine_only",
                ["P1-07"] = OperatingSystem.IsLinux() ? "linux_font_screens_and_input_focus; Windows_retained_unverified" : "recorded_windows_scale_only; three_scale_manual_checks_pending",
                ["P1-08"] = "passed_local_probe"
            };
            passed = true;
        }
        catch (Exception ex) { evidence["failure"] = ex.ToString(); Console.Error.WriteLine(ex); }
        evidence["passedLocalProbe"] = passed;
        await File.WriteAllTextAsync(Path.Combine(output, "probe.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine($"Local native probe: {passed}, evidence: {output}");
        return passed;
    }
    public static void Paint(ReaderSurface surface)
    {
        var scale = TopLevel.GetTopLevel(surface)?.RenderScaling ?? 1;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(surface.Bounds.Width * scale), (int)Math.Ceiling(surface.Bounds.Height * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(surface);
    }
    private static async Task<object> AwaitDesktopFocusAsync(PrototypeWindow window, string output)
    {
        var page = window.Reader.Page!;
        var pressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void KeyUp(object? sender, KeyEventArgs args) { if (args.Key == Key.Right) pressed.TrySetResult(); }
        window.AddHandler(InputElement.KeyUpEvent, KeyUp, RoutingStrategies.Tunnel, true);
        try
        {
            var origin = window.FontInput.TranslatePoint(new Point(0, 0), window)!.Value;
            await File.WriteAllTextAsync(Path.Combine(output, "focus-ready.json"), JsonSerializer.Serialize(new { inputX = origin.X + 16, inputY = origin.Y + 18 }));
            await pressed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Require(window.FontInput.IsKeyboardFocusWithin && !window.Surface.IsKeyboardFocusWithin, "焦点未进入字号输入。");
            Require(window.Reader.Page == page, "输入框中的右键误翻正文页。");
            return new { numericInputFocused = true, readerPageUnchanged = true, actualScale = window.RenderScaling };
        }
        finally { window.RemoveHandler(InputElement.KeyUpEvent, KeyUp); window.Surface.Focus(); }
    }
    private static async Task<object> AwaitDesktopCopyAsync(PrototypeWindow window, string output, string signal)
    {
        var copied = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Complete(string text) => copied.TrySetResult(text);
        window.Surface.Copied += Complete;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var origin = window.Surface.TranslatePoint(new Point(0, 0), window)!.Value;
            await File.WriteAllTextAsync(Path.Combine(output, signal), JsonSerializer.Serialize(new
            { surfaceX = origin.X, surfaceY = origin.Y, width = window.Surface.Bounds.Width, height = window.Surface.Bounds.Height }));
            var value = await copied.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var reader = window.Reader;
            var start = Math.Min(reader.SelectionAnchor, reader.SelectionFocus);
            var end = Math.Max(reader.SelectionAnchor, reader.SelectionFocus);
            Require(end > start && value == reader.Document!.Text[start..end], "真实拖选复制与原文范围不一致。");
            var clipboard = await window.Clipboard!.GetTextAsync();
            Require(clipboard == value, "真实键盘复制后剪贴板内容错误。");
            return new { start, end, reader.SelectionAnchor, reader.SelectionFocus, copiedText = value, clipboardMatches = true,
                pageStart = reader.Page!.Start, pageEnd = reader.Page.End };
        }
        finally { window.Surface.Copied -= Complete; }
    }
    private static void SaveWindow(Window window, string path)
    {
        var scale = window.RenderScaling;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.Bounds.Width * scale), (int)Math.Ceiling(window.Bounds.Height * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(window); bitmap.Save(path);
    }
    private static async Task<object> CompareTextBoxAsync(Window owner)
    {
        var text = string.Concat(Enumerable.Repeat("　　中文标点、English text 与 📖。\n", 30));
        var options = new LayoutOptions(360, 220);
        var document = new ReaderDocument(text);
        using var layout = new NativeLayout(document, options);
        var page = layout.Locate(0);
        var expected = layout.Lines(page).Select(x => x.Line.Length).ToArray();
        var box = new TextBox { Text = document.Text[page.Start..page.End], Width = options.Width,
            Height = options.Height, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            FontFamily = options.FontFamily, FontSize = options.FontSize, LineHeight = options.LineHeight };
        var host = new Window { Width = 420, Height = 300, Content = box, ShowInTaskbar = false };
        try
        {
            host.Show(owner);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var presenter = box.GetVisualDescendants().OfType<TextPresenter>().Single();
            var actual = presenter.TextLayout.TextLines.Where(l => l.Length > 0).Select(l => l.Length).ToArray();
            return new { nativeLineLengths = expected, defaultReadOnlyTextBoxLineLengths = actual,
                defaultTemplateMatches = expected.SequenceEqual(actual), box.Padding, box.BorderThickness,
                decision = "Use one ReaderSurface/TextLayout for rendering, hit-testing and selection. This comparison covers the default TextBox template only; no claim that all tuned TextBoxes must fail." };
        }
        finally { host.Close(); }
    }
}
