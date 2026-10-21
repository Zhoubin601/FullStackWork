using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using ReadJournal;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;

var output = args[0];
SynchronizationContext.SetSynchronizationContext(null);
Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", Path.Combine(Path.GetTempPath(), "readjournal-architecture-probe-" + Guid.NewGuid().ToString("N")));
AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
SynchronizationContext.SetSynchronizationContext(null);
var checks = new List<object>();
void Check(string name, bool ok, object? evidence = null) { checks.Add(new { name, passed = ok, evidence }); if (!ok) throw new Exception("FAILED: " + name); }
var locator = ServiceLocator.Current;
Check("Current equals application resource", ReferenceEquals(locator, Application.Current!.Resources[nameof(ServiceLocator)]));
var provider = (IServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator)!;
Check("Actual Microsoft DI container", provider is ServiceProvider);
var viewLocator = new ViewLocator();
var vmProperties = typeof(ServiceLocator).GetProperties().Where(p => p.PropertyType.IsSubclassOf(typeof(ViewModelBase))).ToArray();
foreach (var property in vmProperties)
{
    var vm = (ViewModelBase)property.GetValue(locator)!;
    Check("Singleton VM " + property.Name, vm != null && ReferenceEquals(vm, property.GetValue(locator)) && ReferenceEquals(vm, provider.GetService(property.PropertyType)));
    if (vm is MainWindowViewModel) continue;
    var first = viewLocator.Build(vm);
    var expectedName = vm!.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal).Replace("ReadJournal.Library.", "ReadJournal.", StringComparison.Ordinal);
    Check("Reflect/create/cache/bind " + property.Name, first!.GetType().FullName == expectedName && ReferenceEquals(first.DataContext, vm) && ReferenceEquals(first, viewLocator.Build(vm)), first.GetType().FullName);
}
var interfaces = typeof(IDatabaseService).Assembly.GetTypes().Where(t => t.IsInterface && t.Namespace == typeof(IDatabaseService).Namespace).ToArray();
foreach (var type in interfaces) { var first = provider.GetService(type); Check("Singleton service " + type.Name, first != null && ReferenceEquals(first, provider.GetService(type)), first?.GetType().FullName); }
var root = provider.GetRequiredService<IRootNavigationService>();
var menu = provider.GetRequiredService<IMenuNavigationService>();
var content = provider.GetRequiredService<IContentNavigationService>();
var main = locator.MainViewModel;
foreach (var field in typeof(RootNavigationConstant).GetFields(BindingFlags.Public | BindingFlags.Static))
{
    root.NavigateTo((string)field.GetRawConstantValue()!);
    var expected = (ViewModelBase)typeof(ServiceLocator).GetProperty(field.Name + "Model")!.GetValue(locator)!;
    Check("Root route " + field.Name, ReferenceEquals(locator.MainWindowViewModel.Content, expected));
}
int Version(ViewModelBase vm) => (int)typeof(ViewModelBase).GetProperty("ParameterVersion", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
foreach (var field in typeof(MenuNavigationConstant).GetFields(BindingFlags.Public | BindingFlags.Static))
{
    var expected = (ViewModelBase)typeof(ServiceLocator).GetProperty(field.Name + "Model")!.GetValue(locator)!;
    var old = Version(expected);
    menu.NavigateTo((string)field.GetRawConstantValue()!, new object());
    Check("Menu route + parameter + stack " + field.Name, ReferenceEquals(main.Content, expected) && main.ContentStack.Count == 1 && Version(expected) == old + 1 && main.SelectedMenuItem!.View == (string)field.GetRawConstantValue()!);
}
var parameters = new Dictionary<string, object> { [ContentNavigationConstant.BookDetailView] = 37, [ContentNavigationConstant.BookEditView] = 41, [ContentNavigationConstant.QuoteEditView] = new Quote { BookId = 43, Text = "probe" }, [ContentNavigationConstant.ReadingPlanEditView] = new ReadingPlan { BookId = 47 } };
foreach (var field in typeof(ContentNavigationConstant).GetFields(BindingFlags.Public | BindingFlags.Static))
{
    menu.NavigateTo(MenuNavigationConstant.BookListView);
    var expected = (ViewModelBase)typeof(ServiceLocator).GetProperty(field.Name + "Model")!.GetValue(locator)!;
    var old = Version(expected);
    content.NavigateTo((string)field.GetRawConstantValue()!, parameters[field.Name]);
    Check("Content route + parameter + push " + field.Name, ReferenceEquals(main.Content, expected) && main.ContentStack.Count == 2 && Version(expected) == old + 1);
    content.NavigateTo((string)field.GetRawConstantValue()!, parameters[field.Name]);
    Check("Duplicate target unchanged " + field.Name, main.ContentStack.Count == 2 && Version(expected) == old + 1);
    main.GoBack();
    if (locator.BookListViewModel.OnLoadedCommand.ExecutionTask is { } loading) await loading;
    Check("Back from " + field.Name, ReferenceEquals(main.Content, locator.BookListViewModel) && main.ContentStack.Count == 1);
    main.GoBack();
    Check("Stack bottom protected " + field.Name, main.ContentStack.Count == 1);
}
Check("Detail parameter reached field", (int)typeof(BookDetailViewModel).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.BookDetailViewModel)! == 37);
Check("Book editor parameter reached field", (int)typeof(BookEditViewModel).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.BookEditViewModel)! == 41);
Check("Quote editor parameter reached field", ((Quote)typeof(QuoteEditViewModel).GetField("_quote", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.QuoteEditViewModel)!).BookId == 43);
Check("Plan editor parameter reached field", ((ReadingPlan)typeof(ReadingPlanEditViewModel).GetField("_plan", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.ReadingPlanEditViewModel)!).BookId == 47);
var assemblyHashes = new[] { typeof(ServiceLocator).Assembly, typeof(ViewModelBase).Assembly }.ToDictionary(a => a.GetName().Name!, a => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant());
File.WriteAllText(output, JsonSerializer.Serialize(new { date = "2026-10-21", scope = "Windows本机当前Release DLL，只读复核", assembly_sha256 = assemblyHashes, concrete_view_models = vmProperties.Length, reflected_views = vmProperties.Length - 1, interfaces = interfaces.Length, checks_count = checks.Count, passed = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS: {checks.Count} checks; {vmProperties.Length} VM singletons; {vmProperties.Length - 1} reflection views; {interfaces.Length} service interfaces; all 12 route constants.");
