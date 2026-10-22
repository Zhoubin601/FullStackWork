using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public partial class BookListViewModel : ViewModelBase
{
    private readonly IBookStorage _books;
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
    public BookListViewModel(IBookStorage books, IContentNavigationService content)
    {
        _books = books;
        OpenBookCommand = new RelayCommand<BookProgress>(b => { if (b != null) content.NavigateTo(ContentNavigationConstant.BookDetailView, b.Book.Id); });
        AddBookCommand = new RelayCommand(() => content.NavigateTo(ContentNavigationConstant.BookEditView));
        SearchCommand = new AsyncRelayCommand(() => RunAsync(async () => { _page = 1; await LoadAsync(); }));
        NextPageCommand = new AsyncRelayCommand(() => RunAsync(async () => { _page++; await LoadAsync(); }));
        PreviousPageCommand = new AsyncRelayCommand(() => RunAsync(async () => { _page = Math.Max(1, _page - 1); await LoadAsync(); }));
    }
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
    public override void InvalidateDataSet() { base.InvalidateDataSet(); BooksPage = []; PageText = ""; IsEmpty = true; _page = 1; }
}
