using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public partial class QuoteEditViewModel : EditViewModelBase
{
    private readonly IBookStorage _books; private readonly IQuoteStorage _quotes; private Quote _quote = new();
    [ObservableProperty] private IReadOnlyList<Book> _booksList = [];
    [ObservableProperty] private Book? _selectedBook;
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _thoughts = "";
    [ObservableProperty] private string _page = "";
    [ObservableProperty] private bool _isFavorite;
    public QuoteEditViewModel(IBookStorage books, IQuoteStorage quotes, IAlertService alert, MainViewModel main) : base(alert, main) { _books = books; _quotes = quotes; }
    public override void SetParameter(object parameter) { base.SetParameter(parameter); _quote = parameter as Quote ?? new(); SelectedBook = null; BooksList = []; Text = Thoughts = Page = ""; IsFavorite = false; AcceptChanges(); }
    protected override async Task LoadAsync()
    {
        var preserveDraft = HasLoaded && IsDirty; var version = ParameterVersion; var quote = _quote;
        var initialBook = HasLoaded ? SelectedBook?.Id : null;
        var initialText = HasLoaded ? Text : ""; var initialThoughts = HasLoaded ? Thoughts : ""; var initialPage = HasLoaded ? Page : ""; var initialFavorite = HasLoaded && IsFavorite;
        if (quote.Id != 0) quote = await _quotes.GetAsync(quote.Id);
        var books = await _books.GetAllAsync(); if (version != ParameterVersion) return; _quote = quote ?? throw new InvalidOperationException("书摘已不存在。");
        var selectedId = preserveDraft || SelectedBook?.Id != initialBook ? SelectedBook?.Id : _quote.BookId;
        BooksList = books; SelectedBook = BooksList.FirstOrDefault(x => x.Id == selectedId);
        // Choices must still load when a draft already exists; only untouched fields receive stored values.
        if (!preserveDraft)
        {
            if (Text == initialText) Text = _quote.Text;
            if (Thoughts == initialThoughts) Thoughts = _quote.Thoughts;
            if (Page == initialPage) Page = _quote.Page?.ToString() ?? "";
            if (IsFavorite == initialFavorite) IsFavorite = _quote.IsFavorite;
            AcceptChanges(Snapshot(books.FirstOrDefault(x => x.Id == _quote.BookId)?.Id, _quote.Text, _quote.Thoughts, _quote.Page?.ToString() ?? "", _quote.IsFavorite));
        }
        MarkLoaded();
    }
    private static string Snapshot(int? bookId, string text, string thoughts, string page, bool isFavorite) => JsonSerializer.Serialize(new { BookId = bookId, Text = text, Thoughts = thoughts, Page = page, IsFavorite = isFavorite });
    protected override string DraftSnapshot() => Snapshot(SelectedBook?.Id, Text, Thoughts, Page, IsFavorite);
    protected override async Task SaveCoreAsync()
    {
        int? page = null; if (!string.IsNullOrWhiteSpace(Page)) { if (!int.TryParse(Page, out var number)) throw new ArgumentException("书摘页码必须为整数或留空。"); page = number; }
        var version = ParameterVersion;
        var quote = new Quote { Id = _quote.Id, BookId = SelectedBook?.Id ?? 0, Text = Text, Thoughts = Thoughts, Page = page, IsFavorite = IsFavorite };
        quote.Id = await _quotes.SaveAsync(quote);
        if (version == ParameterVersion) _quote = quote;
    }
}
