using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;
public partial class BookDetailViewModel : ViewModelBase
{
    private readonly IBookStorage _books; private readonly IReadingRecordStorage _records; private readonly IQuoteStorage _quotes; private readonly IAlertService _alert; private readonly ReadingRecordEditViewModel _editor;
    private int _id;
    private IEbookLibraryService? _library;
    private IApplicationDataCoordinator? _coordinator;
    private IReaderSessionService? _sessions;
    [ObservableProperty] private IReadOnlyList<EbookReadingSession> _sessionsList = [];
    public bool IsEbook => Book?.Kind == BookKind.Ebook;
    public bool CanRead => IsEbook && Progress?.FileStatus == (int)EbookFileStatus.Ready;
    public IAsyncRelayCommand OpenReaderCommand { get; }
    public bool CanAddManualRecord => Book?.Kind == BookKind.Manual;
    partial void OnProgressChanged(BookProgress? value) { OnPropertyChanged(nameof(CanAddManualRecord)); OnPropertyChanged(nameof(IsEbook)); OnPropertyChanged(nameof(CanRead)); OpenReaderCommand?.NotifyCanExecuteChanged(); }
    private readonly IReadingPlanStorage _plans;
    [ObservableProperty] private IReadOnlyList<PlanItem> _plansList = [];
    public IAsyncRelayCommand AddPlanCommand { get; }
    [ObservableProperty] private BookProgress? _progress;
    public Book? Book => Progress?.Book;
    [ObservableProperty] private IReadOnlyList<ReadingRecord> _recordsList = [];
    [ObservableProperty] private IReadOnlyList<QuoteItem> _quotesList = [];
    public IAsyncRelayCommand EditBookCommand { get; }
    public IAsyncRelayCommand DeleteBookCommand { get; }
    public IAsyncRelayCommand AddRecordCommand { get; }
    public IAsyncRelayCommand<ReadingRecord> EditRecordCommand { get; }
    public IAsyncRelayCommand<ReadingRecord> DeleteRecordCommand { get; }
    public IAsyncRelayCommand AddQuoteCommand { get; }
    public IAsyncRelayCommand<QuoteItem> EditQuoteCommand { get; }
    public BookDetailViewModel(IBookStorage books, IReadingRecordStorage records, IQuoteStorage quotes, IAlertService alert, ReadingRecordEditViewModel editor, IContentNavigationService content, MainViewModel main, IReadingPlanStorage plans)
    {
        _plans = plans; AddPlanCommand = new AsyncRelayCommand(() => NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.ReadingPlanEditView, new ReadingPlan { BookId = _id })));
        _books = books; _records = records; _quotes = quotes; _alert = alert; _editor = editor;
        OpenReaderCommand = new AsyncRelayCommand(() => NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.ReaderView, new ReaderOpenRequest(_id))), () => CanRead);
        EditBookCommand = new AsyncRelayCommand(() => NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.BookEditView, _id)));
        AddQuoteCommand = new AsyncRelayCommand(() => NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.QuoteEditView, new Quote { BookId = _id })));
        EditQuoteCommand = new AsyncRelayCommand<QuoteItem>(q => q is null ? Task.CompletedTask : NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.QuoteEditView, q.Quote)));
        DeleteBookCommand = new AsyncRelayCommand(async () =>
        {
            var returnRequested = false; var initialVersion = ParameterVersion;
            await RunAsync(async () =>
            {
            var id = _id;
            var uid = Book?.BookUid;
            var epoch = _coordinator?.CurrentEpoch;
            var version = ParameterVersion;
            bool IsCurrentDetail() => version == ParameterVersion && ReferenceEquals(main.Content, this);
            if (!IsCurrentDetail()) return;
            var impact = await _books.GetDeleteImpactAsync(id);
            if (!IsCurrentDetail()) return;
            var confirmed = await alert.ConfirmAsync("删除书籍", impact);
            if (!confirmed || !IsCurrentDetail()) return;
            if (_library is not null && uid is Guid identity && epoch is DataEpoch expected) await _library.DeleteBookAsync(id, identity, expected, CancellationToken.None);
            else await _books.DeleteAsync(id);
            // A completed operation must not navigate away from a newer page or draft.
            returnRequested = IsCurrentDetail();
            });
            if (returnRequested && initialVersion == ParameterVersion && ReferenceEquals(main.Content, this))
            { await main.RequestBackAsync(this); if (_library?.LastWarning is string warning && main.Content is BookListViewModel list) list.ShowOperationMessage(warning); }
        });
        AddRecordCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            if (Progress == null) return; var complete = Progress.State == "已完成";
            _editor.SetParameter(new ReadingRecord { BookId = _id, StartPage = complete ? 0 : Progress.CurrentPage + 1, EndPage = 0, Minutes = 1 }); _editor.SetRereadHint(complete);
            await alert.EditRecordAsync(_editor); await LoadAsync();
        }));
        EditRecordCommand = new AsyncRelayCommand<ReadingRecord>(r => RunAsync(async () => { if (r == null) return; _editor.SetParameter(r); _editor.SetRereadHint(false); await alert.EditRecordAsync(_editor); await LoadAsync(); }));
        DeleteRecordCommand = new AsyncRelayCommand<ReadingRecord>(r => RunAsync(async () => { if (r != null && await alert.ConfirmAsync("删除阅读记录", "删除后会重新计算进度和统计。")) { await records.DeleteAsync(r.Id); await LoadAsync(); } }));
    }
    public BookDetailViewModel(IBookStorage books, IReadingRecordStorage records, IQuoteStorage quotes, IAlertService alert, ReadingRecordEditViewModel editor, IContentNavigationService content, MainViewModel main, IReadingPlanStorage plans,
        IEbookLibraryService library, IApplicationDataCoordinator coordinator) : this(books, records, quotes, alert, editor, content, main, plans) { _library = library; _coordinator = coordinator; }
    public BookDetailViewModel(IBookStorage books, IReadingRecordStorage records, IQuoteStorage quotes, IAlertService alert, ReadingRecordEditViewModel editor, IContentNavigationService content, MainViewModel main, IReadingPlanStorage plans,
        IEbookLibraryService library, IApplicationDataCoordinator coordinator, IReaderSessionService sessions) : this(books, records, quotes, alert, editor, content, main, plans, library, coordinator) { _sessions = sessions; }
    public override void SetParameter(object parameter) { base.SetParameter(parameter); _id = (int)parameter; Progress = null; RecordsList = []; QuotesList = []; PlansList = []; SessionsList = []; OnPropertyChanged(nameof(Book)); }
    protected override async Task LoadAsync()
    {
        var version = ParameterVersion; var id = _id;
        var progress = await _books.GetProgressAsync(id);
        var records = await _records.GetForBookAsync(id); var quotes = await _quotes.SearchAsync("", false, id);
        var plans = (await _plans.GetAllAsync()).Where(x => x.Plan.BookId == id).ToArray();
        var sessions = _sessions is null ? [] : await _sessions.GetForBookAsync(id, CancellationToken.None);
        if (version != ParameterVersion) return;
        Progress = progress ?? throw new InvalidOperationException("书籍已不存在，请返回书库。"); OnPropertyChanged(nameof(Book));
        RecordsList = records; QuotesList = quotes; PlansList = plans; SessionsList = sessions;
    }
    public override void InvalidateDataSet() { SetParameter(0); }
}
