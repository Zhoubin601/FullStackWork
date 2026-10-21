using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using Xunit;

namespace ReadJournal.UnitTest;

public class BusyLoadRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturningToBusyListWaitsAndRefreshesLatestQueryEvenAfterFailure(bool oldOperationFails)
    {
        var pending = new TaskCompletionSource<PageResult<BookProgress>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = new Mock<IBookStorage>();
        books.Setup(x => x.SearchAsync("旧查询", "全部", 1, 10)).Returns(pending.Task);
        books.Setup(x => x.SearchAsync("最新查询", "全部", 1, 10)).ReturnsAsync(new PageResult<BookProgress>
        {
            Items = [new BookProgress { Book = new Book { Id = 2, Title = "最新书籍", TotalPages = 100 } }],
            TotalCount = 1
        });
        var vm = new BookListViewModel(books.Object, Mock.Of<IContentNavigationService>()) { Query = "旧查询" };
        var searching = vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);

        // Menu navigation can reopen this cached singleton while its previous search is pending.
        vm.SetParameter(null!);
        vm.Query = "中间查询";
        var refreshing = vm.OnLoadedCommand.ExecuteAsync(null);
        var waitedForOperation = !refreshing.IsCompleted;
        vm.SetParameter(null!);
        vm.Query = "最新查询";
        if (oldOperationFails) pending.SetException(new IOException("旧查询读取失败"));
        else pending.SetResult(new PageResult<BookProgress> { Items = [], TotalCount = 0 });
        await Task.WhenAll(searching, refreshing).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(waitedForOperation);
        Assert.Equal(2, Assert.Single(vm.BooksPage).Book.Id);
        Assert.False(vm.IsBusy);
        Assert.Equal("", vm.Error);
        Assert.Equal("第 1/1 页 · 共 1 本", vm.PageText);
        books.Verify(x => x.SearchAsync("中间查询", It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        books.Verify(x => x.SearchAsync("最新查询", "全部", 1, 10), Times.Once);
    }
}
