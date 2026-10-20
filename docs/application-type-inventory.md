# 阅记最终手写类型与页面清单

统计日期：2026-10-20。以 SDK Roslyn 语法树扫描 UI/Library 全部访问级别的 class/interface；partial 按工程、完整类型名和泛型元数去重，排除测试、bin/obj、生成代码，不计 enum/record/struct。完整哈希见 application-type-inventory.json。

实测 **65 类、14 接口、76 个 C# 文件、14 个视图文件**。课程参考为 63 类、10 接口、9 视图，最低线 45 类、7 接口。14 视图中业务页面 10 个，其余为主窗口、主容器、初始化、阅读记录弹窗。

| 类型 | 工程 | 完整类型名 | 手写声明位置 |
|---|---|---|---|
| class | ReadJournal | ReadJournal.App | ReadJournal/App.axaml.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.Models.BackupDocument | ReadJournal.Library/Models/BackupDocument.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Models.Book | ReadJournal.Library/Models/Book.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Models.BookProgress | ReadJournal.Library/Models/BookProgress.cs:2 |
| class | ReadJournal.Library | ReadJournal.Library.Models.DayActivity | ReadJournal.Library/Models/DayActivity.cs:2 |
| class | ReadJournal.Library | ReadJournal.Library.Models.PageResult`1 | ReadJournal.Library/Models/PageResult.cs:2 |
| class | ReadJournal.Library | ReadJournal.Library.Models.PlanItem | ReadJournal.Library/Models/PlanItem.cs:2 |
| class | ReadJournal.Library | ReadJournal.Library.Models.Quote | ReadJournal.Library/Models/Quote.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Models.QuoteItem | ReadJournal.Library/Models/QuoteItem.cs:2 |
| class | ReadJournal.Library | ReadJournal.Library.Models.ReadingPlan | ReadJournal.Library/Models/ReadingPlan.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Models.ReadingRecord | ReadJournal.Library/Models/ReadingRecord.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Models.ReadingStatistics | ReadJournal.Library/Models/ReadingStatistics.cs:2 |
| class | ReadJournal.Library | ReadJournal.Library.Services.BackupService | ReadJournal.Library/Services/BackupService.cs:5 |
| class | ReadJournal.Library | ReadJournal.Library.Services.BookStorage | ReadJournal.Library/Services/BookStorage.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.ContentNavigationConstant | ReadJournal.Library/Services/IContentNavigationService.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.DatabaseService | ReadJournal.Library/Services/DatabaseService.cs:5 |
| class | ReadJournal.Library | ReadJournal.Library.Services.DataPaths | ReadJournal.Library/Services/DataPaths.cs:2 |
| class | ReadJournal.Library | ReadJournal.Library.Services.DemoData | ReadJournal.Library/Services/DemoData.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.FilePreferenceStorage | ReadJournal.Library/Services/FilePreferenceStorage.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.MenuNavigationConstant | ReadJournal.Library/Services/IMenuNavigationService.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.QuoteStorage | ReadJournal.Library/Services/QuoteStorage.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.ReadingPlanStorage | ReadJournal.Library/Services/ReadingPlanStorage.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.ReadingRecordStorage | ReadJournal.Library/Services/ReadingRecordStorage.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.ReadingService | ReadJournal.Library/Services/ReadingService.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.RootNavigationConstant | ReadJournal.Library/Services/IRootNavigationService.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.Services.StatisticsService | ReadJournal.Library/Services/StatisticsService.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.BookDetailViewModel | ReadJournal.Library/ViewModels/BookDetailViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.BookEditViewModel | ReadJournal.Library/ViewModels/BookEditViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.BookListViewModel | ReadJournal.Library/ViewModels/BookListViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.EditViewModelBase | ReadJournal.Library/ViewModels/EditViewModelBase.cs:5 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.InitializationViewModel | ReadJournal.Library/ViewModels/InitializationViewModel.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.MainViewModel | ReadJournal.Library/ViewModels/MainViewModel.cs:5 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.MainWindowViewModel | ReadJournal.Library/ViewModels/MainWindowViewModel.cs:4 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.MenuItem | ReadJournal.Library/ViewModels/MenuItem.cs:3 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.OverviewViewModel | ReadJournal.Library/ViewModels/OverviewViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.QuoteEditViewModel | ReadJournal.Library/ViewModels/QuoteEditViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.QuoteListViewModel | ReadJournal.Library/ViewModels/QuoteListViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.ReadingPlanEditViewModel | ReadJournal.Library/ViewModels/ReadingPlanEditViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.ReadingPlanListViewModel | ReadJournal.Library/ViewModels/ReadingPlanListViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.ReadingRecordEditViewModel | ReadJournal.Library/ViewModels/ReadingRecordEditViewModel.cs:6 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.SettingsViewModel | ReadJournal.Library/ViewModels/SettingsViewModel.cs:5 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.StatisticsViewModel | ReadJournal.Library/ViewModels/StatisticsViewModel.cs:5 |
| class | ReadJournal.Library | ReadJournal.Library.ViewModels.ViewModelBase | ReadJournal.Library/ViewModels/ViewModelBase.cs:4 |
| class | ReadJournal | ReadJournal.Program | ReadJournal/Program.cs:3 |
| class | ReadJournal | ReadJournal.ServiceLocator | ReadJournal/ServiceLocator.cs:8 |
| class | ReadJournal | ReadJournal.Services.AlertService | ReadJournal/Services/AlertService.cs:8 |
| class | ReadJournal | ReadJournal.Services.ContentNavigationService | ReadJournal/Services/ContentNavigationService.cs:4 |
| class | ReadJournal | ReadJournal.Services.FileService | ReadJournal/Services/FileService.cs:7 |
| class | ReadJournal | ReadJournal.Services.MenuNavigationService | ReadJournal/Services/MenuNavigationService.cs:4 |
| class | ReadJournal | ReadJournal.Services.RootNavigationService | ReadJournal/Services/RootNavigationService.cs:3 |
| class | ReadJournal | ReadJournal.ViewLocator | ReadJournal/ViewLocator.cs:5 |
| class | ReadJournal | ReadJournal.Views.BookDetailView | ReadJournal/Views/BookDetailView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.BookEditView | ReadJournal/Views/BookEditView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.BookListView | ReadJournal/Views/BookListView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.InitializationView | ReadJournal/Views/InitializationView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.MainView | ReadJournal/Views/MainView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.MainWindow | ReadJournal/Views/MainWindow.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.OverviewView | ReadJournal/Views/OverviewView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.QuoteEditView | ReadJournal/Views/QuoteEditView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.QuoteListView | ReadJournal/Views/QuoteListView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.ReadingPlanEditView | ReadJournal/Views/ReadingPlanEditView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.ReadingPlanListView | ReadJournal/Views/ReadingPlanListView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.ReadingRecordEditView | ReadJournal/Views/ReadingRecordEditView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.SettingsView | ReadJournal/Views/SettingsView.axaml.cs:3 |
| class | ReadJournal | ReadJournal.Views.StatisticsView | ReadJournal/Views/StatisticsView.axaml.cs:3 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IAlertService | ReadJournal.Library/Services/IAlertService.cs:4 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IBackupService | ReadJournal.Library/Services/IBackupService.cs:2 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IBookStorage | ReadJournal.Library/Services/IBookStorage.cs:3 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IContentNavigationService | ReadJournal.Library/Services/IContentNavigationService.cs:2 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IDatabaseService | ReadJournal.Library/Services/IDatabaseService.cs:3 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IFileService | ReadJournal.Library/Services/IFileService.cs:2 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IMenuNavigationService | ReadJournal.Library/Services/IMenuNavigationService.cs:2 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IPreferenceStorage | ReadJournal.Library/Services/IPreferenceStorage.cs:2 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IQuoteStorage | ReadJournal.Library/Services/IQuoteStorage.cs:3 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IReadingPlanStorage | ReadJournal.Library/Services/IReadingPlanStorage.cs:3 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IReadingRecordStorage | ReadJournal.Library/Services/IReadingRecordStorage.cs:3 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IReadingService | ReadJournal.Library/Services/IReadingService.cs:3 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IRootNavigationService | ReadJournal.Library/Services/IRootNavigationService.cs:2 |
| interface | ReadJournal.Library | ReadJournal.Library.Services.IStatisticsService | ReadJournal.Library/Services/IStatisticsService.cs:3 |

## 页面

- ReadJournal/Views/BookDetailView.axaml
- ReadJournal/Views/BookEditView.axaml
- ReadJournal/Views/BookListView.axaml
- ReadJournal/Views/InitializationView.axaml
- ReadJournal/Views/MainView.axaml
- ReadJournal/Views/MainWindow.axaml
- ReadJournal/Views/OverviewView.axaml
- ReadJournal/Views/QuoteEditView.axaml
- ReadJournal/Views/QuoteListView.axaml
- ReadJournal/Views/ReadingPlanEditView.axaml
- ReadJournal/Views/ReadingPlanListView.axaml
- ReadJournal/Views/ReadingRecordEditView.axaml
- ReadJournal/Views/SettingsView.axaml
- ReadJournal/Views/StatisticsView.axaml
