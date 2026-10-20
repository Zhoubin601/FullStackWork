using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace ReadJournal.Library.ViewModels;
public abstract class ViewModelBase : ObservableObject
{
    private string _error = ""; private bool _isBusy;
    protected int ParameterVersion { get; private set; }
    public string Error { get => _error; protected set => SetProperty(ref _error, value); }
    public bool IsBusy { get => _isBusy; protected set => SetProperty(ref _isBusy, value); }
    public IAsyncRelayCommand OnLoadedCommand { get; }
    protected ViewModelBase()
    {
        OnLoadedCommand = new AsyncRelayCommand(async () =>
        {
            int version;
            do { version = ParameterVersion; await RunAsync(LoadAsync); } while (version != ParameterVersion);
        });
    }
    public virtual void SetParameter(object parameter) { ParameterVersion++; Error = ""; }
    protected virtual Task LoadAsync() => Task.CompletedTask;
    public virtual Task<bool> CanLeaveAsync() => Task.FromResult(true);
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        var version = ParameterVersion; IsBusy = true; Error = "";
        try { await action(); } catch (Exception ex) { if (version == ParameterVersion) Error = ex.Message; } finally { IsBusy = false; }
    }
}
