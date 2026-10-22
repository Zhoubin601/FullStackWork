using System.Text.Json.Serialization;
namespace ReadJournal.Library.Models.Reader;

public enum BookKind { Manual = 0, Ebook = 1 }
public enum EbookFormat { Txt = 1, Epub = 2 }
public enum ReaderBlockKind { Paragraph, Heading, Image }
public enum QuoteSource { Manual = 0, ReaderSelection = 1, AiGenerated = 2 }
public enum HistorySource { ManualRecord = 0, EbookSession = 1 }
public enum PageDirection { Previous = -1, Next = 1 }
public enum ReaderExitReason { Back, Menu, SwitchBook, WindowClose }
public enum AiOperation { Analyze, ExplainSelection, ExtractQuotes }
public enum MaintenanceKind { Migration, Backup, Restore, Reset }
public enum ImportState { Imported, Duplicate, Rejected, Cancelled }

public readonly record struct DataEpoch(Guid Value);
public readonly record struct LayoutId(Guid Value);

public sealed record DocumentKey(
    Guid FileId,
    string SourceSha256,
    string CanonicalSha256,
    int ParserVersion);

public sealed record ReaderPosition(
    DocumentKey Document,
    string ChapterKey,
    [property: JsonRequired] int Utf16Offset,
    string? NonTextBlockKey = null);

public sealed record DocumentRange(ReaderPosition Start, ReaderPosition End);

public sealed record ReaderOpenRequest(int BookId, DocumentRange? Target = null);

public sealed record ReaderOpenIdentity(
    int BookId, Guid BookUid, DocumentKey Document,
    DataEpoch Epoch, Guid ReaderInstanceId, int ParameterVersion);

public sealed record ReaderContext(ReaderOpenIdentity Identity, Guid SessionId);

public sealed record HistoryKey(HistorySource Source, string SourceId);
public sealed record PointD(double X, double Y);
public sealed record RectD(double X, double Y, double Width, double Height);

public sealed record ChapterDescriptor(
    string Key, string Title, int Order, int Utf16Length);

public sealed record TocEntry(
    string Key, string Title, ReaderPosition Target,
    IReadOnlyList<TocEntry> Children);

public sealed record TextStyleSpan(
    int StartOffset, int Utf16Length, bool Bold, bool Italic,
    int HeadingLevel);

public sealed record ImageResourceRef(
    string Key, int PixelWidth, int PixelHeight,
    string MediaType, string Sha256);

public sealed record ReaderBlockDescriptor(
    string Key, ReaderBlockKind Kind, int StartOffset, int Utf16Length,
    ImageResourceRef? Image, IReadOnlyList<TextStyleSpan> Styles);

public sealed record ReaderDocument(
    DocumentKey Key, string Title,
    IReadOnlyList<ChapterDescriptor> Chapters,
    IReadOnlyList<TocEntry> TableOfContents,
    long TotalTextUnits);

public sealed record ChapterSlice(
    string ChapterKey, int StartOffset, string Text,
    IReadOnlyList<ReaderBlockDescriptor> Blocks);

// Text 是 Range 内连续、可逐字核验的纯文本；遇图片等非文本内容拆分。
public sealed record TextSpan(DocumentRange Range, string Text);

public sealed record TextSnapshot(
    DocumentKey Document,
    IReadOnlyList<TextSpan> Spans,
    string TextSha256);

public sealed record PositionResolution(
    bool IsExact, ReaderPosition? Position, string? Reason);

public sealed record ReaderAppearance(
    string FontFamily, double FontSize, double LineHeight,
    double FirstLineIndent, double ParagraphSpacing,
    double MarginHorizontal, double MarginVertical,
    string Palette);

public sealed record LayoutSettings(
    double ViewportWidth, double ViewportHeight, double DpiScale,
    ReaderAppearance Appearance, int RendererVersion,
    string FontFallbackFingerprint);

public sealed record LayoutHandle(LayoutId Id, DocumentKey Document);

public sealed record PageRef(
    LayoutId Layout, string ChapterKey, int? PageInChapter,
    int BlockStartOffset = 0, int PageInBlock = 1);

public sealed record ReaderPage(
    PageRef Ref, IReadOnlyList<DocumentRange> ContentRanges,
    int? GlobalPageNumber, bool IsDocumentStart, bool IsDocumentEnd);

public sealed record PageSpan(int StartInclusive, int EndInclusive);

public sealed record LayoutIndexStatus(
    LayoutId Layout, int IndexedChapters, int TotalChapters,
    int? ExactGlobalPageCount);

