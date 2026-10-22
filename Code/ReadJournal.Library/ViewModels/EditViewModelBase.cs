using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public abstract class EditViewModelBase : ViewModelBase
{
    private readonly IAlertService _alert;
    protected readonly MainViewModel Main;
    private string? _original;
    private bool _confirmingLeave;
    protected bool HasLoaded { get; private set; }
    public bool IsDirty => _original is not null && _original != DraftSnapshot();
    public IAsyncRelayCommand SaveCommand { get; }
    protected EditViewModelBase(IAlertService alert, MainViewModel main)
    {
        _alert = alert; Main = main;
        SaveCommand = new AsyncRelayCommand(async () =>
        {
            var version = ParameterVersion; var saved = false;
            await RunAsync(async () => saved = await SaveDraftAsync());
            if (saved && version == ParameterVersion) await AfterSavedAsync();
        });
    }
    protected abstract string DraftSnapshot();
    protected abstract Task SaveCoreAsync();
    protected virtual Task AfterSavedAsync() => Main.RequestBackAsync(this);
    public override void SetParameter(object parameter) { base.SetParameter(parameter); HasLoaded = false; }
    protected void MarkLoaded() => HasLoaded = true;
    protected void AcceptChanges() => AcceptChanges(DraftSnapshot());
    protected void AcceptChanges(string savedSnapshot) { _original = savedSnapshot; OnPropertyChanged(nameof(IsDirty)); }
    private async Task<bool> SaveDraftAsync()
    {
        if (!HasLoaded) throw new InvalidOperationException("数据尚未加载成功，请先重新加载后再保存。");
        var version = ParameterVersion;
        var submitted = DraftSnapshot();
        await SaveCoreAsync();
        if (version != ParameterVersion) return false;
        // Only the submitted values reached storage. Inputs changed during the await remain a draft.
        AcceptChanges(submitted);
        if (!IsDirty) return true;
        Error = "本次提交已保存；保存期间的新修改尚未保存，请继续保存。";
        return false;
    }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.PropertyName != nameof(IsDirty)) base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsDirty))); }
    public override async Task<bool> CanLeaveAsync()
    {
        if (IsBusy || _confirmingLeave) return false;
        if (!IsDirty) return true;
        _confirmingLeave = true;
        try
        {
            var choice = await _alert.ConfirmLeaveAsync();
            if (choice == LeaveDecision.Cancel) return false;
            if (choice == LeaveDecision.Discard) return true;
            var saved = false;
            await RunAsync(async () => saved = await SaveDraftAsync());
            return saved;
        }
        finally { _confirmingLeave = false; }
    }
}
