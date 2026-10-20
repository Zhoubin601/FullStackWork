using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using ReadJournal.Services;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace ReadJournal.UnitTest;
public class ArchitectureTests
{
    static ArchitectureTests()
    {
        Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString()));
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
    }
    [Fact]
    public void ActualContainerResolvesAllRegisteredSingletonsAndInjectsConstructors()
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
    }
    [Fact]
    public async Task ThreeLayersParametersRepeatedTargetAndBackStack()
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
        main.GoBack(); Assert.Single(main.ContentStack);
        new ContentNavigationService().NavigateTo(ContentNavigationConstant.BookDetailView, 202);
        Assert.Equal(202, typeof(BookDetailViewModel).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator.BookDetailViewModel));
        Assert.Null(locator.BookDetailViewModel.Book);
        new MenuNavigationService().NavigateTo(MenuNavigationConstant.OverviewView); Assert.Single(main.ContentStack); Assert.Same(locator.OverviewViewModel, main.Content);
        Assert.Throws<ArgumentException>(() => new ContentNavigationService().NavigateTo(MenuNavigationConstant.BookListView));
    }
    [Fact]
    public void ReflectionViewLocatorCachesAndSetsDataContext()
    {
        var locator = new ViewLocator(); var vm = ServiceLocator.Current.OverviewViewModel;
        Assert.True(locator.Match(vm)); var first = locator.Build(vm); Assert.IsType<ReadJournal.Views.OverviewView>(first);
        Assert.Same(vm, first!.DataContext); Assert.Same(first, locator.Build(vm)); Assert.False(locator.Match(new object()));
    }
    [Fact]
    public async Task InitializationFailureStaysAndPublicRetrySucceeds()
    {
        var db = new Mock<IDatabaseService>(); var root = new Mock<IRootNavigationService>(); var menu = new Mock<IMenuNavigationService>();
        db.SetupSequence(x => x.InitializeAsync()).ThrowsAsync(new IOException("模拟初始化失败")).Returns(Task.CompletedTask);
        var vm = new InitializationViewModel(db.Object, root.Object, menu.Object);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.Contains("模拟初始化失败", vm.Error); Assert.False(vm.IsBusy);
        root.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("", vm.Error);
        root.Verify(x => x.NavigateTo(RootNavigationConstant.MainView), Times.Once);
        menu.Verify(x => x.NavigateTo(MenuNavigationConstant.OverviewView, null), Times.Once);
    }
    [Fact]
    public async Task SingletonDetailResetsOldBookBeforeLoadingAnother()
    {
        var locator = ServiceLocator.Current;
        var provider = (IServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(locator)!;
        var books = provider.GetRequiredService<IBookStorage>();
        var first = new ReadJournal.Library.Models.Book { Title = "导航 A", TotalPages = 100 }; var second = new ReadJournal.Library.Models.Book { Title = "导航 B", TotalPages = 200 };
        await books.SaveAsync(first); await books.SaveAsync(second);
        var menu = new MenuNavigationService(); var content = new ContentNavigationService();
        menu.NavigateTo(MenuNavigationConstant.BookListView); content.NavigateTo(ContentNavigationConstant.BookDetailView, first.Id);
        await locator.BookDetailViewModel.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("导航 A", locator.BookDetailViewModel.Book!.Title);
        locator.MainViewModel.GoBack(); content.NavigateTo(ContentNavigationConstant.BookDetailView, second.Id);
        Assert.Null(locator.BookDetailViewModel.Book); Assert.Empty(locator.BookDetailViewModel.RecordsList);
        await locator.BookDetailViewModel.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("导航 B", locator.BookDetailViewModel.Book!.Title);
    }

}
