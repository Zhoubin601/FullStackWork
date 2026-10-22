using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using ReadJournal.Services;
using System.Text.Json;
using Xunit;

namespace ReadJournal.UnitTest;

public class RestoreConcurrencyTests
{
    [Theory]
    [InlineData("menu")]
    [InlineData("back")]
    [InlineData("close")]
    public async Task BusyPageCannotLeaveAndNavigationResumesAfterOperation(string action)
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var menu = new Mock<IMenuNavigationService>();
        var main = new MainViewModel(menu.Object);
        var root = new BusyPage(); var page = new BusyPage();
        main.PushContent(root); main.PushContent(page);
        var operation = page.StartAsync(pending.Task);
        bool blocked;
        try
        {
            if (action == "menu")
            {
                await main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.First());
                blocked = menu.Invocations.Count == 0;
            }
            else if (action == "back")
            {
                await main.GoBackCommand.ExecuteAsync(null);
                blocked = ReferenceEquals(main.Content, page);
            }
            else blocked = !await main.CanCloseAsync();
        }
        finally { pending.TrySetResult(); await operation; }
        Assert.True(blocked, "A pending operation must finish before leaving its page.");
        Assert.True(await page.CanLeaveAsync());
        if (action == "menu")
        {
            await main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.First());
            menu.Verify(x => x.NavigateTo(It.IsAny<string>(), null), Times.Once);
        }
        else if (action == "back") { await main.GoBackCommand.ExecuteAsync(null); Assert.Same(root, main.Content); }
        else Assert.True(await main.CanCloseAsync());
    }

    [Fact]
    public async Task FavoriteMustFinishBeforeEnteringRestoreAndRestoredFieldsRemainIntact()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new DataPaths(directory);
            using var db = new DatabaseService(paths);
            var reading = new ReadingService(); var books = new BookStorage(db, reading); var quotes = new QuoteStorage(db, reading);
            var book = new Book { Title = "Old book", TotalPages = 100 }; await books.SaveAsync(book);
            var quote = new Quote { BookId = book.Id, Text = "OLD quote text", Thoughts = "OLD thoughts", Page = 1 };
            await quotes.SaveAsync(quote);
            var input = Path.Combine(directory, "input.json");
            await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new BackupDocument
            {
                Books = [new Book { Id = book.Id, Title = "RESTORED book", TotalPages = 100, CreatedAt = book.CreatedAt, UpdatedAt = book.UpdatedAt }],
                Quotes = [new Quote { Id = quote.Id, BookId = book.Id, Text = "RESTORED quote text", Thoughts = "RESTORED thoughts", Page = 2, CreatedAt = quote.CreatedAt, UpdatedAt = quote.UpdatedAt }]
            }));
            var files = new DiskFiles(input); var backup = new BackupService(db, reading, files, paths);
            var menu = new Mock<IMenuNavigationService>(); var main = new MainViewModel(menu.Object);
            var alert = new Mock<IAlertService>(); alert.Setup(x => x.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            var list = new QuoteListViewModel(quotes, Mock.Of<IContentNavigationService>(), alert.Object);
            var settings = new SettingsViewModel(Mock.Of<IPreferenceStorage>(), backup, files, alert.Object);
            menu.Setup(x => x.NavigateTo(MenuNavigationConstant.SettingsView, null)).Callback(() =>
            {
                settings.SetParameter(null!); main.SetMenuAndContent(MenuNavigationConstant.SettingsView, settings);
            });
            main.SetMenuAndContent(MenuNavigationConstant.QuoteListView, list);
            await list.OnLoadedCommand.ExecuteAsync(null);
            using var release = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var blocking = db.ReadAsync(c =>
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test gate was not released.");
                return 0;
            });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var toggling = list.ToggleFavoriteCommand.ExecuteAsync(Assert.Single(list.QuotesList));
            bool stayed; bool couldClose;
            try
            {
                await main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.Single(x => x.View == MenuNavigationConstant.SettingsView));
                stayed = ReferenceEquals(list, main.Content); couldClose = await main.CanCloseAsync();
            }
            finally { release.Set(); await Task.WhenAll(blocking, toggling).WaitAsync(TimeSpan.FromSeconds(10)); }
            Assert.True(stayed); Assert.False(couldClose);
            Assert.True((await quotes.GetAsync(quote.Id))!.IsFavorite);
            await main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.Single(x => x.View == MenuNavigationConstant.SettingsView));
            Assert.Same(settings, main.Content);
            await settings.RestoreCommand.ExecuteAsync(null);
            Assert.Empty(settings.Error); Assert.StartsWith("恢复成功", settings.Status);
            var restored = (await quotes.GetAsync(quote.Id))!;
            Assert.Equal("RESTORED quote text", restored.Text); Assert.Equal("RESTORED thoughts", restored.Thoughts);
            Assert.Equal(2, restored.Page); Assert.False(restored.IsFavorite);
            Assert.Equal("RESTORED book", (await books.GetAsync(book.Id))!.Title);
            var automatic = ReaderBackupTestData.Legacy(Assert.Single(Directory.GetFiles(Path.Combine(directory,"backups"), "auto-backup-*.zip")));
            Assert.True(Assert.Single(automatic.Quotes).IsFavorite);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ConcurrentFavoriteChangesAreAtomicAndPreserveOtherQuoteFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var db = new DatabaseService(new DataPaths(directory)); var reading = new ReadingService();
            var books = new BookStorage(db, reading); var quotes = new QuoteStorage(db, reading);
            var book = new Book { Title = "原子收藏", TotalPages = 100 }; await books.SaveAsync(book);
            var quote = new Quote { BookId = book.Id, Text = "保留正文", Thoughts = "保留感想", Page = 12 };
            await quotes.SaveAsync(quote);
            await Task.WhenAll(quotes.ToggleFavoriteAsync(quote.Id), quotes.ToggleFavoriteAsync(quote.Id));
            var actual = (await quotes.GetAsync(quote.Id))!;
            Assert.False(actual.IsFavorite);
            Assert.Equal(quote.Text, actual.Text); Assert.Equal(quote.Thoughts, actual.Thoughts); Assert.Equal(quote.Page, actual.Page);
            Assert.Equal(quote.BookId, actual.BookId); Assert.Equal(quote.CreatedAt, actual.CreatedAt); Assert.True(actual.UpdatedAt >= quote.UpdatedAt);
            await quotes.ToggleFavoriteAsync(quote.Id);
            Assert.True((await quotes.GetAsync(quote.Id))!.IsFavorite);
            await quotes.DeleteAsync(quote.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => quotes.ToggleFavoriteAsync(quote.Id));
            Assert.Null(await quotes.GetAsync(quote.Id));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task FailedBusyOperationReleasesNavigationGuard()
    {
        var page = new BusyPage(); var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = page.StartAsync(pending.Task);
        Assert.False(await page.CanLeaveAsync());
        pending.SetException(new IOException("模拟失败")); await operation;
        Assert.False(page.IsBusy); Assert.Equal("模拟失败", page.Error);
        Assert.True(await page.CanLeaveAsync());
    }

    private sealed class BusyPage : ViewModelBase
    {
        public Task StartAsync(Task operation) => RunAsync(() => operation);
    }

    private sealed class DiskFiles(string input) : IFileService
    {
        public Task<string?> ChooseSavePathAsync() => Task.FromResult<string?>(null);
        public Task<string?> ChooseOpenPathAsync() => Task.FromResult<string?>(input);
        public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
        public Task WriteTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);
    }
}
