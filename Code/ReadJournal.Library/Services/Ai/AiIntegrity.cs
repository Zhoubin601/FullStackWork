using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Ai;

public sealed class AiException(string code, string message) : Exception(message)
{ public string Code { get; } = code; }

public static class AiIntegrity
{
    public const int MaxInput = 60000, MaxCandidates = 32, MaxResponseBytes = 262144;
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static string ScopeHash(AiScopeSnapshot scope) => Hash(JsonSerializer.Serialize(new { scope.Context, scope.Layout, scope.DisplayScope, scope.Original, scope.Kind }));
    public static string Dedupe(Guid bookUid, DocumentRange range) => Hash(bookUid.ToString("D") + "\n" + ReaderContractValidator.WritePosition(range.Start) + "\n" + ReaderContractValidator.WritePosition(range.End));
    public static string ContentHash(Quote q) => Hash(JsonSerializer.Serialize(new { q.BookId, q.Text, q.Thoughts, q.Page, q.IsFavorite, CreatedTicks = q.CreatedAt.Ticks, UpdatedTicks = q.UpdatedAt.Ticks }));
    public static AiScopeSnapshot Freeze(AiScopeSnapshot scope) => scope with { Original = scope.Original with { Spans = Array.AsReadOnly(scope.Original.Spans.ToArray()) } };
    public static void Check(AiScopeSnapshot scope, int limit = MaxInput)
    {
        if (!Enum.IsDefined(scope.Kind)) throw new AiException("InvalidScope", "正文范围类型无效，请重新选择。");
        var spans = scope.Original.Spans;
        if (spans.Count is 0 or > 2048 || spans.Sum(s => (long)s.Text.Length) > limit || spans.All(s => string.IsNullOrWhiteSpace(s.Text)))
            throw new AiException("LimitExceeded", "范围为空或超过正文限额，请缩小页范围或选区。");
        if (scope.Context.Identity.Document != scope.Original.Document || scope.Context.SessionId == Guid.Empty ||
            scope.ScopeSha256 != ScopeHash(scope) || scope.Original.TextSha256 != Hash(string.Concat(spans.Select(s => s.Text))))
            throw new AiException("InvalidScope", "正文快照身份或摘要不一致，请重新选择。");
        foreach (var span in spans)
        {
            ReaderContractValidator.Validate(span.Range);
            if (span.Range.Start.Document != scope.Original.Document || span.Range.Start.ChapterKey != span.Range.End.ChapterKey ||
                span.Range.Start.NonTextBlockKey is not null || span.Range.End.NonTextBlockKey is not null || span.Text.Contains('\ufffc') ||
                span.Text.Length != span.Range.End.Utf16Offset - span.Range.Start.Utf16Offset)
                throw new AiException("InvalidScope", "范围必须保留连续纯文本映射，不能跨图片拼接。");
        }
    }
    public static async Task ValidateOriginalAsync(AiScopeSnapshot scope, IReaderDocumentService documents, CancellationToken ct)
    {
        Check(scope); var descriptor = await documents.DescribeAsync(scope.Original.Document, ct); long previousEnd = -1;
        foreach (var span in scope.Original.Spans)
        {
            var chapter = descriptor.Chapters.FirstOrDefault(c => c.Key == span.Range.Start.ChapterKey) ?? throw new AiException("InvalidScope", "章节已失效。");
            var start = descriptor.Chapters.Take(chapter.Order).Sum(c => (long)c.Utf16Length) + span.Range.Start.Utf16Offset;
            if (start < previousEnd) throw new AiException("InvalidScope", "正文范围逆序或重叠。");
            previousEnd = start + span.Text.Length;
            var actual = await documents.ExtractAsync(span.Range, ct);
            if (actual.Spans.Count != 1 || actual.Spans[0] != span) throw new AiException("InvalidScope", "原句与受管正文不一致。");
        }
    }
    public static string Message(Exception error) => error switch
    {
        AiException ai => ai.Message,
        OperationCanceledException => "已取消，未完成的生成不会保存。",
        _ => "操作未完成，请重试本地保存或重新打开阅读器。"
    };
}
