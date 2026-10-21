using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using ReadJournal.Services;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace ReadJournal.UnitTest;
public class ArchitectureTests
{
    private static readonly Lazy<HeadlessUnitTestSession> UiSession = new(() => HeadlessUnitTestSession.StartNew(typeof(ArchitectureTests)));
    public static AppBuilder BuildAvaloniaApp()
    {
        Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString()));
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
    private static void ResetLocatorForNewApplication() => typeof(ServiceLocator).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    private static Task OnUiThread(Action action) => UiSession.Value.Dispatch(() =>
    {
        ResetLocatorForNewApplication(); action();
    }, CancellationToken.None);
    private static Task OnUiThread(Func<Task> action) => UiSession.Value.Dispatch(async () =>
    {
        ResetLocatorForNewApplication(); await action(); return true;
    }, CancellationToken.None);
    [Fact]
    public Task ActualContainerResolvesAllRegisteredSingletonsAndInjectsConstructors() => OnUiThread(() =>
    {
        var locator = ServiceLocator.Current;
        Assert.Same(locator, Application.Current!.Resources[nameof(ServiceLocator)]);
        var provider = (IServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator)!;
        Assert.IsType<ServiceProvider>(provider);
        foreach (var property in typeof(ServiceLocator).GetProperties().Where(p => p.PropertyType.IsSubclassOf(typeof(ViewModelBase))))
        {
            var first = property.GetValue(locator); Assert.NotNull(first); Assert.Same(first, property.GetValue(locator)); Assert.Same(first, provider.GetService(property.PropertyType));
        }
        foreach (var type in typeof(IDatabaseService).Assembly.GetTypes().Where(t => t.IsInterface && t.Namespace == typeof(IDatabaseService).Namespace)) { var service = provider.GetService(type); Assert.NotNull(service); Assert.Same(service, provider.GetService(type)); }
        Assert.Same(provider.GetService<IMenuNavigationService>(), typeof(MainViewModel).GetField("_menu", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.MainViewModel));
        Assert.Same(provider.GetService<IBookStorage>(), typeof(BookDetailViewModel).GetField("_books", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.BookDetailViewModel));
    });
    [Fact]
    public Task ThreeLayersParametersRepeatedTargetAndBackStack() => OnUiThread(async () =>
    {
        var locator = ServiceLocator.Current; var main = locator.MainViewModel;
        new RootNavigationService().NavigateTo(RootNavigationConstant.MainView);
        Assert.Same(main, locator.MainWindowViewModel.Content);
        new MenuNavigationService().NavigateTo(MenuNavigationConstant.BookListView);
        Assert.Single(main.ContentStack);
        new ContentNavigationService().NavigateTo(ContentNavigationConstant.BookDetailView, 101);
        Assert.Equal(2, main.ContentStack.Count);
        new ContentNavigationService().NavigateTo(ContentNavigationConstant.BookDetailView, 202);
        Assert.Equal(2, main.ContentStack.Count);
        Assert.Equal(101, typeof(BookDetailViewModel).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.BookDetailViewModel));
        await main.GoBackCommand.ExecuteAsync(null); Assert.Same(locator.BookListViewModel, main.Content);
        if (locator.BookListViewModel.OnLoadedCommand.ExecutionTask is { } listLoad) await listLoad;
        main.GoBack(); Assert.Single(main.ContentStack);
        new ContentNavigationService().NavigateTo(ContentNavigationConstant.BookDetailView, 202);
        Assert.Equal(202, typeof(BookDetailViewModel).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.BookDetailViewModel));
        Assert.Null(locator.BookDetailViewModel.Book);
        new MenuNavigationService().NavigateTo(MenuNavigationConstant.OverviewView); Assert.Single(main.ContentStack); Assert.Same(locator.OverviewViewModel, main.Content);
        Assert.Throws<ArgumentException>(() => new ContentNavigationService().NavigateTo(MenuNavigationConstant.BookListView));
    });
    [Fact]
    public Task ReflectionViewLocatorCachesAndSetsDataContext() => OnUiThread(() =>
    {
        var locator = new ViewLocator(); var vm = ServiceLocator.Current.OverviewViewModel;
        Assert.True(locator.Match(vm)); var first = locator.Build(vm); Assert.IsType<ReadJournal.Views.OverviewView>(first);
        Assert.Same(vm, first!.DataContext); Assert.Same(first, locator.Build(vm)); Assert.False(locator.Match(new object()));
    });
    [Fact]
    public Task InitializationFailureStaysAndPublicRetrySucceeds() => OnUiThread(async () =>
    {
        var db = new Mock<IDatabaseService>(); var root = new Mock<IRootNavigationService>(); var menu = new Mock<IMenuNavigationService>();
        db.SetupSequence(x => x.InitializeAsync()).ThrowsAsync(new IOException("模拟初始化失败")).Returns(Task.CompletedTask);
        var vm = new InitializationViewModel(db.Object, root.Object, menu.Object);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.Contains("模拟初始化失败", vm.Error); Assert.False(vm.IsBusy);
        root.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("", vm.Error);
        root.Verify(x => x.NavigateTo(RootNavigationConstant.MainView), Times.Once);
        menu.Verify(x => x.NavigateTo(MenuNavigationConstant.OverviewView, null), Times.Once);
    });
    [Fact]
    public Task SingletonDetailResetsOldBookBeforeLoadingAnother() => OnUiThread(async () =>
    {
        var locator = ServiceLocator.Current;
        var provider = (IServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator)!;
        var books = provider.GetRequiredService<IBookStorage>();
        var first = new ReadJournal.Library.Models.Book { Title = "导航 A", TotalPages = 100 }; var second = new ReadJournal.Library.Models.Book { Title = "导航 B", TotalPages = 200 };
        await books.SaveAsync(first); await books.SaveAsync(second);
        var menu = new MenuNavigationService(); var content = new ContentNavigationService();
        menu.NavigateTo(MenuNavigationConstant.BookListView); content.NavigateTo(ContentNavigationConstant.BookDetailView, first.Id);
        await locator.BookDetailViewModel.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("导航 A", locator.BookDetailViewModel.Book!.Title);
        locator.MainViewModel.GoBack();
        if (locator.BookListViewModel.OnLoadedCommand.ExecutionTask is { } listLoad) await listLoad;
        content.NavigateTo(ContentNavigationConstant.BookDetailView, second.Id);
        Assert.Null(locator.BookDetailViewModel.Book); Assert.Empty(locator.BookDetailViewModel.RecordsList);
        await locator.BookDetailViewModel.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("导航 B", locator.BookDetailViewModel.Book!.Title);
    });

    [Theory]
    [InlineData("direct-menu")]
    [InlineData("direct-content")]
    [InlineData("add-quote")]
    [InlineData("edit-quote")]
    public Task ActualNavigationServicesRejectBusyPageBeforeChangingTargetParameter(string route) => OnUiThread(async () =>
    {
        var locator = ServiceLocator.Current; var main = locator.MainViewModel;
        var page = new BusyPage();
        main.SetMenuAndContent(MenuNavigationConstant.QuoteListView, page);
        var target = locator.QuoteEditViewModel;
        target.SetParameter(null!); target.Text = "Unchanged draft";
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = page.StartAsync(pending.Task);
        bool stayed; string draft;
        try
        {
            switch (route)
            {
                case "direct-menu": new MenuNavigationService().NavigateTo(MenuNavigationConstant.SettingsView); break;
                case "direct-content": new ContentNavigationService().NavigateTo(ContentNavigationConstant.QuoteEditView); break;
                case "add-quote": locator.QuoteListViewModel.AddQuoteCommand.Execute(null); break;
                case "edit-quote": locator.QuoteListViewModel.EditQuoteCommand.Execute(new QuoteItem { Quote = new Quote { Id = 19 } }); break;
            }
            stayed = ReferenceEquals(page, main.Content); draft = target.Text;
        }
        finally { pending.TrySetResult(); await operation; }
        Assert.True(stayed, "Direct navigation must not bypass the pending-operation guard.");
        Assert.Equal("Unchanged draft", draft);
        Assert.Single(main.ContentStack);
        new ContentNavigationService().NavigateTo(ContentNavigationConstant.QuoteEditView);
        Assert.Same(target, main.Content); Assert.Equal("", target.Text);
        main.SetMenuAndContent(MenuNavigationConstant.OverviewView, locator.OverviewViewModel);
    });

    private sealed class BusyPage : ViewModelBase
    {
        public Task StartAsync(Task operation) => RunAsync(() => operation);
    }

}
