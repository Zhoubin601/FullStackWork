using System.Text.Json;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Reader;

public static class ReaderContractValidator
{
    public static bool IsSha256(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static void Validate(DocumentKey key)
    {
        if (key is null || key.FileId == Guid.Empty || !IsSha256(key.SourceSha256) || !IsSha256(key.CanonicalSha256) || key.ParserVersion <= 0)
            throw new ArgumentException("文档身份或摘要无效。");
    }
    public static void Validate(ReaderPosition position)
    {
        if (position is null || string.IsNullOrWhiteSpace(position.ChapterKey) || position.Utf16Offset < 0) throw new ArgumentException("正文位置无效。");
        Validate(position.Document);
    }
    public static void Validate(DocumentRange range)
    {
        if (range is null) throw new ArgumentException("正文范围无效。");
        Validate(range.Start); Validate(range.End);
        if (range.Start.Document != range.End.Document || (range.Start.ChapterKey == range.End.ChapterKey && range.End.Utf16Offset <= range.Start.Utf16Offset))
            throw new ArgumentException("正文范围必须非空且属于同一文档。");
    }
    private sealed record Envelope<T>([property: System.Text.Json.Serialization.JsonRequired] int ContractVersion, [property: System.Text.Json.Serialization.JsonRequired] T Value);
    private static readonly JsonSerializerOptions Strict = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static string WritePosition(ReaderPosition position) { Validate(position); return JsonSerializer.Serialize(new Envelope<ReaderPosition>(1, position)); }
    public static ReaderPosition ReadPosition(string json)
    {
        var envelope = JsonSerializer.Deserialize<Envelope<ReaderPosition>>(json, Strict);
        if (envelope is null || envelope.ContractVersion != 1) throw new ArgumentException("不支持的位置契约版本。");
        Validate(envelope.Value); return envelope.Value;
    }
    public static string WriteDocument(DocumentKey key) { Validate(key); return JsonSerializer.Serialize(new Envelope<DocumentKey>(1, key)); }
    public static DocumentKey ReadDocument(string json)
    {
        var envelope = JsonSerializer.Deserialize<Envelope<DocumentKey>>(json, Strict);
        if (envelope is null || envelope.ContractVersion != 1) throw new ArgumentException("不支持的文档契约版本。");
        Validate(envelope.Value); return envelope.Value;
    }
}
