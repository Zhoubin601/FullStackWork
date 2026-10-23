namespace ReadJournal.Library.Services;
public interface IMenuNavigationService
{
    void NavigateTo(string view, object? parameter = null);
    Task<bool> NavigateToAsync(string view, object? parameter = null, CancellationToken ct = default);
}
public static class MenuNavigationConstant
{
    public const string OverviewView = nameof(OverviewView);
    public const string BookListView = nameof(BookListView);
    public const string QuoteListView = nameof(QuoteListView);
    public const string ReadingPlanListView = nameof(ReadingPlanListView);
    public const string StatisticsView = nameof(StatisticsView);
    public const string SettingsView = nameof(SettingsView);
}
