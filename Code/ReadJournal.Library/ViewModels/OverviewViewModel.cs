using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public partial class OverviewViewModel : ViewModelBase
{
    private readonly IReadingRecordStorage _records; private readonly IBookStorage _books; private readonly IStatisticsService _statistics; private readonly IReadingPlanStorage _plans;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private IReadOnlyList<BookProgress> _readingBooks = [];
    [ObservableProperty] private IReadOnlyList<string> _recentRecords = [];
    [ObservableProperty] private IReadOnlyList<PlanItem> _pendingPlans = [];
    public IAsyncRelayCommand OpenLibraryCommand { get; }
    public IAsyncRelayCommand<BookProgress> OpenBookCommand { get; }
    public OverviewViewModel(IReadingRecordStorage records, IBookStorage books, IStatisticsService statistics, IReadingPlanStorage plans, IMenuNavigationService menu, IContentNavigationService content)
    {
        _records = records; _books = books; _statistics = statistics; _plans = plans;
        OpenLibraryCommand = new AsyncRelayCommand(() => NavigateAsync(() => menu.NavigateToAsync(MenuNavigationConstant.BookListView)));
        OpenBookCommand = new AsyncRelayCommand<BookProgress>(b => b is null ? Task.CompletedTask : NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.BookDetailView, b.Book.Id)));
    }
    protected override async Task LoadAsync()
    {
        var version = ParameterVersion;
        var summary = (await _statistics.GetAsync()).Summary; var books = (await _books.SearchAsync("", "在读", 1, 5)).Items;
        var records = await _records.GetRecentSummariesAsync(5);
        var plans = (await _plans.GetAllAsync()).Where(x => x.State != "已完成").Take(5).ToArray();
        if (version != ParameterVersion) return;
        Summary = summary; ReadingBooks = books; RecentRecords = records; PendingPlans = plans;
    }
    public override void InvalidateDataSet() { base.InvalidateDataSet(); Summary = ""; ReadingBooks = []; RecentRecords = []; PendingPlans = []; }
}
