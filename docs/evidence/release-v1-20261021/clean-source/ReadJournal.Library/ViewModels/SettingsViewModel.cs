using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IPreferenceStorage _preferences; private readonly IBackupService _backup; private readonly IFileService _files;
    [ObservableProperty] private string _selectedTheme = "浅色";
    [ObservableProperty] private string _status = "";
    public string[] Themes { get; } = ["浅色", "深色"];
    public IAsyncRelayCommand ApplyThemeCommand { get; }
    public IAsyncRelayCommand LoadDemoCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }
    public SettingsViewModel(IPreferenceStorage preferences, IBackupService backup, IFileService files, IAlertService alert)
    {
        _preferences = preferences; _backup = backup; _files = files;
        ApplyThemeCommand = new AsyncRelayCommand(() => RunAsync(async () => { await preferences.SetThemeAsync(SelectedTheme); Status = "主题已保存。"; }));
        LoadDemoCommand = new AsyncRelayCommand(() => RunAsync(async () => { if (await alert.ConfirmAsync("载入演示数据", "将载入明确标注的虚构书籍、记录、书摘与计划。仅允许在空业务库载入。")) { await backup.LoadDemoAsync(); Status = "演示数据已载入，可从概览和各业务页面查看。"; } }));
        ExportCommand = new AsyncRelayCommand(() => RunAsync(async () => { var path = await files.ChooseSavePathAsync(); if (path != null) { await backup.ExportAsync(path); Status = "备份已保存：" + path; } }));
        RestoreCommand = new AsyncRelayCommand(() => RunAsync(async () => { var path = await files.ChooseOpenPathAsync(); if (path != null && await alert.ConfirmAsync("恢复备份", "将替换全部业务数据。校验通过后会先自动备份当前数据；主题偏好会保留。是否继续？")) { var auto = await backup.RestoreAsync(path); Status = "恢复成功。原数据已自动备份至：" + auto; } }));
    }
    protected override async Task LoadAsync() { SelectedTheme = await _preferences.GetThemeAsync(); }
    public override Task<bool> CanLeaveAsync() => Task.FromResult(!IsBusy);
}
