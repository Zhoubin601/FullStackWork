using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using ReadJournal.Library.Services;
using ReadJournal.Library.Models;
namespace ReadJournal.Library.ViewModels;
public partial class ReadingPlanEditViewModel : EditViewModelBase
{
    private readonly IBookStorage _books; private readonly IReadingPlanStorage _plans; private ReadingPlan _plan = new();
    private DateTimeOffset? _initialTargetDate;
    private readonly Dictionary<int, BookProgress> _progress = [];
    [ObservableProperty] private IReadOnlyList<Book> _booksList = [];
    [ObservableProperty] private Book? _selectedBook;
    [ObservableProperty] private DateTimeOffset? _targetDate = DateTime.Today;
    public string Suggestion => SelectedBook is null || TargetDate is null || !_progress.TryGetValue(SelectedBook.Id, out var progress) ? "选择书籍和日期后展示建议。" : new PlanItem { Plan = new() { TargetDate = TargetDate.Value.Date }, Progress = progress }.Suggestion;
    partial void OnSelectedBookChanged(Book? value) => OnPropertyChanged(nameof(Suggestion));
    partial void OnTargetDateChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(Suggestion));
    public ReadingPlanEditViewModel(IBookStorage books, IReadingPlanStorage plans, IAlertService alert, MainViewModel main) : base(alert, main) { _books = books; _plans = plans; }
    public override void SetParameter(object parameter) { base.SetParameter(parameter); _plan = parameter as ReadingPlan ?? new(); SelectedBook = null; TargetDate = _initialTargetDate = DateTime.Today; BooksList = []; _progress.Clear(); AcceptChanges(); }
    protected override async Task LoadAsync()
    {
        var preserveDraft = HasLoaded && IsDirty; var version = ParameterVersion; var plan = _plan;
        var initialBook = HasLoaded ? SelectedBook?.Id : null; var initialDate = HasLoaded ? TargetDate : _initialTargetDate;
        if (plan.Id != 0) plan = await _plans.GetAsync(plan.Id);
        var books = await _books.GetAllAsync(); var progresses = new Dictionary<int, BookProgress>(); foreach (var b in books) { var p = await _books.GetProgressAsync(b.Id); if (p != null) progresses[b.Id] = p; }
        if (version != ParameterVersion) return; _plan = plan ?? throw new InvalidOperationException("计划已不存在。");
        var selectedId = preserveDraft || SelectedBook?.Id != initialBook ? SelectedBook?.Id : _plan.BookId;
        BooksList = books; _progress.Clear(); foreach (var item in progresses) _progress[item.Key] = item.Value;
        SelectedBook = BooksList.FirstOrDefault(x => x.Id == selectedId);
        if (!preserveDraft)
        {
            if (TargetDate == initialDate) TargetDate = _plan.TargetDate;
            AcceptChanges(Snapshot(books.FirstOrDefault(x => x.Id == _plan.BookId)?.Id, _plan.TargetDate));
        }
        OnPropertyChanged(nameof(Suggestion));
        MarkLoaded();
    }
    private static string Snapshot(int? bookId, DateTimeOffset? targetDate) => JsonSerializer.Serialize(new { BookId = bookId, TargetDate = targetDate });
    protected override string DraftSnapshot() => Snapshot(SelectedBook?.Id, TargetDate);
    protected override async Task SaveCoreAsync()
    {
        if (TargetDate is null) throw new ArgumentException("请选择目标日期。");
        var version = ParameterVersion;
        var plan = new ReadingPlan { Id = _plan.Id, BookId = SelectedBook?.Id ?? 0, TargetDate = TargetDate.Value.Date };
        plan.Id = await _plans.SaveAsync(plan);
        if (version == ParameterVersion) _plan = plan;
    }
}
