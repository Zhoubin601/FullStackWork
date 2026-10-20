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
        target.SetParameter(parameter!);
        ServiceLocator.Current.MainViewModel.SetMenuAndContent(view, target);
    }
}
