using System.Net;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Ai;
using Xunit;
namespace ReadJournal.UnitTest;

public sealed class AiHttpTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Count++; return send(request, ct); }
    }
    private static HttpResponseMessage Response(string payload, string finish = "stop") => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = payload }, finish_reason = finish } } }), Encoding.UTF8, "application/json") };
    private static AiProviderRequest Request(AiSettings settings, AiOperation operation = AiOperation.ExtractQuotes) => new(operation, settings.Model, "test schema", "{\"operation\":\"ExtractQuotes\",\"segments\":[\"合成原文。\"]}", settings.MaxOutputTokens, settings);
    [Fact]
    public async Task DefaultMockUsesRealSerializerParserAndCannotCallNetworkOrReadAKey()
    {
        await using var x = await AiTestFixture.Create(); var handler = new Handler((_, _) => throw new Exception("Must not use network"));
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        var result = await client.SendAsync(Request(AiSettingsStorage.Default), default);
        Assert.Single(result.Candidates); Assert.Equal("合成原文。", result.Candidates[0].OriginalText); Assert.Equal(0, handler.Count);
        var analysisRequest = Request(AiSettingsStorage.Default, AiOperation.Analyze) with { UserText = "{\"operation\":\"Analyze\",\"segments\":[\"合成原文。\"]}" };
        Assert.Contains("本地模拟", (await client.SendAsync(analysisRequest, default)).Analysis); Assert.False(await x.Secrets.HasApiKeyAsync(default));
    }
    [Fact]
    public async Task RealAdapterPostsOnlyMessagesWithBearerAndRejectsMissingConsentOrCredentialBeforeHttp()
    {
        await using var x = await AiTestFixture.Create(); var key = "fictional-" + Guid.NewGuid().ToString("N");
        var config = AiSettingsStorage.Default with { UseMock = false, HasAcknowledgedTextUpload = true };
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("https://api.deepseek.com/chat/completions", request.RequestUri!.ToString());
            Assert.True(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == key);
            var raw = await request.Content!.ReadAsStringAsync(ct); Assert.False(raw.Contains(key, StringComparison.Ordinal));
            using var body = JsonDocument.Parse(raw); Assert.False(body.RootElement.GetProperty("stream").GetBoolean()); Assert.Equal("json_object", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
            return Response("{\"quotes\":[{\"originalText\":\"合成原文。\",\"explanation\":\"感想\"}]}");
        });
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        Assert.Equal("CredentialMissing", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(config), default))).Code);
        await x.Secrets.WriteApiKeyAsync(key, default);
        Assert.Equal("UploadNotAcknowledged", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(config with { HasAcknowledgedTextUpload = false }), default))).Code);
        Assert.Equal(0, handler.Count); Assert.Single((await client.SendAsync(Request(config), default)).Candidates); Assert.Equal(1, handler.Count);
    }
    [Theory][InlineData(401,"ProviderUnauthorized")][InlineData(403,"ProviderUnauthorized")][InlineData(429,"RateLimited")][InlineData(500,"ProviderUnavailable")][InlineData(503,"ProviderUnavailable")][InlineData(302,"ProviderRejected")][InlineData(400,"ProviderRejected")]
    public async Task HttpFailuresAreClassifiedAndNeverEchoBodyOrAutomaticallyRetry(int status, string code)
    {
        await using var x = await AiTestFixture.Create(); var key = "fictional-" + Guid.NewGuid().ToString("N"); await x.Secrets.WriteApiKeyAsync(key, default);
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(key) }));
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        var error = await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(AiSettingsStorage.Default with { UseMock = false, HasAcknowledgedTextUpload = true }), default));
        Assert.Equal(code, error.Code); Assert.False(error.ToString().Contains(key, StringComparison.Ordinal)); Assert.Equal(1, handler.Count);
    }
    [Theory][InlineData("offline","Offline")][InlineData("disconnect","Interrupted")][InlineData("timeout","Timeout")][InlineData("cancel","cancel")]
    public async Task OfflineDisconnectTimeoutAndCancelAreDistinctAndNeverRetry(string failure, string code)
    {
        await using var x = await AiTestFixture.Create(); var key = "fictional-" + Guid.NewGuid().ToString("N"); await x.Secrets.WriteApiKeyAsync(key, default);
        using var cancel = new CancellationTokenSource();
        var handler = new Handler(async (_, ct) =>
        {
            if (failure == "offline") throw new HttpRequestException(key);
            if (failure == "disconnect") throw new IOException(key);
            if (failure == "cancel") cancel.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct); throw new Exception("unreachable");
        });
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        var operation = client.SendAsync(Request(AiSettingsStorage.Default with { UseMock = false, HasAcknowledgedTextUpload = true, Timeout = TimeSpan.FromSeconds(1) }), cancel.Token);
        if (failure == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        else { var error = await Assert.ThrowsAsync<AiException>(() => operation); Assert.Equal(code, error.Code); Assert.False(error.ToString().Contains(key, StringComparison.Ordinal)); }
        Assert.Equal(1, handler.Count);
    }
    [Theory]
    [InlineData("not-json")][InlineData("{\"quotes\":null}")][InlineData("{\"quotes\":[{\"originalText\":123,\"explanation\":\"x\"}]}")]
    [InlineData("{\"quotes\":[{\"originalText\":\"x\",\"explanation\":\"y\",\"offset\":1}]}")]
    [InlineData("{\"quotes\":[{\"originalText\":\"x\",\"originalText\":\"y\",\"explanation\":\"z\"}]}")]
    [InlineData("{\"quotes\":[],\"quotes\":[]}")]
    public async Task MalformedOrUntrustedStructuredResponsesAreRejected(string payload)
    {
        await using var x = await AiTestFixture.Create(); await x.Secrets.WriteApiKeyAsync("fictional-" + Guid.NewGuid().ToString("N"), default);
        var handler = new Handler((_, _) => Task.FromResult(Response(payload))); using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        var error = await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(AiSettingsStorage.Default with { UseMock = false, HasAcknowledgedTextUpload = true }), default));
        Assert.Equal("InvalidAiResponse", error.Code); Assert.Equal(0, await x.QuoteCount());
    }
    [Theory][InlineData("huge")][InlineData("truncated")][InlineData("broken-envelope")][InlineData("too-many")]
    public async Task BoundedBodyIncompleteCompletionsAndCandidateLimitsRejectWithoutWrites(string kind)
    {
        await using var x = await AiTestFixture.Create(); await x.Secrets.WriteApiKeyAsync("fictional-" + Guid.NewGuid().ToString("N"), default);
        var handler = new Handler((_, _) => Task.FromResult(kind switch
        {
            "huge" => Response(new string('x', AiIntegrity.MaxResponseBytes + 1)),
            "truncated" => Response("{\"quotes\":[]}", "length"),
            "too-many" => Response(JsonSerializer.Serialize(new { quotes = Enumerable.Range(0, 33).Select(_ => new { originalText = "x", explanation = "y" }).ToArray() })),
            _ => new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[null]}") }
        }));
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        Assert.Equal("InvalidAiResponse", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(AiSettingsStorage.Default with { UseMock = false, HasAcknowledgedTextUpload = true }), default))).Code);
        Assert.Equal(1, handler.Count);
    }
    [Theory][InlineData("http://api.deepseek.com/chat/completions")][InlineData("https://attacker.invalid/chat/completions")][InlineData("https://api.deepseek.com/chat/completions?key=x")][InlineData("https://user:pass@api.deepseek.com/chat/completions")]
    public async Task SettingsRejectCredentialRedirectEndpointsAndNeverPersistThem(string endpoint)
    {
        await using var x = await AiTestFixture.Create(); await Assert.ThrowsAsync<AiException>(() => x.Settings.SaveAsync(AiSettingsStorage.Default with { HttpsEndpoint = endpoint }, default));
        Assert.False(File.Exists(Path.Combine(x.Db.Paths.DirectoryPath, "ai-settings.json")));
    }
}
