using System.Text.Json;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Ai;

// The cross-platform fallback is deliberately session-only: no plaintext credential file.
public sealed class SessionSecretStore : ISecretStore
{
    private readonly object _sync = new(); private string? _key;
    public Task<bool> HasApiKeyAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); lock (_sync) return Task.FromResult(_key is not null); }
    public Task<string?> ReadApiKeyAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); lock (_sync) return Task.FromResult(_key); }
    public Task WriteApiKeyAsync(string apiKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length > 512 || apiKey.Any(c => c < 33 || c > 126)) throw new AiException("InvalidCredential", "密钥格式无效。");
        lock (_sync) _key = apiKey; return Task.CompletedTask;
    }
    public Task DeleteApiKeyAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); lock (_sync) _key = null; return Task.CompletedTask; }
}
public sealed class AiSettingsStorage(DataPaths paths) : IAiSettingsStorage
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public static AiSettings Default => new("https://api.deepseek.com/chat/completions", "deepseek-chat", 24000, 2048, TimeSpan.FromSeconds(45), false);
    public static void Validate(AiSettings settings)
    {
        if (!Uri.TryCreate(settings.HttpsEndpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "api.deepseek.com" || !uri.IsDefaultPort ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath is not ("/chat/completions" or "/v1/chat/completions") ||
            settings.Model is not ("deepseek-chat" or "deepseek-reasoner") || settings.MaxInputUtf16Units is < 1 or > AiIntegrity.MaxInput || settings.MaxOutputTokens is < 64 or > 8192 ||
            settings.Timeout < TimeSpan.FromSeconds(1) || settings.Timeout > TimeSpan.FromSeconds(120))
            throw new AiException("InvalidSettings", "AI 配置无效，请检查 HTTPS 地址、模型与限额。");
    }
    public async Task<AiSettings> GetAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = Path.Combine(paths.DirectoryPath, "ai-settings.json");
            if (!File.Exists(path)) return Default;
            try { var settings = JsonSerializer.Deserialize<AiSettings>(await File.ReadAllTextAsync(path, ct)) ?? throw new JsonException(); Validate(settings); return settings; }
            catch (JsonException) { throw new AiException("InvalidSettings", "AI 配置文件无效，请在设置页重新保存。"); }
        }
        finally { _gate.Release(); }
    }
    public async Task SaveAsync(AiSettings settings, CancellationToken ct)
    {
        Validate(settings); await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(paths.DirectoryPath); var path = Path.Combine(paths.DirectoryPath, "ai-settings.json");
            var temp = path + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(settings), ct); File.Move(temp, path, true);
        }
        finally { _gate.Release(); }
    }
}
