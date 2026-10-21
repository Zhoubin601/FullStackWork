using ReadJournal.Library.Services;
namespace ReadJournal.Services;
public class RootNavigationService : IRootNavigationService
{
    public void NavigateTo(string view)
    {
        ServiceLocator.Current.MainWindowViewModel.Content = view switch
        {
            RootNavigationConstant.InitializationView => ServiceLocator.Current.InitializationViewModel,
            RootNavigationConstant.MainView => ServiceLocator.Current.MainViewModel,
            _ => throw new ArgumentException("未知的根导航目标。", nameof(view))
        };
    }
}
