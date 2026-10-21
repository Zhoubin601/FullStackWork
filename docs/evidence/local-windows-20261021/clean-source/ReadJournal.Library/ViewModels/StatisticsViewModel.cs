using CommunityToolkit.Mvvm.ComponentModel;
using ReadJournal.Library.Services;
using ReadJournal.Library.Models;
namespace ReadJournal.Library.ViewModels;
public partial class StatisticsViewModel : ViewModelBase
{
    private readonly IStatisticsService _statistics;
    [ObservableProperty] private ReadingStatistics? _statisticsData;
    public StatisticsViewModel(IStatisticsService statistics) { _statistics = statistics; }
    protected override async Task LoadAsync() => StatisticsData = await _statistics.GetAsync();
}
