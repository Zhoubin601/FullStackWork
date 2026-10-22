using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Reader;

public interface IBookParser
{
    EbookFormat Format { get; }
    Task<ParsedBook> ParseAsync(
        string stagedSourcePath, string workspacePath,
        ParserOptions options, CancellationToken ct);
}

public interface IEbookLibraryService
{
    Task<ImportResult> ImportAsync(
        ImportRequest request, IProgress<ImportProgress>? progress,
        CancellationToken ct);
    Task<ScanResult> ScanInboxAsync(CancellationToken ct);
    Task ReconcileAsync(CancellationToken ct);
    Task<DeleteImpact> GetDeleteImpactAsync(int bookId, CancellationToken ct);
    Task DeleteBookAsync(
        int bookId, Guid bookUid, DataEpoch expectedEpoch, CancellationToken ct);
}

public interface IReaderDocumentService
{
    Task ReleaseAllAsync();
    Task<ReaderDocument> DescribeAsync(DocumentKey document, CancellationToken ct);
    Task<ReaderDocument> OpenAsync(
        ReaderOpenIdentity identity, CancellationToken ct);
    Task<ChapterSlice> ReadSliceAsync(
        DocumentKey document, string chapterKey,
        int startOffset, int maxUtf16Units, CancellationToken ct);
    Task<TextSnapshot> ExtractAsync(
        DocumentRange range, CancellationToken ct);
    Task<Stream> OpenResourceAsync(
        DocumentKey document, string resourceKey, CancellationToken ct);
    Task<double> GetPercentAsync(ReaderPosition position, CancellationToken ct);
    Task<PositionResolution> ResolveAsync(
        ReaderPosition position, string? expectedOriginalText,
        CancellationToken ct);
    Task ReleaseAsync(DocumentKey document);
}

public interface IReaderLayoutService
{
    Task ReleaseAllAsync();
    Task<LayoutHandle> CreateAsync(
        DocumentKey document, LayoutSettings settings,
        ReaderPosition anchor, CancellationToken ct);
    Task<ReaderPage> LocateAsync(
        LayoutHandle layout, ReaderPosition anchor, CancellationToken ct);
    Task<ReaderPage?> GetAdjacentAsync(
        PageRef page, PageDirection direction, CancellationToken ct);
    Task<PageRangeSnapshot> ResolveGlobalPagesAsync(
        LayoutHandle layout, PageSpan pages,
        IProgress<LayoutIndexStatus>? progress, CancellationToken ct);
    Task<LayoutIndexStatus> GetIndexStatusAsync(
        LayoutHandle layout, CancellationToken ct);
    Task<ReaderPosition?> HitTestAsync(
        PageRef page, PointD point, CancellationToken ct);
    Task<IReadOnlyList<RectD>> GetSelectionRectsAsync(
        PageRef page, DocumentRange selection, CancellationToken ct);
    Task ReleaseAsync(LayoutHandle layout);
}

public interface IReaderPreferencesStorage
{
    Task<ReaderAppearance> GetAsync(CancellationToken ct);
    Task SaveAsync(ReaderAppearance appearance, CancellationToken ct);
}

public interface IReaderPlatformService
{
    Task CopyTextAsync(string text, CancellationToken ct);
    Task SetFullScreenAsync(bool enabled, CancellationToken ct);
    Task OpenLibraryDirectoryAsync(CancellationToken ct);
}

public interface IAppFileDialogService
{
    Task<IReadOnlyList<string>> ChooseBooksAsync(CancellationToken ct);
    Task<string?> ChooseBackupSavePathAsync(CancellationToken ct);
    Task<string?> ChooseBackupOpenPathAsync(CancellationToken ct);
}

public interface IReaderActionLease : IAsyncDisposable { }

