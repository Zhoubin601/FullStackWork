using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using ReadJournal.Library.Services;
using ReadJournal.Library.Models;
namespace ReadJournal.Library.ViewModels;
public partial class BookEditViewModel : EditViewModelBase
{
    private readonly IBookStorage _books; private int _id;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _author = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _totalPages = "";
    [ObservableProperty] private string _description = "";
    public BookEditViewModel(IBookStorage books, IAlertService alert, MainViewModel main) : base(alert, main) { _books = books; }
    public override void SetParameter(object parameter) { base.SetParameter(parameter); _id = parameter is int id ? id : 0; Title = Author = Category = Description = TotalPages = ""; AcceptChanges(); }
    protected override async Task LoadAsync()
    {
        if (IsDirty) return; var version = ParameterVersion; var id = _id;
        if (id != 0) { var book = await _books.GetAsync(id); if (version != ParameterVersion) return; if (book is null) throw new InvalidOperationException("书籍已不存在。"); Title = book.Title; Author = book.Author; Category = book.Category; Description = book.Description; TotalPages = book.TotalPages.ToString(); }
        AcceptChanges();
    }
    protected override string DraftSnapshot() => JsonSerializer.Serialize(new[] { Title, Author, Category, TotalPages, Description });
    protected override async Task SaveCoreAsync()
    {
        if (!int.TryParse(TotalPages, out var pages)) throw new ArgumentException("总页数必须为正整数。");
        await _books.SaveAsync(new Book { Id = _id, Title = Title, Author = Author, Category = Category, TotalPages = pages, Description = Description });
    }
}
