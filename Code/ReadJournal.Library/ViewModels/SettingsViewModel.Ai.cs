using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Ai;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;

public partial class SettingsViewModel
{
    private IAiSettingsStorage? _aiSettings; private ISecretStore? _secrets;
    [ObservableProperty] private bool _useMock = true;
    [ObservableProperty] private bool _uploadAcknowledged;
    [ObservableProperty] private string _apiKeyInput = "";
    [ObservableProperty] private string _aiEndpoint = AiSettingsStorage.Default.HttpsEndpoint;
    [ObservableProperty] private string _aiModel = AiSettingsStorage.Default.Model;
    [ObservableProperty] private string _aiInputLimit = "24000";
    [ObservableProperty] private string _aiOutputLimit = "2048";
    [ObservableProperty] private string _aiTimeoutSeconds = "45";
    [ObservableProperty] private string _credentialStatus = "未配置会话密钥";
    public IAsyncRelayCommand SaveAiSettingsCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveApiKeyCommand { get; private set; } = null!;
    public IAsyncRelayCommand DeleteApiKeyCommand { get; private set; } = null!;
    private void InitializeAiSettings(IAiSettingsStorage? settings, ISecretStore? secrets)
    {
        _aiSettings = settings; _secrets = secrets;
        SaveAiSettingsCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            if (_aiSettings is null) return;
            if (!int.TryParse(AiInputLimit, out var input) || !int.TryParse(AiOutputLimit, out var output) || !int.TryParse(AiTimeoutSeconds, out var timeout)) throw new AiException("InvalidSettings", "限额与超时请输入整数。");
            await _aiSettings.SaveAsync(new(AiEndpoint, AiModel, input, output, TimeSpan.FromSeconds(timeout), UploadAcknowledged, UseMock), default);
            Status = UseMock ? "AI配置已保存：本地模拟，不上传正文。" : "AI配置已保存：真实模式只发送所选范围；P9真实联调尚未完成。";
        }));
        SaveApiKeyCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            try { if (_secrets is null) return; await _secrets.WriteApiKeyAsync(ApiKeyInput, default); CredentialStatus = "已配置会话密钥，退出应用后自动清除"; }
            finally { ApiKeyInput = ""; }
        }));
        DeleteApiKeyCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        { ApiKeyInput = ""; if (_secrets is not null) await _secrets.DeleteApiKeyAsync(default); CredentialStatus = "会话密钥已删除"; }));
    }
    private async Task LoadAiSettingsAsync()
    {
        ApiKeyInput = ""; if (_aiSettings is null) return;
        var settings = await _aiSettings.GetAsync(default);
        UseMock = settings.UseMock; UploadAcknowledged = settings.HasAcknowledgedTextUpload; AiEndpoint = settings.HttpsEndpoint; AiModel = settings.Model;
        AiInputLimit = settings.MaxInputUtf16Units.ToString(); AiOutputLimit = settings.MaxOutputTokens.ToString(); AiTimeoutSeconds = ((int)settings.Timeout.TotalSeconds).ToString();
        CredentialStatus = _secrets is not null && await _secrets.HasApiKeyAsync(default) ? "已配置会话密钥，退出应用后自动清除" : "未配置会话密钥（本地模拟不需要密钥）";
    }
}
