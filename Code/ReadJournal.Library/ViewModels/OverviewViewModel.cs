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
    public IRelayCommand OpenLibraryCommand { get; }
    public IRelayCommand<BookProgress> OpenBookCommand { get; }
    public OverviewViewModel(IReadingRecordStorage records, IBookStorage books, IStatisticsService statistics, IReadingPlanStorage plans, IMenuNavigationService menu, IContentNavigationService content)
    {
        _records = records; _books = books; _statistics = statistics; _plans = plans;
        OpenLibraryCommand = new RelayCommand(() => menu.NavigateTo(MenuNavigationConstant.BookListView));
        OpenBookCommand = new RelayCommand<BookProgress>(b => { if (b != null) content.NavigateTo(ContentNavigationConstant.BookDetailView, b.Book.Id); });
    }
    protected override async Task LoadAsync()
    {
        Summary = (await _statistics.GetAsync()).Summary; ReadingBooks = (await _books.SearchAsync("", "在读", 1, 5)).Items;
        RecentRecords = await _records.GetRecentSummariesAsync(5);
        PendingPlans = (await _plans.GetAllAsync()).Where(x => x.State != "已完成").Take(5).ToArray();
    }
}
