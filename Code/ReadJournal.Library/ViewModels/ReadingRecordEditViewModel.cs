using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using ReadJournal.Library.Services;
using ReadJournal.Library.Models;
namespace ReadJournal.Library.ViewModels;
public partial class ReadingRecordEditViewModel : EditViewModelBase
{
    private readonly IReadingRecordStorage _records; private ReadingRecord _record = new();
    [ObservableProperty] private string _startPage = "";
    [ObservableProperty] private string _endPage = "";
    [ObservableProperty] private string _minutes = "";
    [ObservableProperty] private DateTimeOffset? _readingDate = DateTime.Today;
    [ObservableProperty] private string _hint = "";
    public event EventHandler? Saved;
    public ReadingRecordEditViewModel(IReadingRecordStorage records, IAlertService alert, MainViewModel main) : base(alert, main) { _records = records; }
    public override void SetParameter(object parameter)
    {
        base.SetParameter(parameter); _record = (ReadingRecord)parameter;
        StartPage = _record.StartPage == 0 ? "" : _record.StartPage.ToString(); EndPage = _record.EndPage == 0 ? "" : _record.EndPage.ToString(); Minutes = _record.Minutes.ToString(); ReadingDate = _record.ReadingDate; AcceptChanges(); MarkLoaded();
    }
    public void SetRereadHint(bool completed) => Hint = completed ? "此书已读完，请手动选择合法页段进行重读。" : "使用实际页码，起止页均计入累计阅读页数。";
    protected override string DraftSnapshot() => JsonSerializer.Serialize(new { StartPage, EndPage, Minutes, ReadingDate });
    protected override async Task SaveCoreAsync()
    {
        if (!int.TryParse(StartPage, out var start) || !int.TryParse(EndPage, out var end) || !int.TryParse(Minutes, out var minutes) || ReadingDate is null) throw new ArgumentException("请填写有效的整数页码、分钟数和阅读日期。");
        var version = ParameterVersion;
        var record = new ReadingRecord { Id = _record.Id, BookId = _record.BookId, StartPage = start, EndPage = end, Minutes = minutes, ReadingDate = ReadingDate.Value.Date };
        record.Id = await _records.SaveAsync(record);
        if (version == ParameterVersion) _record = record;
    }
    protected override Task AfterSavedAsync() { Saved?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
    public override void InvalidateDataSet() => SetParameter(new ReadingRecord());
}
