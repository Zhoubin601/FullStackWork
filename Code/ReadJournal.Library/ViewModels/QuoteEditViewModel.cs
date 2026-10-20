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
        if (IsDirty) return; var version = ParameterVersion; var quote = _quote;
        if (quote.Id != 0) quote = await _quotes.GetAsync(quote.Id);
        var books = await _books.GetAllAsync(); if (version != ParameterVersion) return; _quote = quote ?? throw new InvalidOperationException("书摘已不存在。");
        BooksList = books; SelectedBook = BooksList.FirstOrDefault(x => x.Id == _quote.BookId);
        Text = _quote.Text; Thoughts = _quote.Thoughts; Page = _quote.Page?.ToString() ?? ""; IsFavorite = _quote.IsFavorite; AcceptChanges();
    }
    protected override string DraftSnapshot() => JsonSerializer.Serialize(new { BookId = SelectedBook?.Id, Text, Thoughts, Page, IsFavorite });
    protected override async Task SaveCoreAsync()
    {
        int? page = null; if (!string.IsNullOrWhiteSpace(Page)) { if (!int.TryParse(Page, out var number)) throw new ArgumentException("书摘页码必须为整数或留空。"); page = number; }
        await _quotes.SaveAsync(new Quote { Id = _quote.Id, BookId = SelectedBook?.Id ?? 0, Text = Text, Thoughts = Thoughts, Page = page, IsFavorite = IsFavorite });
    }
}
