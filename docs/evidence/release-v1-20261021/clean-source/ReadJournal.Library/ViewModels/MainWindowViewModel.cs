using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public class MainWindowViewModel : ViewModelBase
{
    private readonly IRootNavigationService _root; private readonly IMenuNavigationService _menu; private readonly IDatabaseService _database;
    private ViewModelBase? _content;
    public ViewModelBase? Content { get => _content; set => SetProperty(ref _content, value); }
    public IRelayCommand OnInitializedCommand { get; }
    public MainWindowViewModel(IDatabaseService database, IRootNavigationService root, IMenuNavigationService menu)
    {
        _database = database; _root = root; _menu = menu;
        OnInitializedCommand = new RelayCommand(() =>
        {
            _root.NavigateTo(_database.IsInitialized ? RootNavigationConstant.MainView : RootNavigationConstant.InitializationView);
            if (_database.IsInitialized) _menu.NavigateTo(MenuNavigationConstant.OverviewView);
        });
    }
}
