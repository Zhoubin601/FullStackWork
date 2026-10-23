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
    public SettingsViewModel(IPreferenceStorage preferences, IBackupService backup, IFileService files, IAlertService alert, ReadJournal.Library.Services.Reader.IAiSettingsStorage? aiSettings = null, ReadJournal.Library.Services.Reader.ISecretStore? secrets = null)
    {
        _preferences = preferences; _backup = backup; _files = files;
        InitializeAiSettings(aiSettings, secrets);
        ApplyThemeCommand = new AsyncRelayCommand(() => RunAsync(async () => { await preferences.SetThemeAsync(SelectedTheme); Status = "主题已保存。"; }));
        LoadDemoCommand = new AsyncRelayCommand(() => RunAsync(async () => { if (await alert.ConfirmAsync("载入演示数据", "将载入明确标注的虚构书籍、记录、书摘与计划。仅允许在空业务库载入。")) { await backup.LoadDemoAsync(); Status = "演示数据已载入，可从概览和各业务页面查看。"; } }));
        ExportCommand = new AsyncRelayCommand(() => RunAsync(async () => { var path = await files.ChooseSavePathAsync(); if (path != null) { await backup.ExportAsync(path); Status = "备份已保存：" + path; } }));
        RestoreCommand = new AsyncRelayCommand(() => RunAsync(async () => { var path = await files.ChooseOpenPathAsync(); if (path != null && await alert.ConfirmAsync("恢复备份", "将整体替换全部业务数据，包括当前电子书、原书文件、阅读会话、书摘和计划。旧 JSON 只恢复手动书籍，也会移除当前电子书。校验通过后会先生成完整自动备份；本机主题和密钥保留。是否继续？")) { var auto = await backup.RestoreAsync(path); Status = "恢复成功。原数据已自动备份至：" + auto + (backup.LastWarning is string warning ? "\n" + warning : ""); } }));
    }
    protected override async Task LoadAsync() { SelectedTheme = await _preferences.GetThemeAsync(); await LoadAiSettingsAsync(); }
    public override Task<bool> CanLeaveAsync() { if (IsBusy) return Task.FromResult(false); ApiKeyInput = ""; return Task.FromResult(true); }
}
