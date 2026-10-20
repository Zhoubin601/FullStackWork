using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public abstract class EditViewModelBase : ViewModelBase
{
    private readonly IAlertService _alert;
    protected readonly MainViewModel Main;
    private string? _original;
    public bool IsDirty => _original is not null && _original != DraftSnapshot();
    public IAsyncRelayCommand SaveCommand { get; }
    protected EditViewModelBase(IAlertService alert, MainViewModel main)
    {
        _alert = alert; Main = main;
        SaveCommand = new AsyncRelayCommand(() => RunAsync(async () => { await SaveCoreAsync(); AcceptChanges(); AfterSaved(); }));
    }
    protected abstract string DraftSnapshot();
    protected abstract Task SaveCoreAsync();
    protected virtual void AfterSaved() => Main.GoBack();
    protected void AcceptChanges() { _original = DraftSnapshot(); OnPropertyChanged(nameof(IsDirty)); }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.PropertyName != nameof(IsDirty)) base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsDirty))); }
    public override async Task<bool> CanLeaveAsync()
    {
        if (IsBusy) return false;
        if (!IsDirty) return true;
        var choice = await _alert.ConfirmLeaveAsync();
        if (choice == LeaveDecision.Cancel) return false;
        if (choice == LeaveDecision.Discard) return true;
        var saved = false; await RunAsync(async () => { await SaveCoreAsync(); AcceptChanges(); saved = true; }); return saved;
    }
}
