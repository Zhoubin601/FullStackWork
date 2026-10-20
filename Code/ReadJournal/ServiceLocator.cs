using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using ReadJournal.Services;
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
        serviceCollection.AddSingleton<IDatabaseService, DatabaseService>();
        serviceCollection.AddSingleton<IRootNavigationService, RootNavigationService>();
        serviceCollection.AddSingleton<IMenuNavigationService, MenuNavigationService>();
        serviceCollection.AddSingleton<IContentNavigationService, ContentNavigationService>();
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
        serviceCollection.AddSingleton<IAlertService, AlertService>();
        serviceCollection.AddSingleton<BookEditViewModel>();
        serviceCollection.AddSingleton<QuoteListViewModel>();
        serviceCollection.AddSingleton<QuoteEditViewModel>();
        serviceCollection.AddSingleton<ReadingRecordEditViewModel>();
        serviceCollection.AddSingleton<IReadingPlanStorage, ReadingPlanStorage>();
        serviceCollection.AddSingleton<IStatisticsService, StatisticsService>();
        serviceCollection.AddSingleton<IPreferenceStorage, FilePreferenceStorage>();
        serviceCollection.AddSingleton<IFileService, FileService>();
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
