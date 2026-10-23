namespace ReadJournal.Library.Models.Reader;

public sealed record TxtParserLimits(int MaxBlockUtf16 = 8192, int MaxChapterUtf16 = 262144,
    long MaxTextUtf16 = 20_000_000, int MaxBlocks = 100000, int MaxChapters = 2048);
public sealed record BookFileJournal(int Version, Guid OperationId, string Kind, string Stage,
    Guid FileId, int BookId, Guid BookUid, string? SourceSha256, IReadOnlyList<BackupFileEntry> Files);
public sealed record BookFileJournalEnvelope(int Version, BookFileJournal Payload, string PayloadSha256);
