using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using Xunit;
namespace ReadJournal.UnitTest;
public class NavigationRaceTests
{
    [Fact]
    public async Task OldSingletonLoadCannotPublishAfterNewParameter()
    {
        var books = new Mock<IBookStorage>(); var records = new Mock<IReadingRecordStorage>(); var quotes = new Mock<IQuoteStorage>(); var plans = new Mock<IReadingPlanStorage>();
        var pending = new TaskCompletionSource<BookProgress?>(TaskCreationOptions.RunContinuationsAsynchronously);
        books.Setup(x => x.GetProgressAsync(1)).Returns(pending.Task);
        books.Setup(x => x.GetProgressAsync(2)).ReturnsAsync(new BookProgress { Book = new() { Id = 2, Title = "B", TotalPages = 20 } });
        records.Setup(x => x.GetForBookAsync(It.IsAny<int>())).ReturnsAsync(Array.Empty<ReadingRecord>());
        quotes.Setup(x => x.SearchAsync("", false, It.IsAny<int?>())).ReturnsAsync(Array.Empty<QuoteItem>());
        plans.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<PlanItem>());
        var main = new MainViewModel(Mock.Of<IMenuNavigationService>()); var alert = Mock.Of<IAlertService>();
        var editor = new ReadingRecordEditViewModel(records.Object, alert, main);
        var vm = new BookDetailViewModel(books.Object, records.Object, quotes.Object, alert, editor, Mock.Of<IContentNavigationService>(), main, plans.Object);
        var published = new List<int>(); vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Progress) && vm.Book != null) published.Add(vm.Book.Id); };
        vm.SetParameter(1); var loading = vm.OnLoadedCommand.ExecuteAsync(null); Assert.True(vm.IsBusy);
        vm.SetParameter(2); Assert.Null(vm.Book);
        pending.SetResult(new BookProgress { Book = new() { Id = 1, Title = "A", TotalPages = 10 } });
        await loading;
        Assert.Equal(2, vm.Book!.Id); Assert.DoesNotContain(1, published); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task BackAndMenuCannotOpenTwoLeaveConfirmations()
    {
        var confirmation = new TaskCompletionSource<LeaveDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var alerts = new Mock<IAlertService>(); alerts.Setup(x => x.ConfirmLeaveAsync()).Returns(confirmation.Task);
        var menu = new Mock<IMenuNavigationService>(); var main = new MainViewModel(menu.Object);
        var editor = new BookEditViewModel(Mock.Of<IBookStorage>(), alerts.Object, main); editor.SetParameter(null!); editor.Title = "草稿";
        main.SetMenuAndContent(MenuNavigationConstant.BookListView, new BookListViewModel(Mock.Of<IBookStorage>(), Mock.Of<IContentNavigationService>()));
        main.PushContent(editor);
        var back = main.GoBackCommand.ExecuteAsync(null);
        await main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.First()); Assert.False(await main.CanCloseAsync());
        confirmation.SetResult(LeaveDecision.Cancel); await back;
        alerts.Verify(x => x.ConfirmLeaveAsync(), Times.Once); menu.Verify(x => x.NavigateTo(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
}
