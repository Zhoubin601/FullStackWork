using SQLite;
namespace ReadJournal.Library.Models.Reader;

[Table("EbookFile")]
public sealed class EbookFile
{
    [PrimaryKey] public int BookId { get; set; }
    public Guid FileId { get; set; }
    public EbookFormat Format { get; set; }
    public string SourceRelativePath { get; set; } = "";
    public string CanonicalRelativePath { get; set; } = "";
    public string SourceSha256 { get; set; } = "";
    public string CanonicalSha256 { get; set; } = "";
    public long SourceBytes { get; set; }
    public int ParserVersion { get; set; }
    public long ImportedUtcTicks { get; set; }
    public int Status { get; set; }
}
[Table("EbookProgress")]
public sealed class EbookProgress
{
    [PrimaryKey] public int BookId { get; set; }
    public string DocumentKeyJson { get; set; } = "";
    public string LastPositionJson { get; set; } = "";
    public bool HasStarted { get; set; }
    public double CurrentPercent { get; set; }
    public long? CompletedUtcTicks { get; set; }
    public int? CompletedOffsetMinutes { get; set; }
    public long LastOpenedUtcTicks { get; set; }
    public long LastSavedUtcTicks { get; set; }
}
[Table("EbookReadingSession")]
public sealed class EbookReadingSession
{
    [Ignore, System.Text.Json.Serialization.JsonIgnore]
    public string DisplayText => $"{new DateTimeOffset(ClosedUtcTicks, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(ClosedOffsetMinutes)):yyyy-MM-dd HH:mm:ss} · {DurationMilliseconds / 1000.0:0.###} 秒 · {StartPercent:0.#}% → {EndPercent:0.#}%" + (ClockAnomaly ? "（时钟异常）" : "");
    [PrimaryKey] public Guid SessionId { get; set; }
    public int BookId { get; set; }
    public Guid BookUid { get; set; }
    public string DocumentKeyJson { get; set; } = "";
    public string StartPositionJson { get; set; } = "";
    public string EndPositionJson { get; set; } = "";
    public double StartPercent { get; set; }
    public double EndPercent { get; set; }
    public long OpenedUtcTicks { get; set; }
    public long ClosedUtcTicks { get; set; }
    public int OpenedOffsetMinutes { get; set; }
    public int ClosedOffsetMinutes { get; set; }
    public long DurationMilliseconds { get; set; }
    public string CloseLocalDate { get; set; } = "";
    public bool ClockAnomaly { get; set; }
}
[Table("QuoteOrigin")]
public sealed class QuoteOrigin
{
    [PrimaryKey] public int QuoteId { get; set; }
    public QuoteSource Source { get; set; }
    public string OriginalText { get; set; } = "";
    public string? DocumentKeyJson { get; set; }
    public string? StartPositionJson { get; set; }
    public string? EndPositionJson { get; set; }
    public string? PrefixHash { get; set; }
    public string? SuffixHash { get; set; }
    public int LocationStatus { get; set; }
    public Guid? BatchId { get; set; }
    public string? DedupeKey { get; set; }
}
[Table("AiGenerationBatch")]
public sealed class AiGenerationBatch
{
    [PrimaryKey] public Guid BatchId { get; set; }
    public int BookId { get; set; }
    public Guid BookUid { get; set; }
    public string DocumentKeyJson { get; set; } = "";
    public string ScopeSha256 { get; set; } = "";
    public long CreatedUtcTicks { get; set; }
    public int Status { get; set; }
    public int InsertedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int RejectedCount { get; set; }
    // Immutable receipt survives Quote/BatchItem cascades (P8 writes and replays it).
    public string InsertedQuoteIdsJson { get; set; } = "[]";
}
[Table("AiGenerationBatchItem")]
public sealed class AiGenerationBatchItem
{
    public Guid BatchId { get; set; }
    public int QuoteId { get; set; }
    public long CreatedRevision { get; set; }
    public string CreatedContentHash { get; set; } = "";
}
[Table("AppMetadata")]
public sealed class AppMetadata
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed record BackupBookV2(Book Book, Guid BookUid, BookKind Kind);
public sealed record BackupQuoteV2(Quote Quote, long Revision);
public sealed record BackupDataV2(
    int FormatVersion, IReadOnlyList<BackupBookV2> Books,
    IReadOnlyList<ReadingRecord> ReadingRecords, IReadOnlyList<BackupQuoteV2> Quotes,
    IReadOnlyList<ReadingPlan> ReadingPlans, IReadOnlyList<EbookFile> Files,
    IReadOnlyList<EbookProgress> Progress, IReadOnlyList<EbookReadingSession> Sessions,
    IReadOnlyList<QuoteOrigin> Origins, IReadOnlyList<AiGenerationBatch> Batches,
    IReadOnlyList<AiGenerationBatchItem> BatchItems);
