using Avalonia.Controls;
namespace ReadJournal.Views;
public partial class MainWindow : Window
{
    private bool _closing;
    public MainWindow() { InitializeComponent(); Closing += async (_, e) => { if (_closing) return; e.Cancel = true; if (await ServiceLocator.Current.MainViewModel.CanCloseAsync()) { _closing = true; Close(); } }; }
}