public sealed record PageRangeSnapshot(
    LayoutId Layout, PageSpan Pages,
    IReadOnlyList<ReaderPage> ResolvedPages, TextSnapshot Text);

public sealed record ParserOptions(
    int ParserVersion, string? RequestedEncoding,
    long MaxSourceBytes, long MaxExpandedBytes,
    int MaxArchiveEntries, long MaxImagePixels);

public sealed record ParsedBook(
    EbookFormat Format, string Title, string Author,
    string PreparedDocumentRelativePath,
    string CanonicalSha256, int ParserVersion,
    IReadOnlyList<string> Warnings);

public sealed record ImportRequest(string SourcePath, string? EncodingName = null);
public sealed record ImportProgress(Guid OperationId, string Step, double? Fraction);
public sealed record ImportResult(
    ImportState State, int? BookId, Guid? BookUid, string Message);

public sealed record ScanResult(
    int Imported, int Duplicates, int Rejected,
    IReadOnlyList<ImportResult> Items);

public sealed record DeleteImpact(
    int ManualRecords, int Sessions, int Quotes, int Plans,
    long ManagedFileBytes);

public sealed record SessionCloseCommand(
    ReaderContext Context,
    ReaderPosition StartPosition, ReaderPosition EndPosition,
    DateTimeOffset OpenedAt, DateTimeOffset ClosedAt,
    bool MarkCompleted, ReaderExitReason Reason);

public sealed record SessionCommitResult(
    Guid SessionId, bool WasAlreadyCommitted,
    long DurationMilliseconds, ReaderPosition SavedPosition);

public sealed record ReadingHistoryItem(
    HistoryKey Key, int BookId, Guid BookUid, string BookTitle,
    DateTimeOffset SortTime, DateOnly ActivityDate,
    long DurationMilliseconds, int? RealPages,
    double? StartPercent, double? EndPercent,
    bool CanEdit, string DisplayText);

public sealed record EbookStateInput(
    bool HasStarted, DateTimeOffset? CompletedAt,
    double CurrentPercent, long SessionMilliseconds,
    int SessionCount);

public sealed record AiScopeSnapshot(
    ReaderContext Context, LayoutId? Layout,
    string DisplayScope, TextSnapshot Original,
    string ScopeSha256);

public sealed record AiProviderRequest(
    AiOperation Operation, string Model,
    string SystemPrompt, string UserText, int MaxOutputTokens);

public sealed record AiQuoteCandidate(
    string OriginalText, string Explanation,
    string? PrefixHint, string? SuffixHint);

public sealed record AiProviderResponse(
    string? Analysis,
    IReadOnlyList<AiQuoteCandidate> Candidates,
    string? ProviderRequestId, int? InputTokens, int? OutputTokens);

public sealed record VerifiedQuote(
    DocumentRange Range, string OriginalText, string Explanation);

public sealed record QuoteValidationResult(
    IReadOnlyList<VerifiedQuote> Accepted,
    IReadOnlyList<string> RejectionReasons);

public sealed record QuoteWriteCommand(
    ReaderContext Context, DocumentRange Range,
    string OriginalText, string Thoughts, QuoteSource Source);

public sealed record VerifiedAiBatchCommand(
    Guid BatchId, ReaderContext Context, string ScopeSha256,
    IReadOnlyList<VerifiedQuote> Quotes, int RejectedCount);

public sealed record QuoteBatchResult(
    Guid BatchId, IReadOnlyList<int> InsertedQuoteIds,
    int DuplicateCount, int RejectedCount);

public sealed record UndoBatchResult(
    Guid BatchId, int DeletedCount, int RetainedModifiedCount,
    bool WasAlreadyUndone);

public sealed record AiAnalysisResult(
    string Analysis, string DisplayScope,
    int? InputTokens, int? OutputTokens);

public sealed record AiSettings(
    string HttpsEndpoint, string Model,
    int MaxInputUtf16Units, int MaxOutputTokens,
    TimeSpan Timeout, bool HasAcknowledgedTextUpload);

public sealed record BackupValidationResult(
    bool IsValid, int FormatVersion,
    int BookCount, int ManagedFileCount,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

public sealed record BackupExportResult(
    string Path, string Sha256, int ManagedFileCount);

public sealed record RestoreResult(
    string AutomaticBackupPath, DataEpoch NewEpoch,
    int BookCount, int ManagedFileCount, IReadOnlyList<string>? Warnings = null);
