using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReadJournal;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Library.ViewModels;
using ReadJournal.Reader;

if (args.Length != 2 || Directory.Exists(args[1])) throw new ArgumentException("Usage: <million.txt> <new-isolated-root>");
var source = Path.GetFullPath(args[0]); var work = Path.GetFullPath(args[1]); Directory.CreateDirectory(work);
Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", Path.Combine(work, "data"));
using var session = HeadlessUnitTestSession.StartNew(typeof(Bootstrap));
await session.Dispatch(async () =>
{
    var checks = new List<object>();
    void Check(string name, bool ok) { checks.Add(new { name, passed = ok }); if (!ok) throw new InvalidOperationException(name); }
    var locator = ServiceLocator.Current;
    var provider = (IServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator)!;
    Check("Real Microsoft DI container", provider is ServiceProvider);
    Check("Resource locator identity", ReferenceEquals(locator, Application.Current!.Resources[nameof(ServiceLocator)]));
    var viewLocator = new ViewLocator();
    var properties = typeof(ServiceLocator).GetProperties().Where(p => p.PropertyType.IsSubclassOf(typeof(ViewModelBase))).ToArray();
    foreach (var property in properties)
    {
        var vm = (ViewModelBase)property.GetValue(locator)!;
        Check("Singleton VM " + property.Name, ReferenceEquals(vm, property.GetValue(locator)) && ReferenceEquals(vm, provider.GetService(property.PropertyType)));
        if (vm is MainWindowViewModel) continue;
        var view = viewLocator.Build(vm)!;
        Check("Reflect/cache/bind " + property.Name, ReferenceEquals(view.DataContext, vm) && ReferenceEquals(view, viewLocator.Build(vm)) && view.GetType().Name == vm.GetType().Name.Replace("ViewModel", "View"));
    }
    var excluded = new HashSet<string> { "IBookParser", "IReaderActionLease", "IDataOperationLease", "IDataMaintenanceLease", "IReaderRegistration" };
    var interfaces = typeof(IDatabaseService).Assembly.GetTypes().Where(t => t.IsInterface && (t.Namespace == "ReadJournal.Library.Services" || t.Namespace == "ReadJournal.Library.Services.Reader") && !excluded.Contains(t.Name)).ToArray();
    foreach (var type in interfaces)
    {
        var value = provider.GetService(type);
        Check("Singleton service " + type.Name, value is not null && ReferenceEquals(value, provider.GetService(type)));
    }
    var parsers = provider.GetServices<IBookParser>().ToArray();
    Check("Both concrete parser singletons", parsers.Length == 2 && parsers.SequenceEqual(provider.GetServices<IBookParser>()));
    Check("Reader layout concrete/interface identity", ReferenceEquals(locator.ReaderLayoutService, provider.GetRequiredService<IReaderLayoutService>()));
    Check("Document concrete/interface identity", ReferenceEquals(provider.GetRequiredService<ReaderDocumentService>(), provider.GetRequiredService<IReaderDocumentService>()));
    Check("AI requires upload consent and no session credential", !(await provider.GetRequiredService<IAiSettingsStorage>().GetAsync(default)).HasAcknowledgedTextUpload && !await provider.GetRequiredService<ISecretStore>().HasApiKeyAsync(default));
    var root = provider.GetRequiredService<IRootNavigationService>();
    foreach (var field in typeof(RootNavigationConstant).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        root.NavigateTo((string)field.GetRawConstantValue()!);
        Check("Root route " + field.Name, ReferenceEquals(locator.MainWindowViewModel.Content, typeof(ServiceLocator).GetProperty(field.Name + "Model")!.GetValue(locator)));
    }
    var menu = provider.GetRequiredService<IMenuNavigationService>();
    foreach (var field in typeof(MenuNavigationConstant).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        var ok = await menu.NavigateToAsync((string)field.GetRawConstantValue()!);
        Check("Menu route " + field.Name, ok && locator.MainViewModel.ContentStack.Count == 1 && ReferenceEquals(locator.MainViewModel.Content, typeof(ServiceLocator).GetProperty(field.Name + "Model")!.GetValue(locator)));
    }
    var library = provider.GetRequiredService<IEbookLibraryService>();
    var timer = Stopwatch.StartNew(); var imported = await library.ImportAsync(new(source), null, default); var coldImportMs = timer.Elapsed.TotalMilliseconds;
    Check("Actual million-character TXT imported", imported.State == ImportState.Imported);
    var small = Path.Combine(work, "small.txt"); await File.WriteAllTextAsync(small, "第一章 第二本书\n" + string.Concat(Enumerable.Repeat("本地合成段落，不上传正文。\n", 100)));
    var second = await library.ImportAsync(new(small), null, default); Check("Second book imported", second.State == ImportState.Imported);
    var navigation = provider.GetRequiredService<IContentNavigationService>();
    var arguments = new Dictionary<string, object> {
        [ContentNavigationConstant.BookDetailView] = imported.BookId!.Value,
        [ContentNavigationConstant.BookEditView] = imported.BookId.Value,
        [ContentNavigationConstant.QuoteEditView] = new Quote { BookId = imported.BookId.Value, Text = "合成原文" },
        [ContentNavigationConstant.ReadingPlanEditView] = new ReadingPlan { BookId = imported.BookId.Value },
        [ContentNavigationConstant.ReaderView] = new ReaderOpenRequest(imported.BookId.Value)
    };
    var reader = locator.ReaderViewModel; var layouts = locator.ReaderLayoutService;
    foreach (var field in typeof(ContentNavigationConstant).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        await menu.NavigateToAsync(MenuNavigationConstant.BookListView);
        var ok = await navigation.NavigateToAsync((string)field.GetRawConstantValue()!, arguments[field.Name]);
        if (field.Name == ContentNavigationConstant.ReaderView) await reader.PagePresentedAsync(reader.Page!.Ref.Layout);
        Check("Content route " + field.Name, ok && locator.MainViewModel.ContentStack.Count == 2 && ReferenceEquals(locator.MainViewModel.Content, typeof(ServiceLocator).GetProperty(field.Name + "Model")!.GetValue(locator)));
        Check("Back route " + field.Name, await locator.MainViewModel.RequestBackAsync() && locator.MainViewModel.ContentStack.Count == 1);
    }
    var surface = new ReaderSurface { LayoutService = layouts };
    void Draw()
    {
        surface.Page = reader.Page;
        surface.Measure(new Size(760, 440)); surface.Arrange(new Rect(0, 0, 760, 440));
        using var bitmap = new RenderTargetBitmap(new PixelSize(1140, 660), new Vector(144, 144)); bitmap.Render(surface);
    }
    async Task Open(int id)
    {
        reader.SetParameter(new ReaderOpenRequest(id)); await reader.OnLoadedCommand.ExecuteAsync(null);
        Check("Open has page", reader.Page is not null && reader.Error == ""); Draw(); await reader.PagePresentedAsync(reader.Page!.Ref.Layout);
    }
    var heartbeats = new List<double>(); var heartbeatClock = Stopwatch.StartNew(); var lastHeartbeat = 0d;
    var heartbeat = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
    heartbeat.Tick += (_, _) => { var now = heartbeatClock.Elapsed.TotalMilliseconds; heartbeats.Add(now - lastHeartbeat); lastHeartbeat = now; };
    heartbeat.Start();
    var coldRender = Stopwatch.StartNew(); await Open(imported.BookId.Value); var firstOpenMs = coldRender.Elapsed.TotalMilliseconds;
    heartbeat.Stop();
    if (heartbeats.Count == 0 || heartbeats.Max() > 250) throw new InvalidOperationException("Production layout dispatcher heartbeat exceeds 250ms.");
    var flips = new List<double>();
    for (var i = 0; i < 200; i++) { timer.Restart(); await reader.NextCommand.ExecuteAsync(null); Draw(); flips.Add(timer.Elapsed.TotalMilliseconds); }
    var reflows = new List<double>();
    for (var i = 0; i < 20; i++) { timer.Restart(); await reader.SetViewportAsync(760 + i % 2 * 80, 440 + i % 2 * 40, 1.5); Draw(); reflows.Add(timer.Elapsed.TotalMilliseconds); }
    timer.Restart(); Check("First leave", await reader.CanLeaveAsync()); var leaveMs = timer.Elapsed.TotalMilliseconds;
    var documents = provider.GetRequiredService<ReaderDocumentService>();
    int DocumentCount() => ((IDictionary)typeof(ReaderDocumentService).GetField("_documents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(documents)!).Count;
    var memory = new List<object>();
    for (var i = 0; i < 20; i++)
    {
        await Open(i % 2 == 0 ? imported.BookId.Value : second.BookId!.Value); Check("Leave " + i, await reader.CanLeaveAsync());
        surface.Page = null; GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Check("No retained layout/document/image " + i, layouts.ActiveLayoutCount == 0 && layouts.DecodedImageCount == 0 && DocumentCount() == 0);
        memory.Add(new { cycle = i + 1, managedBytes = GC.GetTotalMemory(false), privateBytes = Process.GetCurrentProcess().PrivateMemorySize64, activeLayouts = layouts.ActiveLayoutCount, retainedDocuments = DocumentCount(), decodedImages = layouts.DecodedImageCount });
    }
    static double P95(List<double> rows) => rows.Order().ElementAt((int)Math.Ceiling(rows.Count * .95) - 1);
    var output = new { passed = true, environment = new { os = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(), processorCount = Environment.ProcessorCount, render = "Production ReaderViewModel/ReaderSurface with Skia offscreen; 760x440 DIP, raster1140x660; not a physical DPI acceptance" }, checksCount = checks.Count, vmCount = properties.Length, serviceInterfaceCount = interfaces.Length, checks,
        measurements = new { coldImportMs, firstOpenMs, firstOpenNote = "After architecture-route warmup; OS/disk/font caches warm; session load plus first bitmap draw", layoutDispatcherHeartbeatMs = heartbeats, layoutDispatcherMaximumGapMs = heartbeats.Max(), flipsMs = flips, flipP95Ms = P95(flips), reflowsMs = reflows, reflowP95Ms = P95(reflows), leaveDuringIndexMs = leaveMs, memory },
        goals = new { firstOpenUnder2Seconds = firstOpenMs <= 2000, flipP95Under100Ms = P95(flips) <= 100, reflowP95Under500Ms = P95(reflows) <= 500, allReferencesReleased = true } };
    await File.WriteAllTextAsync(Path.Combine(work, "probe.json"), JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(new { checks = checks.Count, firstOpenMs, flipP95Ms = P95(flips), reflowP95Ms = P95(reflows), output = Path.Combine(work, "probe.json") }));
    ((IDisposable)provider).Dispose();
    return true;
}, default);

public static class Bootstrap
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
}
