using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Library.ViewModels;
using ReadJournal.Services;
using ReadJournal.Reader;
using ReadJournal.Library.Services.Ai;
namespace ReadJournal;
public class ServiceLocator
{
    private readonly IServiceProvider _serviceProvider;
    private static ServiceLocator? _current;
    public static ServiceLocator Current
    {
        get
        {
            if (_current is not null) return _current;
            if (Application.Current!.TryGetResource(nameof(ServiceLocator), out var resource) && resource is ServiceLocator locator) return _current = locator;
            throw new InvalidOperationException("应用资源中没有 ServiceLocator。");
        }
    }
    public ServiceLocator()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<DataPaths>();
        serviceCollection.AddSingleton<ApplicationDataCoordinator>();
        serviceCollection.AddSingleton<IApplicationDataCoordinator>(sp => sp.GetRequiredService<ApplicationDataCoordinator>());
        serviceCollection.AddSingleton<DatabaseMigrationV2>();
        serviceCollection.AddSingleton<BackupLimits>();
        serviceCollection.AddSingleton<TxtParserLimits>();
        serviceCollection.AddSingleton<ManagedBookFiles>();
        serviceCollection.AddSingleton<BookFileOperations>();
        serviceCollection.AddSingleton<EpubBookParser>();
        serviceCollection.AddSingleton<IBookParser>(sp => sp.GetRequiredService<EpubBookParser>());
        serviceCollection.AddSingleton<TxtBookParser>();
        serviceCollection.AddSingleton<IBookParser>(sp => sp.GetRequiredService<TxtBookParser>());
        serviceCollection.AddSingleton<BookDeletionService>();
        serviceCollection.AddSingleton<IEbookLibraryService, EbookLibraryService>();
        serviceCollection.AddSingleton<BackupV2Validator>();
        serviceCollection.AddSingleton<CompleteBackupArchive>();
        serviceCollection.AddSingleton<RestoreRecoveryService>();
        serviceCollection.AddSingleton<DatabaseService>();
        serviceCollection.AddSingleton<IDatabaseService>(sp => sp.GetRequiredService<DatabaseService>());
        serviceCollection.AddSingleton<IOperationRecoveryService>(sp => sp.GetRequiredService<DatabaseService>());
        serviceCollection.AddSingleton<ICompleteBackupService, CompleteBackupService>();
        serviceCollection.AddSingleton<IDataSetInvalidationService, DataSetInvalidationService>();
        serviceCollection.AddSingleton<IReadingStateCalculator, ReadingStateCalculator>();
        serviceCollection.AddSingleton<BackupEntityValidator>();
        serviceCollection.AddSingleton<BackupV1Adapter>();
        serviceCollection.AddSingleton<IEbookProgressMaintenance, EbookProgressMaintenance>();
        serviceCollection.AddSingleton<ReaderDocumentService>();
        serviceCollection.AddSingleton<IReaderDocumentService>(sp => sp.GetRequiredService<ReaderDocumentService>());
        serviceCollection.AddSingleton<AvaloniaReaderLayoutService>();
        serviceCollection.AddSingleton<IReaderLayoutService>(sp => sp.GetRequiredService<AvaloniaReaderLayoutService>());
        serviceCollection.AddSingleton<IRootNavigationService, RootNavigationService>();
        serviceCollection.AddSingleton<IMenuNavigationService, MenuNavigationService>();
        serviceCollection.AddSingleton<IContentNavigationService, ContentNavigationService>();
        serviceCollection.AddSingleton<ReaderQuoteNavigation>();
        serviceCollection.AddSingleton<ReaderQuoteCheckpoints>();
        serviceCollection.AddSingleton<IReaderQuoteService, ReaderQuoteService>();
        serviceCollection.AddSingleton<IReadingHistoryService, ReadingHistoryService>();
        serviceCollection.AddSingleton<ReaderClock>();
        serviceCollection.AddSingleton<ReaderSessionCheckpoints>();
        serviceCollection.AddSingleton<IReaderSessionService, ReaderSessionService>();
        serviceCollection.AddSingleton<IReaderPreferencesStorage, ReaderPreferencesStorage>();
        serviceCollection.AddSingleton<IReaderFontCatalog, SystemReaderFontCatalog>();
        serviceCollection.AddSingleton<IAiSettingsStorage, AiSettingsStorage>();
        serviceCollection.AddSingleton<ISecretStore, SessionSecretStore>();
        serviceCollection.AddSingleton<AiBatchCheckpoints>();
        serviceCollection.AddSingleton<IAiQuoteVerifier, AiQuoteVerifier>();
        serviceCollection.AddSingleton<IDeepSeekClient>(sp => new DeepSeekClient(sp.GetRequiredService<IAiSettingsStorage>(), sp.GetRequiredService<ISecretStore>()));
        serviceCollection.AddSingleton<AiScopeFactory>();
        serviceCollection.AddSingleton<IReaderAiService, ReaderAiService>();
        serviceCollection.AddSingleton<ReaderViewModel>();
        serviceCollection.AddSingleton<MainWindowViewModel>();
        serviceCollection.AddSingleton<InitializationViewModel>();
        serviceCollection.AddSingleton<MainViewModel>();
        serviceCollection.AddSingleton<OverviewViewModel>();
        serviceCollection.AddSingleton<BookListViewModel>();
        serviceCollection.AddSingleton<BookDetailViewModel>();
        serviceCollection.AddSingleton<IReadingService, ReadingService>();
        serviceCollection.AddSingleton<IBookStorage, BookStorage>();
        serviceCollection.AddSingleton<IReadingRecordStorage, ReadingRecordStorage>();
        serviceCollection.AddSingleton<IQuoteStorage, QuoteStorage>();
        serviceCollection.AddSingleton<AlertService>();
        serviceCollection.AddSingleton<IAlertService>(sp => sp.GetRequiredService<AlertService>());
        serviceCollection.AddSingleton<IReaderAlertService>(sp => sp.GetRequiredService<AlertService>());
        serviceCollection.AddSingleton<BookEditViewModel>();
        serviceCollection.AddSingleton<QuoteListViewModel>();
        serviceCollection.AddSingleton<QuoteEditViewModel>();
        serviceCollection.AddSingleton<ReadingRecordEditViewModel>();
        serviceCollection.AddSingleton<IReadingPlanStorage, ReadingPlanStorage>();
        serviceCollection.AddSingleton<IStatisticsService, StatisticsService>();
        serviceCollection.AddSingleton<IPreferenceStorage, FilePreferenceStorage>();
        serviceCollection.AddSingleton<FileService>();
        serviceCollection.AddSingleton<IFileService>(sp => sp.GetRequiredService<FileService>());
        serviceCollection.AddSingleton<IAppFileDialogService>(sp => sp.GetRequiredService<FileService>());
        serviceCollection.AddSingleton<IReaderPlatformService, ReaderPlatformService>();
        serviceCollection.AddSingleton<IBackupService, BackupService>();
        serviceCollection.AddSingleton<ReadingPlanListViewModel>();
        serviceCollection.AddSingleton<ReadingPlanEditViewModel>();
        serviceCollection.AddSingleton<StatisticsViewModel>();
        serviceCollection.AddSingleton<SettingsViewModel>();
        _serviceProvider = serviceCollection.BuildServiceProvider();
    }
    public MainWindowViewModel MainWindowViewModel => _serviceProvider.GetService<MainWindowViewModel>()!;
    public InitializationViewModel InitializationViewModel => _serviceProvider.GetService<InitializationViewModel>()!;
    public MainViewModel MainViewModel => _serviceProvider.GetService<MainViewModel>()!;
    public OverviewViewModel OverviewViewModel => _serviceProvider.GetService<OverviewViewModel>()!;
    public BookListViewModel BookListViewModel => _serviceProvider.GetService<BookListViewModel>()!;
    public BookDetailViewModel BookDetailViewModel => _serviceProvider.GetService<BookDetailViewModel>()!;

    public ReaderViewModel ReaderViewModel => _serviceProvider.GetRequiredService<ReaderViewModel>();
    public AvaloniaReaderLayoutService ReaderLayoutService => _serviceProvider.GetRequiredService<AvaloniaReaderLayoutService>();
    public BookEditViewModel BookEditViewModel => _serviceProvider.GetService<BookEditViewModel>()!;
    public QuoteListViewModel QuoteListViewModel => _serviceProvider.GetService<QuoteListViewModel>()!;
    public QuoteEditViewModel QuoteEditViewModel => _serviceProvider.GetService<QuoteEditViewModel>()!;
    public ReadingRecordEditViewModel ReadingRecordEditViewModel => _serviceProvider.GetService<ReadingRecordEditViewModel>()!;

    public ReadingPlanListViewModel ReadingPlanListViewModel => _serviceProvider.GetService<ReadingPlanListViewModel>()!;
    public ReadingPlanEditViewModel ReadingPlanEditViewModel => _serviceProvider.GetService<ReadingPlanEditViewModel>()!;
    public StatisticsViewModel StatisticsViewModel => _serviceProvider.GetService<StatisticsViewModel>()!;
    public SettingsViewModel SettingsViewModel => _serviceProvider.GetService<SettingsViewModel>()!;
    public IPreferenceStorage PreferenceStorage => _serviceProvider.GetService<IPreferenceStorage>()!;
}
