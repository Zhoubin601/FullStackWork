using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.UnitTest;

// The local mock exercises exactly the same HTTP serialization/response parser as the real adapter.
internal sealed class AiMockHandler : HttpMessageHandler
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
