using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public partial class BookDetailViewModel : ViewModelBase
{
    private readonly IBookStorage _books; private readonly IReadingRecordStorage _records; private readonly IQuoteStorage _quotes; private readonly IAlertService _alert; private readonly ReadingRecordEditViewModel _editor;
    private int _id;
    private readonly IReadingPlanStorage _plans;
    [ObservableProperty] private IReadOnlyList<PlanItem> _plansList = [];
    public IRelayCommand AddPlanCommand { get; }
    [ObservableProperty] private BookProgress? _progress;
    public Book? Book => Progress?.Book;
    [ObservableProperty] private IReadOnlyList<ReadingRecord> _recordsList = [];
    [ObservableProperty] private IReadOnlyList<QuoteItem> _quotesList = [];
    public IRelayCommand EditBookCommand { get; }
    public IAsyncRelayCommand DeleteBookCommand { get; }
    public IAsyncRelayCommand AddRecordCommand { get; }
    public IAsyncRelayCommand<ReadingRecord> EditRecordCommand { get; }
    public IAsyncRelayCommand<ReadingRecord> DeleteRecordCommand { get; }
    public IRelayCommand AddQuoteCommand { get; }
    public IRelayCommand<QuoteItem> EditQuoteCommand { get; }
    public BookDetailViewModel(IBookStorage books, IReadingRecordStorage records, IQuoteStorage quotes, IAlertService alert, ReadingRecordEditViewModel editor, IContentNavigationService content, MainViewModel main, IReadingPlanStorage plans)
    {
        _plans = plans; AddPlanCommand = new RelayCommand(() => content.NavigateTo(ContentNavigationConstant.ReadingPlanEditView, new ReadingPlan { BookId = _id }));
        _books = books; _records = records; _quotes = quotes; _alert = alert; _editor = editor;
        EditBookCommand = new RelayCommand(() => content.NavigateTo(ContentNavigationConstant.BookEditView, _id));
        AddQuoteCommand = new RelayCommand(() => content.NavigateTo(ContentNavigationConstant.QuoteEditView, new Quote { BookId = _id }));
        EditQuoteCommand = new RelayCommand<QuoteItem>(q => { if (q != null) content.NavigateTo(ContentNavigationConstant.QuoteEditView, q.Quote); });
        DeleteBookCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            var id = _id;
            var version = ParameterVersion;
            bool IsCurrentDetail() => version == ParameterVersion && ReferenceEquals(main.Content, this);
            if (!IsCurrentDetail()) return;
            var impact = await _books.GetDeleteImpactAsync(id);
            if (!IsCurrentDetail()) return;
            var confirmed = await alert.ConfirmAsync("删除书籍", impact);
            if (!confirmed || !IsCurrentDetail()) return;
            await _books.DeleteAsync(id);
            // A completed operation must not navigate away from a newer page or draft.
            if (IsCurrentDetail()) main.GoBack();
        }));
        AddRecordCommand = new AsyncRelayCommand(() => RunAsync(async () =>
        {
            if (Progress == null) return; var complete = Progress.State == "已完成";
            _editor.SetParameter(new ReadingRecord { BookId = _id, StartPage = complete ? 0 : Progress.CurrentPage + 1, EndPage = 0, Minutes = 1 }); _editor.SetRereadHint(complete);
            await alert.EditRecordAsync(_editor); await LoadAsync();
        }));
        EditRecordCommand = new AsyncRelayCommand<ReadingRecord>(r => RunAsync(async () => { if (r == null) return; _editor.SetParameter(r); _editor.SetRereadHint(false); await alert.EditRecordAsync(_editor); await LoadAsync(); }));
        DeleteRecordCommand = new AsyncRelayCommand<ReadingRecord>(r => RunAsync(async () => { if (r != null && await alert.ConfirmAsync("删除阅读记录", "删除后会重新计算进度和统计。")) { await records.DeleteAsync(r.Id); await LoadAsync(); } }));
    }
    public override void SetParameter(object parameter) { base.SetParameter(parameter); _id = (int)parameter; Progress = null; RecordsList = []; QuotesList = []; PlansList = []; OnPropertyChanged(nameof(Book)); }
    protected override async Task LoadAsync()
    {
        var version = ParameterVersion; var id = _id;
        var progress = await _books.GetProgressAsync(id);
        var records = await _records.GetForBookAsync(id); var quotes = await _quotes.SearchAsync("", false, id);
        var plans = (await _plans.GetAllAsync()).Where(x => x.Plan.BookId == id).ToArray();
        if (version != ParameterVersion) return;
        Progress = progress ?? throw new InvalidOperationException("书籍已不存在，请返回书库。"); OnPropertyChanged(nameof(Book));
        RecordsList = records; QuotesList = quotes; PlansList = plans;
    }
}
