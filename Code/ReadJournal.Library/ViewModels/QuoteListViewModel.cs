using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;
public partial class QuoteListViewModel : ViewModelBase
{
    public IAsyncRelayCommand<QuoteItem>? OpenSourceCommand { get; private set; }
    private readonly IQuoteStorage _quotes;
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _favoritesOnly;
    [ObservableProperty] private IReadOnlyList<QuoteItem> _quotesList = [];
    [ObservableProperty] private bool _isEmpty;
    public IAsyncRelayCommand AddQuoteCommand { get; }
    public IAsyncRelayCommand<QuoteItem> EditQuoteCommand { get; }
    public IAsyncRelayCommand<QuoteItem> DeleteQuoteCommand { get; }
    public IAsyncRelayCommand<QuoteItem> ToggleFavoriteCommand { get; }
    public QuoteListViewModel(IQuoteStorage quotes, IContentNavigationService content, IAlertService alert)
    {
        _quotes = quotes; AddQuoteCommand = new AsyncRelayCommand(() => NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.QuoteEditView)));
        EditQuoteCommand = new AsyncRelayCommand<QuoteItem>(q => q is null ? Task.CompletedTask : NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.QuoteEditView, q.Quote)));
        DeleteQuoteCommand = new AsyncRelayCommand<QuoteItem>(q => RunAsync(async () => { if (q != null && await alert.ConfirmAsync("删除书摘", "确定删除这条书摘吗？")) { await quotes.DeleteAsync(q.Quote.Id); await LoadAsync(); } }));
        ToggleFavoriteCommand = new AsyncRelayCommand<QuoteItem>(q => RunAsync(async () => { if (q == null) return; await quotes.ToggleFavoriteAsync(q.Quote.Id); await LoadAsync(); }));
    }
    public QuoteListViewModel(IQuoteStorage quotes, IContentNavigationService content, IAlertService alert, ReaderQuoteNavigation readerNavigation) : this(quotes, content, alert)
    { OpenSourceCommand = new AsyncRelayCommand<QuoteItem>(q => q is null ? Task.CompletedTask : NavigateAsync(() => readerNavigation.OpenAsync(q.Quote.Id))); }
    protected override async Task LoadAsync() { var version = ParameterVersion; var list = await _quotes.SearchAsync(Query, FavoritesOnly); if (version != ParameterVersion) return; QuotesList = list; IsEmpty = QuotesList.Count == 0; }
    public override void InvalidateDataSet() { base.InvalidateDataSet(); QuotesList = []; IsEmpty = true; }
}
