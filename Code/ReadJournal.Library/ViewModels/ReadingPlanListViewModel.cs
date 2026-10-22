using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public partial class ReadingPlanListViewModel : ViewModelBase
{
    private readonly IReadingPlanStorage _plans;
    [ObservableProperty] private string _state = "全部";
    [ObservableProperty] private IReadOnlyList<PlanItem> _plansList = [];
    [ObservableProperty] private bool _isEmpty;
    public string[] States { get; } = ["全部", "进行中", "已完成", "已逾期"];
    public IAsyncRelayCommand AddPlanCommand { get; }
    public IAsyncRelayCommand<PlanItem> EditPlanCommand { get; }
    public IAsyncRelayCommand<PlanItem> DeletePlanCommand { get; }
    public ReadingPlanListViewModel(IReadingPlanStorage plans, IContentNavigationService content, IAlertService alert)
    {
        _plans = plans; AddPlanCommand = new AsyncRelayCommand(() => NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.ReadingPlanEditView)));
        EditPlanCommand = new AsyncRelayCommand<PlanItem>(p => p is null ? Task.CompletedTask : NavigateAsync(() => content.NavigateToAsync(ContentNavigationConstant.ReadingPlanEditView, p.Plan)));
        DeletePlanCommand = new AsyncRelayCommand<PlanItem>(p => RunAsync(async () => { if (p != null && await alert.ConfirmAsync("删除计划", "删除计划不会影响阅读记录。是否继续？")) { await plans.DeleteAsync(p.Plan.Id); await LoadAsync(); } }));
    }
    protected override async Task LoadAsync() { var version = ParameterVersion; var plans = (await _plans.GetAllAsync()).Where(x => State == "全部" || x.State == State).ToArray(); if (version != ParameterVersion) return; PlansList = plans; IsEmpty = PlansList.Count == 0; }
    public override void InvalidateDataSet() { base.InvalidateDataSet(); PlansList = []; IsEmpty = true; }
}
