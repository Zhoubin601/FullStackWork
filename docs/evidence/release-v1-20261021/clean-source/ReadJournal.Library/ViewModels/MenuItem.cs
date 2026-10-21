using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public class MenuItem
{
    public string Name { get; }
    public string View { get; }
    public MenuItem(string name, string view) { Name = name; View = view; }
    public static IEnumerable<MenuItem> MenuItems { get; } = [new("阅读概览", MenuNavigationConstant.OverviewView), new("我的书库", MenuNavigationConstant.BookListView), new("书摘收藏", MenuNavigationConstant.QuoteListView), new("阅读计划", MenuNavigationConstant.ReadingPlanListView), new("阅读统计", MenuNavigationConstant.StatisticsView), new("设置与数据", MenuNavigationConstant.SettingsView)];
}
