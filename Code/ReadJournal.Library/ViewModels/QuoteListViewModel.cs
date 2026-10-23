using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;
public partial class QuoteListViewModel : ViewModelBase
{
    public IAsyncRelayCommand<QuoteItem>? UndoBatchCommand { get; private set; }
    [ObservableProperty] private string _batchStatus = "";
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
    public QuoteListViewModel(IQuoteStorage quotes, IContentNavigationService content, IAlertService alert, ReaderQuoteNavigation readerNavigation, IReaderAiService? ai = null, IApplicationDataCoordinator? data = null) : this(quotes, content, alert)
    { UndoBatchCommand = new AsyncRelayCommand<QuoteItem>(q => RunAsync(async () =>
        {
            if (q?.BatchId is not Guid batch || ai is null || data is null) return;
            var epoch = data.CurrentEpoch;
            if (!await alert.ConfirmAsync("撤销AI批次", "只删除此批未编辑且未收藏的新增书摘，保留已修改项与此前已有项。是否继续？")) return;
            var result = await ai.UndoBatchAsync(batch, epoch, default);
            BatchStatus = $"删除{result.DeletedCount}条，保留已修改/收藏{result.RetainedModifiedCount}条" + (result.WasAlreadyUndone ? "（已撤销过）" : ""); await LoadAsync();
        }));
        OpenSourceCommand = new AsyncRelayCommand<QuoteItem>(q => q is null ? Task.CompletedTask : NavigateAsync(() => readerNavigation.OpenAsync(q.Quote.Id))); }
    protected override async Task LoadAsync() { var version = ParameterVersion; var list = await _quotes.SearchAsync(Query, FavoritesOnly); if (version != ParameterVersion) return; QuotesList = list; IsEmpty = QuotesList.Count == 0; }
    public override void InvalidateDataSet() { base.InvalidateDataSet(); QuotesList = []; IsEmpty = true; }
}
