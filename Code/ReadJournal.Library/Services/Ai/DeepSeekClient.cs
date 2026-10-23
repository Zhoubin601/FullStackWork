using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Ai;

// The local mock exercises exactly the same HTTP serialization/response parser as the real adapter.
public sealed class AiMockHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        var messages = body.RootElement.GetProperty("messages");
        using var scope = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
        var operation = scope.RootElement.GetProperty("operation").GetString();
        var text = scope.RootElement.GetProperty("segments").EnumerateArray().Select(x => x.GetString()!).First(x => !string.IsNullOrWhiteSpace(x));
        var boundaries = StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();
        var length = boundaries.Where(x => x <= 64).Last(); if (length == 0) length = boundaries[1];
        var content = operation == nameof(AiOperation.ExtractQuotes)
            ? JsonSerializer.Serialize(new { quotes = new[] { new { originalText = text[..length], explanation = "本地模拟感想：请结合原文自行判断。", prefixHint = (string?)null, suffixHint = text[length..Math.Min(text.Length, length + 32)] } } })
            : JsonSerializer.Serialize(new { analysis = $"本地模拟结果：已冻结{scope.RootElement.GetProperty("segments").GetArrayLength()}段正文，共{scope.RootElement.GetProperty("segments").EnumerateArray().Sum(x => x.GetString()!.Length)}字符。本结果仅用于演示流程，不代表真实模型分析。" });
        ct.ThrowIfCancellationRequested();
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content }, finish_reason = "stop" } }, usage = new { prompt_tokens = (int?)null, completion_tokens = (int?)null } }), Encoding.UTF8, "application/json") };
    }
}

public sealed class DeepSeekClient : IDeepSeekClient, IDisposable
{
    private readonly IAiSettingsStorage _settings; private readonly ISecretStore _secrets;
    private readonly HttpClient _http, _mock;
    public DeepSeekClient(IAiSettingsStorage settings, ISecretStore secrets) : this(settings, secrets, new HttpClientHandler { AllowAutoRedirect = false }) { }
    public DeepSeekClient(IAiSettingsStorage settings, ISecretStore secrets, HttpMessageHandler handler)
    { _settings = settings; _secrets = secrets; _http = new(handler) { Timeout = Timeout.InfiniteTimeSpan }; _mock = new(new AiMockHandler()) { Timeout = Timeout.InfiniteTimeSpan }; }
    public async Task<AiProviderResponse> SendAsync(AiProviderRequest request, CancellationToken ct)
    {
        var settings = request.Settings ?? await _settings.GetAsync(ct); AiSettingsStorage.Validate(settings);
        if (request.UserText.Length > AiIntegrity.MaxInput * 6 + 65536 || request.MaxOutputTokens < 1 || request.MaxOutputTokens > settings.MaxOutputTokens)
            throw new AiException("LimitExceeded", "请求超过限额，请缩小范围。");
        if (!settings.UseMock && !settings.HasAcknowledgedTextUpload) throw new AiException("UploadNotAcknowledged", "请先在设置中确认正文上传范围。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(settings.Timeout);
        using var message = new HttpRequestMessage(HttpMethod.Post, settings.HttpsEndpoint);
        if (!settings.UseMock)
        {
            var key = await _secrets.ReadApiKeyAsync(ct);
            if (string.IsNullOrEmpty(key)) throw new AiException("CredentialMissing", "未配置会话密钥，请前往设置。");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
        message.Content = new StringContent(JsonSerializer.Serialize(new { model = request.Model, messages = new[] { new { role = "system", content = request.SystemPrompt }, new { role = "user", content = request.UserText } }, stream = false, max_tokens = request.MaxOutputTokens, response_format = new { type = "json_object" } }), Encoding.UTF8, "application/json");
        try
        {
            using var response = await (settings.UseMock ? _mock : _http).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AiException("ProviderUnauthorized", "密钥无效或无权限（401/403），请检查设置。"),
                HttpStatusCode.TooManyRequests => new AiException("RateLimited", "请求过于频繁（429），请稍后手动重试。"),
                >= HttpStatusCode.InternalServerError => new AiException("ProviderUnavailable", "服务暂不可用（5xx），请稍后手动重试。"),
                _ => new AiException("ProviderRejected", "服务拒绝请求，请检查模型与配置。")
            };
            if (response.Content.Headers.ContentLength > AiIntegrity.MaxResponseBytes) throw Invalid();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream(); var chunk = new byte[4096]; int count;
            while ((count = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            { if (buffer.Length + count > AiIntegrity.MaxResponseBytes) throw Invalid(); buffer.Write(chunk, 0, count); }
            ct.ThrowIfCancellationRequested(); return Parse(buffer.ToArray(), request.Operation);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AiException("Timeout", "请求超时，未自动重发；请确认后再手动生成。"); }
        catch (HttpRequestException) { throw new AiException("Offline", "连接不可用或已断开，请检查网络后手动重试。"); }
        catch (IOException) { throw new AiException("Interrupted", "响应中断，未保存结果，请稍后手动重试。"); }
        catch (JsonException) { throw Invalid(); }
        catch (InvalidOperationException) { throw Invalid(); }
        catch (FormatException) { throw Invalid(); }
    }
    private static AiException Invalid() => new("InvalidAiResponse", "返回内容无效、被截断或超过限额，未保存书摘。");
    private static AiProviderResponse Parse(byte[] bytes, AiOperation operation)
    {
        using var envelope = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        var root = envelope.RootElement;
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() != 1 ||
            !choices[0].TryGetProperty("finish_reason", out var finish) || finish.GetString() != "stop" ||
            !choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String) throw Invalid();
        using var payload = JsonDocument.Parse(content.GetString()!, new JsonDocumentOptions { MaxDepth = 8 }); var value = payload.RootElement;
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Select(x => x.Name).Distinct().Count() != value.EnumerateObject().Count()) throw Invalid();
        var candidates = new List<AiQuoteCandidate>(); string? analysis = null;
        if (operation == AiOperation.ExtractQuotes)
        {
            if (!value.TryGetProperty("quotes", out var quotes) || quotes.ValueKind != JsonValueKind.Array || quotes.GetArrayLength() > AiIntegrity.MaxCandidates) throw Invalid();
            foreach (var q in quotes.EnumerateArray())
            {
                if (q.ValueKind != JsonValueKind.Object || q.EnumerateObject().Any(p => p.Name is not ("originalText" or "explanation" or "prefixHint" or "suffixHint")) || q.EnumerateObject().Select(p => p.Name).Distinct().Count() != q.EnumerateObject().Count()) throw Invalid();
                string? Field(string name, bool required)
                { if (!q.TryGetProperty(name, out var field)) { if (required) throw Invalid(); return null; } if (field.ValueKind == JsonValueKind.Null && !required) return null; if (field.ValueKind != JsonValueKind.String) throw Invalid(); return field.GetString(); }
                candidates.Add(new(Field("originalText", true)!, Field("explanation", true)!, Field("prefixHint", false), Field("suffixHint", false)));
            }
        }
        else
        {
            if (!value.TryGetProperty("analysis", out var a) || a.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(a.GetString()) || a.GetString()!.Length > 32000) throw Invalid();
            analysis = a.GetString();
        }
        int? Tokens(string name) => root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty(name, out var token) && token.ValueKind == JsonValueKind.Number && token.TryGetInt32(out var n) && n >= 0 ? n : null;
        return new(analysis, candidates.AsReadOnly(), null, Tokens("prompt_tokens"), Tokens("completion_tokens"));
    }
    public void Dispose() { _http.Dispose(); _mock.Dispose(); }
}