public interface IReaderSessionService
{
    Task<ReaderContext> BeginAsync(
        ReaderOpenIdentity identity, ReaderPosition start,
        DateTimeOffset openedAt, CancellationToken ct);
    Task<SessionCommitResult> CloseAsync(
        SessionCloseCommand command, CancellationToken ct);
    Task<SessionCommitResult?> FindCommittedAsync(
        Guid sessionId, CancellationToken ct);
    Task<IReaderActionLease> EnterActiveActionAsync(
        ReaderContext context, CancellationToken ct);
    Task MarkCompletedAsync(ReaderContext context, CancellationToken ct);
    Task ResetProgressAsync(
        int bookId, Guid bookUid, DataEpoch expectedEpoch,
        CancellationToken ct);
}

public interface IDataOperationLease : IAsyncDisposable
{
    DataEpoch Epoch { get; }
}

public interface IDataMaintenanceLease : IAsyncDisposable
{
    DataEpoch Epoch { get; }
    DataEpoch CompleteReplacement();
}

public interface IReaderRegistration : IAsyncDisposable { }

public interface IApplicationDataCoordinator
{
    DataEpoch CurrentEpoch { get; }
    Task<IDataOperationLease> EnterOperationAsync(
        DataEpoch expectedEpoch, CancellationToken ct);
    Task<IDataMaintenanceLease> EnterMaintenanceAsync(
        MaintenanceKind kind, CancellationToken ct);
    Task<IReaderRegistration> RegisterReaderAsync(
        ReaderOpenIdentity identity, CancellationToken ct);
}

public interface IReadingHistoryService
{
    Task<PageResult<ReadingHistoryItem>> GetAsync(
        int? bookId, int page, int pageSize, CancellationToken ct);
    Task DeleteAsync(
        HistoryKey key, DataEpoch expectedEpoch, CancellationToken ct);
}

public interface IReadingStateCalculator
{
    BookProgress Calculate(
        Book book, IReadOnlyList<ReadingRecord> manualRecords,
        EbookStateInput? ebook);
}

public interface IReaderQuoteService
{
    Task<int> SaveSelectionAsync(
        ReaderContext context, DocumentRange range,
        string thoughts, CancellationToken ct);
    Task<PositionResolution> ResolveQuoteAsync(int quoteId, CancellationToken ct);
}

public interface IAiQuoteVerifier
{
    QuoteValidationResult Verify(
        AiScopeSnapshot scope, IReadOnlyList<AiQuoteCandidate> candidates);
}

public interface IDeepSeekClient
{
    Task<AiProviderResponse> SendAsync(
        AiProviderRequest request, CancellationToken ct);
}

public interface IReaderAiService
{
    Task<AiAnalysisResult> AnalyzeAsync(
        AiScopeSnapshot scope, AiOperation operation,
        CancellationToken ct);
    Task<QuoteBatchResult> GenerateAndSaveQuotesAsync(
        AiScopeSnapshot scope, CancellationToken ct);
    Task<UndoBatchResult> UndoBatchAsync(
        Guid batchId, DataEpoch expectedEpoch, CancellationToken ct);
}

public interface IAiSettingsStorage
{
    Task<AiSettings> GetAsync(CancellationToken ct);
    Task SaveAsync(AiSettings settings, CancellationToken ct);
}

public interface ISecretStore
{
    Task<bool> HasApiKeyAsync(CancellationToken ct);
    Task<string?> ReadApiKeyAsync(CancellationToken ct);
    Task WriteApiKeyAsync(string apiKey, CancellationToken ct);
    Task DeleteApiKeyAsync(CancellationToken ct);
}

public interface ICompleteBackupService
{
    Task<BackupValidationResult> ValidateAsync(string path, CancellationToken ct);
    Task<BackupExportResult> ExportAsync(string path, CancellationToken ct);
    Task<RestoreResult> RestoreAsync(string path, CancellationToken ct);
}

public interface IOperationRecoveryService
{
    Task RecoverBeforeBusinessReadyAsync(CancellationToken ct);
}

public interface IDataSetInvalidationService
{
    Task InvalidateAsync(CancellationToken ct);
}
