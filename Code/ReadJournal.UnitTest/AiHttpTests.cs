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
    public async Task DefaultRequiresConsentAndCredentialAndNeverReturnsSimulation()
    {
        await using var x = await AiTestFixture.Create();
        var handler = new Handler((_, _) => throw new Exception("Must not use network"));
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        Assert.Equal("UploadNotAcknowledged", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(AiSettingsStorage.Default), default))).Code);
        var config = AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true };
        Assert.Equal("CredentialMissing", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(config), default))).Code);
        Assert.Equal(0, handler.Count);
    }
    [Fact]
    public async Task TestOnlyHandlerExercisesRealSerializerParser()
    {
        await using var x = await AiTestFixture.Create();
        await x.Secrets.WriteApiKeyAsync("fictional-test-handler", default);
        using var client = new DeepSeekClient(x.Settings, x.Secrets, new AiMockHandler());
        var config = AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true };
        Assert.Single((await client.SendAsync(Request(config), default)).Candidates);
    }
    [Fact]
    public async Task OldMockSettingCannotProduceSimulatedResultsAndIsRemovedOnSave()
    {
        await using var x = await AiTestFixture.Create();
        var path = Path.Combine(x.Db.Paths.DirectoryPath, "ai-settings.json");
        var json = JsonSerializer.Serialize(AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true });
        await File.WriteAllTextAsync(path, json[..^1] + ",\"UseMock\":true}");
        var settings = await x.Settings.GetAsync(default);
        var handler = new Handler((_, _) => throw new Exception("must reject before network"));
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        Assert.Equal("CredentialMissing", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(settings), default))).Code);
        Assert.Equal(0, handler.Count);
        await x.Settings.SaveAsync(settings, default);
        Assert.DoesNotContain("UseMock", await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task RealAdapterPostsOnlyMessagesWithBearerAndRejectsMissingConsentOrCredentialBeforeHttp()
    {
        await using var x = await AiTestFixture.Create(); var key = "fictional-" + Guid.NewGuid().ToString("N");
        var config = AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true };
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
        var error = await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true }), default));
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
        var operation = client.SendAsync(Request(AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true, Timeout = TimeSpan.FromSeconds(1) }), cancel.Token);
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
        var error = await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true }), default));
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
        Assert.Equal("InvalidAiResponse", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true }), default))).Code);
        Assert.Equal(1, handler.Count);
    }
    [Theory][InlineData("http://api.deepseek.com/chat/completions")][InlineData("https://attacker.invalid/chat/completions")][InlineData("https://api.deepseek.com/chat/completions?key=x")][InlineData("https://user:pass@api.deepseek.com/chat/completions")]
    public async Task SettingsRejectCredentialRedirectEndpointsAndNeverPersistThem(string endpoint)
    {
        await using var x = await AiTestFixture.Create(); await Assert.ThrowsAsync<AiException>(() => x.Settings.SaveAsync(AiSettingsStorage.Default with { HttpsEndpoint = endpoint }, default));
        Assert.False(File.Exists(Path.Combine(x.Db.Paths.DirectoryPath, "ai-settings.json")));
    }
    [Theory][InlineData("deepseek-flash")][InlineData("deepseek-v4-pro")]
    public async Task CurrentModelsDisableThinkingAndPreserveReportedUsage(string model)
    {
        await using var x = await AiTestFixture.Create();
        await x.Secrets.WriteApiKeyAsync("fictional-" + Guid.NewGuid().ToString("N"), default);
        var config = AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true, Model = model };
        var handler = new Handler(async (message, ct) =>
        {
            using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync(ct));
            Assert.Equal(model, body.RootElement.GetProperty("model").GetString());
            Assert.Equal("disabled", body.RootElement.GetProperty("thinking").GetProperty("type").GetString());
            Assert.Equal(config.MaxOutputTokens, body.RootElement.GetProperty("max_tokens").GetInt32());
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                choices = new[] { new { message = new { content = "{\"analysis\":\"短文分析\"}" }, finish_reason = "stop" } },
                usage = new { prompt_tokens = 120, completion_tokens = 24 }
            })) };
        });
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        var result = await client.SendAsync(Request(config, AiOperation.Analyze), default);
        Assert.Equal(120, result.InputTokens); Assert.Equal(24, result.OutputTokens); Assert.Equal(1, handler.Count);
    }
    [Theory][InlineData("deepseek-chat")][InlineData("deepseek-reasoner")]
    public async Task LegacyModelRemainsReadableButCannotSendRequests(string model)
    {
        await using var x = await AiTestFixture.Create();
        var config = AiSettingsStorage.Default with { Model = model, HasAcknowledgedTextUpload = true };
        await x.Settings.SaveAsync(config, default); Assert.Equal(model, (await x.Settings.GetAsync(default)).Model);
        var handler = new Handler((_, _) => throw new Exception("must reject before network"));
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        var error = await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(config), default));
        Assert.Equal("ModelUpdateRequired", error.Code); Assert.Contains("deepseek-flash", error.Message);
        Assert.Equal(0, handler.Count);
    }
    [Fact]
    public async Task MismatchedModelAndAlreadyCancelledRequestNeverReachHttp()
    {
        await using var x = await AiTestFixture.Create();
        var config = AiSettingsStorage.Default with { HasAcknowledgedTextUpload = true };
        var handler = new Handler((_, _) => throw new Exception("must reject before network"));
        using var client = new DeepSeekClient(x.Settings, x.Secrets, handler);
        Assert.Equal("InvalidSettings", (await Assert.ThrowsAsync<AiException>(() => client.SendAsync(Request(config) with { Model = "deepseek-v4-pro" }, default))).Code);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(Request(config), cancelled.Token));
        Assert.Equal(0, handler.Count);
    }
}
