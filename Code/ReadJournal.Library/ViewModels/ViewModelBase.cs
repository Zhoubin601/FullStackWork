using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace ReadJournal.Library.ViewModels;
public abstract class ViewModelBase : ObservableObject
{
    private string _error = ""; private bool _isBusy;
    private Task _operationFinished = Task.CompletedTask;
    protected int ParameterVersion { get; private set; }
    public string Error { get => _error; protected set => SetProperty(ref _error, value); }
    public bool IsBusy { get => _isBusy; protected set => SetProperty(ref _isBusy, value); }
    public IAsyncRelayCommand OnLoadedCommand { get; }
    protected virtual bool UseGlobalBusyForLoad => true;
    protected ViewModelBase()
    {
        OnLoadedCommand = new AsyncRelayCommand(async () =>
        {
            int version;
            do
            {
                // A singleton can be reopened while an earlier operation is still finishing.
                // Keep its Loaded request so the new parameter is actually displayed.
                while (IsBusy) await _operationFinished;
                version = ParameterVersion;
                if (UseGlobalBusyForLoad) await RunAsync(LoadAsync);
                else { try { await LoadAsync(); } catch (Exception ex) { if (version == ParameterVersion) Error = ex.Message; } }
            } while (version != ParameterVersion);
        });
    }
    public virtual void SetParameter(object parameter) { ParameterVersion++; Error = ""; }
    public virtual void InvalidateDataSet() => SetParameter(null!);
    protected virtual Task LoadAsync() => Task.CompletedTask;
    public virtual Task<bool> CanLeaveAsync() => Task.FromResult(!IsBusy);
    protected async Task NavigateAsync(Func<Task<bool>> navigation)
    {
        var version = ParameterVersion;
        try { await navigation(); } catch (Exception ex) { if (version == ParameterVersion) Error = ex.Message; }
    }
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _operationFinished = completion.Task;
        var version = ParameterVersion; IsBusy = true; Error = "";
        try { await action(); }
        catch (Exception ex) { if (version == ParameterVersion) Error = ex.Message; }
        finally { IsBusy = false; completion.TrySetResult(); }
    }
}
