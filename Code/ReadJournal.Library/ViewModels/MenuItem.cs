using CommunityToolkit.Mvvm.ComponentModel;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public class MenuItem : ObservableObject
{
    private bool _isSelected;
    public string Name { get; }
    public string View { get; }
    public string IconData { get; }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public MenuItem(string name, string view, string iconData = "", bool isSelected = false)
    {
        Name = name;
        View = view;
        IconData = iconData;
        _isSelected = isSelected;
    }
    public static IEnumerable<MenuItem> MenuItems { get; } =
    [
        new("阅读概览", MenuNavigationConstant.OverviewView, "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z", true),
        new("我的书库", MenuNavigationConstant.BookListView, "M4 19.5A2.5 2.5 0 0 1 6.5 17H20V3H6.5A2.5 2.5 0 0 0 4 5.5v14zm2.5-.5a1 1 0 0 0 0 2H20v-2H6.5z"),
        new("书摘收藏", MenuNavigationConstant.QuoteListView, "M6 17h3l2-4V7H5v6h3l-2 4zm8 0h3l2-4V7h-6v6h3l-2 4z"),
        new("阅读计划", MenuNavigationConstant.ReadingPlanListView, "M19 4h-1V2h-2v2H8V2H6v2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2zm0 16H5V10h14v10zm-9-4l-3-3 1.4-1.4 1.6 1.6 4.6-4.6L16 11l-6 5z"),
        new("阅读统计", MenuNavigationConstant.StatisticsView, "M5 9.2h3V19H5V9.2zM10.6 5h2.8v14h-2.8V5zm5.6 8H19v6h-2.8v-6z"),
        new("设置与数据", MenuNavigationConstant.SettingsView, "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.488.488 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z")
    ];
}
