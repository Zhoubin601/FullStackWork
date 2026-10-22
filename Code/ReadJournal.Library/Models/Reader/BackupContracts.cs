namespace ReadJournal.Library.Models.Reader;

public sealed record BackupFileEntry(string Path, long Bytes, string Sha256);
public sealed record CompleteBackupManifest(int FormatVersion, int SchemaVersion, DateTimeOffset CreatedAt,
    string DataSha256, IReadOnlyList<BackupFileEntry> Files);
public sealed record CanonicalChapter(string Key, string Title, int Order, string TextPath, int Utf16Length, string Sha256, IReadOnlyList<ReaderBlockDescriptor> Blocks);
public sealed record CanonicalResource(string Key, string Path, string MediaType, int PixelWidth, int PixelHeight, string Sha256);
public sealed record CanonicalTocEntry(string Key, string Title, string ChapterKey, int Utf16Offset, string? NonTextBlockKey, IReadOnlyList<CanonicalTocEntry> Children);
public sealed record CanonicalManifest(int ContractVersion, string Title,
    IReadOnlyList<CanonicalChapter> Chapters, IReadOnlyList<CanonicalResource> Resources, long TotalTextUnits, IReadOnlyList<CanonicalTocEntry> TableOfContents);
public sealed record BackupLimits(long MaxArchiveBytes = 536870912, long MaxExpandedBytes = 1073741824,
    int MaxEntries = 20000, long MaxEntryBytes = 314572800, int MaxJsonBytes = 16777216, long MaxImagePixels = 25000000);
public sealed record RestoreJournal(int Version, Guid RestoreId, string Stage, bool HadManagedDirectory,
    string AutomaticBackupPath, IReadOnlyList<BackupFileEntry> OldFiles, IReadOnlyList<BackupFileEntry> NewFiles, string OldDataSha256, string NewDataSha256);

public sealed record RestoreJournalEnvelope(int Version, RestoreJournal Payload, string PayloadSha256);
