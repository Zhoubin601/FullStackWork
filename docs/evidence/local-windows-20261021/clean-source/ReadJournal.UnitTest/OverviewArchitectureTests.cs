using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using Xunit;

namespace ReadJournal.UnitTest;

public class OverviewArchitectureTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString());
    private readonly DatabaseService _database;
    private readonly BookStorage _books;
    private readonly ReadingRecordStorage _records;

    public OverviewArchitectureTests()
    {
        _database = new DatabaseService(new DataPaths(_directory));
        var rules = new ReadingService();
        _books = new BookStorage(_database, rules);
        _records = new ReadingRecordStorage(_database, rules);
    }

    [Fact]
    public async Task RecentSummariesJoinBooksSortDatesAndIdsAndApplyLimitInActualSqlite()
    {
        var today = DateTime.Today;
        var first = new Book { Title = "甲书", TotalPages = 100 };
        var second = new Book { Title = "乙书", TotalPages = 200 };
        await _books.SaveAsync(first);
        await _books.SaveAsync(second);
        await _records.SaveAsync(new ReadingRecord { BookId = first.Id, ReadingDate = today, StartPage = 1, EndPage = 3, Minutes = 2 });
        await _records.SaveAsync(new ReadingRecord { BookId = first.Id, ReadingDate = today.AddDays(-1), StartPage = 21, EndPage = 29, Minutes = 9 });
        await _records.SaveAsync(new ReadingRecord { BookId = second.Id, ReadingDate = today, StartPage = 10, EndPage = 20, Minutes = 7 });

        var expected = new[]
        {
            $"{today:MM-dd} · 《乙书》 · 11 页 / 7 分钟",
            $"{today:MM-dd} · 《甲书》 · 3 页 / 2 分钟",
            $"{today.AddDays(-1):MM-dd} · 《甲书》 · 9 页 / 9 分钟"
        };
        Assert.Equal(expected.Take(2), await _records.GetRecentSummariesAsync(2));
        Assert.Equal(expected, await _records.GetRecentSummariesAsync(5));
    }

    [Fact]
    public async Task RecentSummariesReturnEmptyForActualEmptySqlite()
    {
        Assert.Empty(await _records.GetRecentSummariesAsync(5));
    }

    [Fact]
    public async Task OverviewLoadsThroughBusinessInterfacesWithoutDatabaseDependency()
    {
        var records = new Mock<IReadingRecordStorage>(MockBehavior.Strict);
        var books = new Mock<IBookStorage>(MockBehavior.Strict);
        var statistics = new Mock<IStatisticsService>(MockBehavior.Strict);
        var plans = new Mock<IReadingPlanStorage>(MockBehavior.Strict);
        var menu = new Mock<IMenuNavigationService>(MockBehavior.Strict);
        var content = new Mock<IContentNavigationService>(MockBehavior.Strict);
        var summaries = new[] { "服务提供的最近记录" };
        var readingBook = new BookProgress { Book = new Book { Id = 7, Title = "在读书籍", TotalPages = 100 }, CurrentPage = 10 };
        var stats = new ReadingStatistics { TotalPages = 11, TotalMinutes = 7, CompletedBooks = 1 };
        var pending = new PlanItem { Progress = readingBook, Plan = new ReadingPlan { BookId = 7, TargetDate = DateTime.Today.AddDays(1) } };
        records.Setup(x => x.GetRecentSummariesAsync(5)).ReturnsAsync(summaries);
        books.Setup(x => x.SearchAsync("", "在读", 1, 5)).ReturnsAsync(new PageResult<BookProgress> { Items = [readingBook], TotalCount = 1 });
        statistics.Setup(x => x.GetAsync()).ReturnsAsync(stats);
        plans.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { pending });
        var vm = new OverviewViewModel(records.Object, books.Object, statistics.Object, plans.Object, menu.Object, content.Object);

        await vm.OnLoadedCommand.ExecuteAsync(null);

        Assert.Equal("", vm.Error);
        Assert.Equal(stats.Summary, vm.Summary);
        Assert.Same(summaries, vm.RecentRecords);
        Assert.Same(readingBook, Assert.Single(vm.ReadingBooks));
        Assert.Same(pending, Assert.Single(vm.PendingPlans));
        Assert.DoesNotContain(typeof(OverviewViewModel).GetConstructors().SelectMany(x => x.GetParameters()), x => x.ParameterType == typeof(IDatabaseService));
        records.Verify(x => x.GetRecentSummariesAsync(5), Times.Once);
        books.Verify(x => x.SearchAsync("", "在读", 1, 5), Times.Once);
        statistics.Verify(x => x.GetAsync(), Times.Once);
        plans.Verify(x => x.GetAllAsync(), Times.Once);
        records.VerifyNoOtherCalls();
        books.VerifyNoOtherCalls();
        statistics.VerifyNoOtherCalls();
        plans.VerifyNoOtherCalls();
        menu.VerifyNoOtherCalls();
        content.VerifyNoOtherCalls();
    }

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
