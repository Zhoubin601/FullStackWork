using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using Xunit;
namespace ReadJournal.UnitTest;

public class DeleteNavigationRaceTests
{
    [Fact]
    public async Task PendingImpactReadCannotDeleteBookOpenedByLaterNavigation()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = NewBooks(); books.Setup(x => x.GetDeleteImpactAsync(1)).Returns(pending.Task);
        var alerts = NewAlerts(); var scenario = NewScenario(books.Object, alerts.Object);
        scenario.Detail.SetParameter(1); scenario.Main.PushContent(scenario.Detail);
        var deleting = scenario.Detail.DeleteBookCommand.ExecuteAsync(null);
        Assert.True(scenario.Detail.IsBusy);

        // User navigation is blocked while busy; a forced context reset must still be safe.
        await scenario.Main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.Single(x => x.View == MenuNavigationConstant.BookListView));
        Assert.Same(scenario.Detail, scenario.Main.Content);
        scenario.Main.SetMenuAndContent(MenuNavigationConstant.BookListView, scenario.List);
        scenario.Detail.SetParameter(2); scenario.Main.PushContent(scenario.Detail);
        pending.SetResult("书籍 A 的关联统计"); await deleting;

        books.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        alerts.Verify(x => x.ConfirmAsync("删除书籍", It.IsAny<string>()), Times.Never);
        Assert.Same(scenario.Detail, scenario.Main.Content);
    }

    [Fact]
    public async Task CompletionOfOldDeletionCannotDiscardDraftOnNewPage()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = NewBooks(); books.Setup(x => x.GetDeleteImpactAsync(1)).ReturnsAsync("关联统计");
        books.Setup(x => x.DeleteAsync(1)).Returns(pending.Task);
        var alerts = NewAlerts(); var scenario = NewScenario(books.Object, alerts.Object);
        scenario.Detail.SetParameter(1); scenario.Main.PushContent(scenario.Detail);
        var deleting = scenario.Detail.DeleteBookCommand.ExecuteAsync(null);
        Assert.True(scenario.Detail.IsBusy);

        await scenario.Main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.Single(x => x.View == MenuNavigationConstant.BookListView));
        var editor = new BookEditViewModel(books.Object, alerts.Object, scenario.Main);
        editor.SetParameter(null!); editor.Title = "另一本书的新草稿"; scenario.Main.PushContent(editor);
        pending.SetResult(true); await deleting;

        books.Verify(x => x.DeleteAsync(1), Times.Once);
        Assert.Same(editor, scenario.Main.Content); Assert.True(editor.IsDirty);
        alerts.Verify(x => x.ConfirmLeaveAsync(), Times.Never);
    }

    [Fact]
    public async Task PendingConfirmationCannotDeleteAfterDetailParameterChanges()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = NewBooks(); books.Setup(x => x.GetDeleteImpactAsync(1)).ReturnsAsync("书籍 A 的关联统计");
        var alerts = NewAlerts(); alerts.Setup(x => x.ConfirmAsync("删除书籍", It.IsAny<string>())).Returns(pending.Task);
        var scenario = NewScenario(books.Object, alerts.Object);
        scenario.Detail.SetParameter(1); scenario.Main.PushContent(scenario.Detail);
        var deleting = scenario.Detail.DeleteBookCommand.ExecuteAsync(null);
        scenario.Detail.SetParameter(2);
        pending.SetResult(true); await deleting;
        books.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        Assert.Same(scenario.Detail, scenario.Main.Content);
    }

    [Fact]
    public async Task CommittedDeletionCannotPopReopenedDetailUsingSameSingleton()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = NewBooks(); books.Setup(x => x.GetDeleteImpactAsync(1)).ReturnsAsync("关联统计");
        books.Setup(x => x.DeleteAsync(1)).Returns(pending.Task);
        var alerts = NewAlerts(); var scenario = NewScenario(books.Object, alerts.Object);
        scenario.Detail.SetParameter(1); scenario.Main.PushContent(scenario.Detail);
        var deleting = scenario.Detail.DeleteBookCommand.ExecuteAsync(null);
        await scenario.Main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.Single(x => x.View == MenuNavigationConstant.BookListView));
        scenario.Detail.SetParameter(2); scenario.Main.PushContent(scenario.Detail);
        pending.SetResult(true); await deleting;
        books.Verify(x => x.DeleteAsync(1), Times.Once); books.Verify(x => x.DeleteAsync(2), Times.Never);
        Assert.Same(scenario.Detail, scenario.Main.Content); Assert.Equal(2, scenario.Main.ContentStack.Count);
    }

    [Fact]
    public async Task ReopenedDetailLoadsNewBookAfterOlderOperationCompletes()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = NewBooks(); books.Setup(x => x.GetDeleteImpactAsync(1)).Returns(pending.Task);
        books.Setup(x => x.GetProgressAsync(2)).ReturnsAsync(new BookProgress { Book = new Book { Id = 2, Title = "新打开的 B", TotalPages = 100 } });
        var alerts = NewAlerts(); var scenario = NewScenario(books.Object, alerts.Object);
        scenario.Detail.SetParameter(1); scenario.Main.PushContent(scenario.Detail);
        var deleting = scenario.Detail.DeleteBookCommand.ExecuteAsync(null);
        await scenario.Main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.Single(x => x.View == MenuNavigationConstant.BookListView));
        scenario.Detail.SetParameter(2); scenario.Main.PushContent(scenario.Detail);
        var loading = scenario.Detail.OnLoadedCommand.ExecuteAsync(null);
        pending.SetResult("旧上下文关联统计"); await Task.WhenAll(deleting, loading);
        Assert.Equal(2, scenario.Detail.Book?.Id); Assert.Equal("新打开的 B", scenario.Detail.Book?.Title);
        Assert.False(scenario.Detail.IsBusy); Assert.Empty(scenario.Detail.Error);
    }

    [Fact]
    public async Task ConfirmedDeletionOnCurrentDetailStillReturnsToLibrary()
    {
        var books = NewBooks(); books.Setup(x => x.GetDeleteImpactAsync(1)).ReturnsAsync("关联统计");
        var alerts = NewAlerts(); var scenario = NewScenario(books.Object, alerts.Object);
        scenario.Detail.SetParameter(1); scenario.Main.PushContent(scenario.Detail);
        await scenario.Detail.DeleteBookCommand.ExecuteAsync(null);
        books.Verify(x => x.DeleteAsync(1), Times.Once);
        Assert.Same(scenario.List, scenario.Main.Content); Assert.Single(scenario.Main.ContentStack);
    }

    private static Mock<IBookStorage> NewBooks()
    {
        var books = new Mock<IBookStorage>();
        books.Setup(x => x.DeleteAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        books.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new PageResult<BookProgress>());
        return books;
    }
    private static Mock<IAlertService> NewAlerts()
    {
        var alerts = new Mock<IAlertService>(); alerts.Setup(x => x.ConfirmAsync("删除书籍", It.IsAny<string>())).ReturnsAsync(true); return alerts;
    }
    private static (MainViewModel Main, BookListViewModel List, BookDetailViewModel Detail) NewScenario(IBookStorage books, IAlertService alerts)
    {
        var records = new Mock<IReadingRecordStorage>(); records.Setup(x => x.GetForBookAsync(It.IsAny<int>())).ReturnsAsync(Array.Empty<ReadingRecord>());
        var quotes = new Mock<IQuoteStorage>(); quotes.Setup(x => x.SearchAsync("", false, It.IsAny<int?>())).ReturnsAsync(Array.Empty<QuoteItem>());
        var plans = new Mock<IReadingPlanStorage>(); plans.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<PlanItem>());
        var menu = new Mock<IMenuNavigationService>(); var main = new MainViewModel(menu.Object);
        var list = new BookListViewModel(books, Mock.Of<IContentNavigationService>());
        menu.Setup(x => x.NavigateTo(MenuNavigationConstant.BookListView, null)).Callback(() => { list.SetParameter(null!); main.SetMenuAndContent(MenuNavigationConstant.BookListView, list); });
        var editor = new ReadingRecordEditViewModel(records.Object, alerts, main);
        var detail = new BookDetailViewModel(books, records.Object, quotes.Object, alerts, editor, Mock.Of<IContentNavigationService>(), main, plans.Object);
        main.SetMenuAndContent(MenuNavigationConstant.BookListView, list);
        return (main, list, detail);
    }
}
