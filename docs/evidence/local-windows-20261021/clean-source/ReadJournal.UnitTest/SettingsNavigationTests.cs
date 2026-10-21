using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using Xunit;
namespace ReadJournal.UnitTest;

public class SettingsNavigationTests
{
    [Fact]
    public async Task PendingRestorePreventsMenuNavigation()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scenario = NewScenario(pending.Task);
        var restoring = scenario.Settings.RestoreCommand.ExecuteAsync(null);
        Assert.True(scenario.Settings.IsBusy);
        try
        {
            await scenario.Main.OnMenuTappedCommand.ExecuteAsync(LibraryMenu());
            Assert.Same(scenario.Settings, scenario.Main.Content);
            scenario.Menu.Verify(x => x.NavigateTo(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }
        finally { pending.TrySetResult("auto-backup.json"); await restoring; }
    }

    [Fact]
    public async Task PendingRestorePreventsWindowClose()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scenario = NewScenario(pending.Task);
        var restoring = scenario.Settings.RestoreCommand.ExecuteAsync(null);
        Assert.True(scenario.Settings.IsBusy);
        try { Assert.False(await scenario.Main.CanCloseAsync()); }
        finally { pending.TrySetResult("auto-backup.json"); await restoring; }
    }

    [Fact]
    public async Task FailedRestoreReleasesNavigationAndCloseProtection()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scenario = NewScenario(pending.Task);
        var restoring = scenario.Settings.RestoreCommand.ExecuteAsync(null);
        pending.SetException(new IOException("备份读取失败")); await restoring;
        Assert.Contains("备份读取失败", scenario.Settings.Error); Assert.False(scenario.Settings.IsBusy);
        Assert.True(await scenario.Main.CanCloseAsync());
        await scenario.Main.OnMenuTappedCommand.ExecuteAsync(LibraryMenu());
        Assert.Same(scenario.Library, scenario.Main.Content);
    }

    [Fact]
    public async Task SuccessfulRestoreReleasesNavigationAndCloseProtection()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scenario = NewScenario(pending.Task);
        var restoring = scenario.Settings.RestoreCommand.ExecuteAsync(null);
        pending.SetResult("auto-backup.json"); await restoring;
        Assert.Contains("恢复成功", scenario.Settings.Status); Assert.False(scenario.Settings.IsBusy);
        Assert.True(await scenario.Main.CanCloseAsync());
        await scenario.Main.OnMenuTappedCommand.ExecuteAsync(LibraryMenu());
        Assert.Same(scenario.Library, scenario.Main.Content);
    }

    private static MenuItem LibraryMenu() => MenuItem.MenuItems.Single(x => x.View == MenuNavigationConstant.BookListView);
    private static (SettingsViewModel Settings, MainViewModel Main, BookListViewModel Library, Mock<IMenuNavigationService> Menu) NewScenario(Task<string> restore)
    {
        var files = new Mock<IFileService>(); files.Setup(x => x.ChooseOpenPathAsync()).ReturnsAsync("source.json");
        var backup = new Mock<IBackupService>(); backup.Setup(x => x.RestoreAsync("source.json")).Returns(restore);
        var alerts = new Mock<IAlertService>(); alerts.Setup(x => x.ConfirmAsync("恢复备份", It.IsAny<string>())).ReturnsAsync(true);
        var menu = new Mock<IMenuNavigationService>(); var main = new MainViewModel(menu.Object);
        var books = new Mock<IBookStorage>(); books.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new PageResult<BookProgress>());
        var library = new BookListViewModel(books.Object, Mock.Of<IContentNavigationService>());
        menu.Setup(x => x.NavigateTo(MenuNavigationConstant.BookListView, null)).Callback(() => main.SetMenuAndContent(MenuNavigationConstant.BookListView, library));
        var settings = new SettingsViewModel(Mock.Of<IPreferenceStorage>(), backup.Object, files.Object, alerts.Object);
        main.SetMenuAndContent(MenuNavigationConstant.SettingsView, settings);
        return (settings, main, library, menu);
    }
}
