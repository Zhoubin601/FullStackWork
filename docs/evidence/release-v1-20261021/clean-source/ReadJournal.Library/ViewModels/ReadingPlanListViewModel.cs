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
    public IRelayCommand AddPlanCommand { get; }
    public IRelayCommand<PlanItem> EditPlanCommand { get; }
    public IAsyncRelayCommand<PlanItem> DeletePlanCommand { get; }
    public ReadingPlanListViewModel(IReadingPlanStorage plans, IContentNavigationService content, IAlertService alert)
    {
        _plans = plans; AddPlanCommand = new RelayCommand(() => content.NavigateTo(ContentNavigationConstant.ReadingPlanEditView));
        EditPlanCommand = new RelayCommand<PlanItem>(p => { if (p != null) content.NavigateTo(ContentNavigationConstant.ReadingPlanEditView, p.Plan); });
        DeletePlanCommand = new AsyncRelayCommand<PlanItem>(p => RunAsync(async () => { if (p != null && await alert.ConfirmAsync("删除计划", "删除计划不会影响阅读记录。是否继续？")) { await plans.DeleteAsync(p.Plan.Id); await LoadAsync(); } }));
    }
    protected override async Task LoadAsync() { PlansList = (await _plans.GetAllAsync()).Where(x => State == "全部" || x.State == State).ToArray(); IsEmpty = PlansList.Count == 0; }
}
