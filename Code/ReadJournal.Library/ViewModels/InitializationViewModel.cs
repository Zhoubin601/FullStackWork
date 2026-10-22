using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;
public class InitializationViewModel : ViewModelBase
{
    private readonly IDatabaseService _database; private readonly IRootNavigationService _root; private readonly IMenuNavigationService _menu;
    public InitializationViewModel(IDatabaseService database, IRootNavigationService root, IMenuNavigationService menu) { _database = database; _root = root; _menu = menu; }
    private readonly IOperationRecoveryService? _recovery;
    public InitializationViewModel(IDatabaseService database, IRootNavigationService root, IMenuNavigationService menu, IOperationRecoveryService recovery) : this(database, root, menu) { _recovery = recovery; }
    protected override async Task LoadAsync()
    {
        if (_recovery is null) await _database.InitializeAsync();
        else await _recovery.RecoverBeforeBusinessReadyAsync(CancellationToken.None);
        _root.NavigateTo(RootNavigationConstant.MainView);
        _menu.NavigateTo(MenuNavigationConstant.OverviewView);
    }
}
