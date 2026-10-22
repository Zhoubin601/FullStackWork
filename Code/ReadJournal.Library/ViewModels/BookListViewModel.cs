using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;
public partial class BookListViewModel : ViewModelBase
{
    private readonly IBookStorage _books;
    private IEbookLibraryService? _library;
    private IAppFileDialogService? _dialogs;
    private IReaderPlatformService? _platform;
    private CancellationTokenSource? _importCancellation;
    [ObservableProperty] private string _importEncoding = "UTF-8";
    [ObservableProperty] private string _operationMessage = "";
    [ObservableProperty] private bool _isImporting;
    public string[] ImportEncodings { get; } = ["UTF-8", "GB18030"];
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private string _state = "全部";
    [ObservableProperty] private IReadOnlyList<BookProgress> _booksPage = [];
    [ObservableProperty] private string _pageText = "";
    [ObservableProperty] private bool _isEmpty;
    private int _page = 1;
    public string[] States { get; } = ["全部", "未开始", "在读", "已完成"];
    public IRelayCommand<BookProgress> OpenBookCommand { get; }
    public IRelayCommand AddBookCommand { get; }
    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IAsyncRelayCommand ImportBooksCommand { get; }
    public IRelayCommand CancelImportCommand { get; }
    public IAsyncRelayCommand ScanInboxCommand { get; }
    public IAsyncRelayCommand OpenInboxCommand { get; }
    public BookListViewModel(IBookStorage books, IContentNavigationService content)
    {
        _books = books;
        OpenBookCommand = new RelayCommand<BookProgress>(b => { if (b != null) content.NavigateTo(ContentNavigationConstant.BookDetailView, b.Book.Id); });
        AddBookCommand = new RelayCommand(() => content.NavigateTo(ContentNavigationConstant.BookEditView));
        SearchCommand = new AsyncRelayCommand(() => RunAsync(async () => { _page = 1; await LoadAsync(); }));
        NextPageCommand = new AsyncRelayCommand(() => RunAsync(async () => { _page++; await LoadAsync(); }));
        PreviousPageCommand = new AsyncRelayCommand(() => RunAsync(async () => { _page = Math.Max(1, _page - 1); await LoadAsync(); }));
        ImportBooksCommand = new AsyncRelayCommand(() => RunAsync(ImportAsync));
        CancelImportCommand = new RelayCommand(() => _importCancellation?.Cancel());
        ScanInboxCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            var result = await (_library ?? throw new InvalidOperationException("导入服务未就绪。")).ScanInboxAsync(CancellationToken.None);
            OperationMessage = $"收件扫描：新增{result.Imported}本，已存在{result.Duplicates}本，暂缓/失败{result.Rejected}个。" +
                string.Join("\n", result.Items.Where(i => i.State == ImportState.Rejected).Select(i => i.Message)) + (_library.LastWarning ?? "");
            _page = 1; await LoadAsync();
        }));
        OpenInboxCommand = new AsyncRelayCommand(() => RunAsync(() => (_platform ?? throw new InvalidOperationException("目录服务未就绪。")).OpenLibraryDirectoryAsync(CancellationToken.None)));
    }
    public BookListViewModel(IBookStorage books, IContentNavigationService content, IEbookLibraryService library, IAppFileDialogService dialogs, IReaderPlatformService platform)
        : this(books, content) { _library = library; _dialogs = dialogs; _platform = platform; }
    private async Task ImportAsync()
    {
        if (_dialogs is null || _library is null) throw new InvalidOperationException("导入服务未就绪。");
        using var cancellation = new CancellationTokenSource(); _importCancellation = cancellation; IsImporting = true;
        try
        {
            var selected = await _dialogs.ChooseBooksAsync(cancellation.Token);
            var messages = new List<string>(); var acceptingProgress = true; var version = ParameterVersion;
            var progress = new Progress<ImportProgress>(p => { if (acceptingProgress && version == ParameterVersion) OperationMessage = p.Fraction is double f ? $"{p.Step} {f:P0}" : p.Step; });
            foreach (var path in selected)
            {
                var result = await _library.ImportAsync(new(path, ImportEncoding == "GB18030" ? "gb18030" : null),
                    progress, cancellation.Token);
                messages.Add(Path.GetFileName(path) + "：" + result.Message);
                if (result.State == ImportState.Cancelled || cancellation.IsCancellationRequested) break;
            }
            acceptingProgress = false; OperationMessage = string.Join("\n", messages); _page = 1; await LoadAsync();
        }
        catch (OperationCanceledException) { OperationMessage = "已取消选择或导入。"; }
        finally { _importCancellation = null; IsImporting = false; }
    }
    public void ShowOperationMessage(string message) => OperationMessage = message;
    protected override async Task LoadAsync()
    {
        var version = ParameterVersion;
        var result = await _books.SearchAsync(Query, State, _page, 10);
        if (version != ParameterVersion) return;
        var pages = Math.Max(1, (result.TotalCount + 9) / 10);
        if (_page > pages) { _page = pages; result = await _books.SearchAsync(Query, State, _page, 10); }
        if (version != ParameterVersion) return;
        BooksPage = result.Items; IsEmpty = result.TotalCount == 0; PageText = $"第 {_page}/{pages} 页 · 共 {result.TotalCount} 本";
    }
    public override void InvalidateDataSet() { base.InvalidateDataSet(); BooksPage = []; PageText = ""; OperationMessage = ""; IsEmpty = true; _page = 1; }
}
