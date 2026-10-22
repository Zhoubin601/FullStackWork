using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
namespace ReadJournal.Services;
public class MenuNavigationService : IMenuNavigationService
{
    public void NavigateTo(string view, object? parameter = null)
    {
        ViewModelBase target = view switch
        {
            MenuNavigationConstant.OverviewView => ServiceLocator.Current.OverviewViewModel,
            MenuNavigationConstant.BookListView => ServiceLocator.Current.BookListViewModel,
            MenuNavigationConstant.QuoteListView => ServiceLocator.Current.QuoteListViewModel,
            MenuNavigationConstant.ReadingPlanListView => ServiceLocator.Current.ReadingPlanListViewModel,
            MenuNavigationConstant.StatisticsView => ServiceLocator.Current.StatisticsViewModel,
            MenuNavigationConstant.SettingsView => ServiceLocator.Current.SettingsViewModel,
            _ => throw new ArgumentException("未知或尚未实现的菜单目标。", nameof(view))
        };
        var main = ServiceLocator.Current.MainViewModel;
        if (main.Content is not null && !main.IsNavigationCommit) throw new InvalidOperationException("运行期菜单导航必须等待NavigateToAsync。");
        target.SetParameter(parameter!);
        main.SetMenuAndContent(view, target);
    }
    public async Task<bool> NavigateToAsync(string view, object? parameter = null, CancellationToken ct = default)
    {
        if (!MenuItem.MenuItems.Any(m => m.View == view)) throw new ArgumentException("菜单目标无效。");
        return await ServiceLocator.Current.MainViewModel.RequestNavigationAsync(() => NavigateTo(view, parameter), ct);
    }
}
