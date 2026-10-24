using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using ReadJournal;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Reader;
using ReadJournal.Views;

// Native Linux/X11 probe: real production DI, controls, mouse selection and confirmation dialog, local Mock only.
if (args.Length != 3) throw new ArgumentException("Usage: <new-isolated-work> <evidence-directory> <xdotool>");
var work = Path.GetFullPath(args[0]); var evidence = Path.GetFullPath(args[1]); var xdotool = Path.GetFullPath(args[2]);
if (Directory.Exists(work)) throw new ArgumentException("Use a new isolated work directory.");
Directory.CreateDirectory(work); Directory.CreateDirectory(evidence);
Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", Path.Combine(work, "data"));
var checks = new List<string>();
var exit = 1;
AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace().AfterSetup(_ =>
    Dispatcher.UIThread.Post(async () =>
    {
        try { await Run(); exit = 0; }
        catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(evidence, "desktop-error.txt"), ex.ToString()); Console.Error.WriteLine(ex); }
        finally { ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).Shutdown(exit); }
    })).StartWithClassicDesktopLifetime([]);
return exit;

void Check(string name, bool passed)
{
    if (!passed) throw new InvalidOperationException(name);
    checks.Add(name); Console.WriteLine(name);
}
async Task Wait(Func<bool> predicate)
{
    var timer = Stopwatch.StartNew();
    while (!predicate()) { if (timer.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Native UI did not settle."); await Task.Delay(25); }
    await Task.Delay(100);
}
async Task Exec(string file, params string[] arguments)
{
    var info = new ProcessStartInfo(file) { UseShellExecute = false };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info)!;
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new InvalidOperationException(file + " exited " + process.ExitCode);
}
async Task Click(Control control)
{
    var owner = (Window)TopLevel.GetTopLevel(control)!;
    var local = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), owner)!.Value;
    await Exec(xdotool, "mousemove", (owner.Position.X + (int)(local.X * owner.RenderScaling)).ToString(),
        (owner.Position.Y + (int)(local.Y * owner.RenderScaling)).ToString(), "click", "1");
    await Task.Delay(150);
}
async Task Shot(string name)
{
    await Task.Delay(150);
    await Exec("import", "-window", "root", Path.Combine(evidence, name + ".png"));
}
async Task Run()
{
    var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
    var locator = ServiceLocator.Current;
    var provider = (IServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator)!;
    var db = provider.GetRequiredService<IDatabaseService>();
    await Wait(() => db.IsInitialized && locator.MainWindowViewModel.Content is not null);
    var settings = await provider.GetRequiredService<IAiSettingsStorage>().GetAsync(default);
    Check("Mock only; no credential", settings.UseMock && !await provider.GetRequiredService<ISecretStore>().HasApiKeyAsync(default));
    var source = Path.Combine(work, "圈选合成对话.txt");
    await File.WriteAllTextAsync(source, "第一章 合成对话\n" + string.Concat(Enumerable.Repeat("“孩子问：为什么还没有回来？”\n“朋友回答：先坐下来，我们一起想办法。”\n两人沉默了一会儿，终于决定互相照顾。\n", 30)));
    var book = await provider.GetRequiredService<IEbookLibraryService>().ImportAsync(new(source), null, default);
    Check("Synthetic TXT imported", book.State == ImportState.Imported);
    await provider.GetRequiredService<IMenuNavigationService>().NavigateToAsync(MenuNavigationConstant.BookListView);
    await provider.GetRequiredService<IContentNavigationService>().NavigateToAsync(ContentNavigationConstant.ReaderView, new ReaderOpenRequest(book.BookId!.Value));
    var vm = locator.ReaderViewModel; var window = lifetime.MainWindow!;
    window.Position = new PixelPoint(0, 0); window.Width = 1380; window.Height = 940;
    await Wait(() => vm.Phase == ReaderPhase.Active && !vm.IsLayoutBusy);
    var view = window.GetVisualDescendants().OfType<ReaderView>().Single();
    var surface = view.FindControl<ReaderSurface>("Surface")!;
    var expanded = surface.Bounds.Size; var position = vm.Position; var context = vm.Context;
    await Shot("01-expanded");
    for (var mask = 7; mask >= 0; mask--)
    {
        foreach (var (name, enabled) in new[] { ("TocToggle", (mask & 1) != 0), ("AppearanceToggle", (mask & 2) != 0), ("AiToggle", (mask & 4) != 0) })
        {
            var toggle = view.FindControl<ToggleButton>(name)!;
            if (toggle.IsChecked != enabled) await Click(toggle);
        }
        await Wait(() => !vm.IsLayoutBusy && vm.Page is not null);
        Check("Panel combination " + mask, vm.IsTocExpanded == ((mask & 1) != 0) && vm.IsAppearanceExpanded == ((mask & 2) != 0) && vm.IsAiExpanded == ((mask & 4) != 0));
        Check("Stable position and session " + mask, vm.Position == position && vm.Context == context);
    }
    Check("Collapsed panels release space", surface.Bounds.Width > expanded.Width + 400 && surface.Bounds.Height > expanded.Height);
    await Shot("02-collapsed");
    await Click(view.FindControl<ToggleButton>("AiToggle")!);
    await Wait(() => !vm.IsLayoutBusy);
    // Drag across several rendered lines using actual XTest input. The resulting selected text is read from managed original.
    var origin = surface.TranslatePoint(new Point(70, 70), window)!.Value;
    var end = surface.TranslatePoint(new Point(330, 185), window)!.Value;
    await Exec(xdotool, "mousemove", ((int)origin.X).ToString(), ((int)origin.Y).ToString(), "mousedown", "1");
    await Exec(xdotool, "mousemove", ((int)end.X).ToString(), ((int)end.Y).ToString());
    await Task.Delay(150); await Exec(xdotool, "mouseup", "1");
    await Wait(() => vm.Selection is not null);
    var selected = vm.Selection!;
    var original = await provider.GetRequiredService<IReaderDocumentService>().ExtractAsync(selected, default);
    var selectedText = string.Concat(original.Spans.Select(s => s.Text));
    Check("Mouse selected multiple lines", selectedText.Length > 20 && selectedText.Contains('\n'));
    var combo = view.GetVisualDescendants().OfType<ComboBox>().Single(c => ReferenceEquals(c.ItemsSource, vm.AiScopes));
    await Click(combo); await Exec(xdotool, "key", "Home", "Down", "Return");
    await Wait(() => vm.AiScope == "当前选区");
    Check("Selection scope hides pages", !view.FindControl<Grid>("AiPageRangeInputs")!.IsVisible);
    await Click(view.FindControl<ToggleButton>("AiToggle")!); await Wait(() => !vm.IsLayoutBusy);
    await Click(view.FindControl<ToggleButton>("AiToggle")!); await Wait(() => !vm.IsLayoutBusy);
    Check("Folding with selection preserves original range", vm.Selection == selected);
    await Shot("03-selection");
    var generate = view.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "生成并自动保存书摘"));
    await Click(generate);
    await Wait(() => lifetime.Windows.Any(w => w != window && w.Title == "生成并自动保存书摘"));
    var dialog = lifetime.Windows.Single(w => w != window && w.Title == "生成并自动保存书摘");
    await Click(dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "确定")));
    await Wait(() => !vm.IsAiBusy);
    var quotes = provider.GetRequiredService<IQuoteStorage>();
    var saved = (await quotes.SearchAsync("", false)).Single();
    Check("One quote equals complete mouse selection", saved.Quote.Text == selectedText && saved.Source == QuoteSource.AiGenerated && !string.IsNullOrWhiteSpace(saved.Quote.Thoughts));
    Check("Clicking scope and generate preserves selection", vm.Selection == selected);
    await Shot("04-selection-saved");
    await vm.UndoAiCommand.ExecuteAsync(null);
    Check("Undo removes the one generated quote", (await quotes.SearchAsync("", false)).Count == 0);
    await Click(combo); await Exec(xdotool, "key", "End", "Return"); await Wait(() => vm.IsAiPageRange);
    Check("Page range shows page fields", view.FindControl<Grid>("AiPageRangeInputs")!.IsVisible);
    await Shot("05-page-range");
    Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    await Shot("06-dark");
    window.Width = 900; window.Height = 700; await Wait(() => !vm.IsLayoutBusy);
    await Shot("07-narrow");
    await Click(view.FindControl<ToggleButton>("AiToggle")!); await Wait(() => !vm.IsLayoutBusy);
    await Shot("08-narrow-reading");
    Check("Normal reader close", await vm.CanLeaveAsync());
    await File.WriteAllTextAsync(Path.Combine(evidence, "desktop.json"), JsonSerializer.Serialize(new
    {
        passed = true, platform = "Linux X11/Xvfb actual native window and xdotool mouse/keyboard; not Windows",
        checks, expanded, selectedCharacters = selectedText.Length, input = "synthetic text only", api = "local Mock only",
        finalContextClosed = vm.Context is null
    }, new JsonSerializerOptions { WriteIndented = true }));
}
