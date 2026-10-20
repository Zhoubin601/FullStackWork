using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public class InitializationViewModel : ViewModelBase
{
    private readonly IDatabaseService _database; private readonly IRootNavigationService _root; private readonly IMenuNavigationService _menu;
    public InitializationViewModel(IDatabaseService database, IRootNavigationService root, IMenuNavigationService menu) { _database = database; _root = root; _menu = menu; }
    protected override async Task LoadAsync()
    {
        await _database.InitializeAsync();
        _root.NavigateTo(RootNavigationConstant.MainView);
        _menu.NavigateTo(MenuNavigationConstant.OverviewView);
    }
}
