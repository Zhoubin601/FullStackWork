# DailyPoetryA 参考项目数量基线

复核日期：2026-10-20。使用 Roslyn C# 语法树只读检查，不编译或修改参考项目。

## 结论与口径

- 63 个手写类、10 个接口、9 个视图文件；扫描 55 个 C# 源文件。
- 类最低线为 ceil(63 × 0.7) = 45；接口最低线为 ceil(10 × 0.7) = 7。
- v2 的正则扫描漏计未显式写访问修饰符的 sealed class Program，本清单更正该错误；不是参考项目发生了新增。
- 只统计 UI 与 Library 工程，排除测试、bin、obj 和生成代码；包含所有访问级别的 class/interface，partial 按工程及完整类型名去重，不计 enum、struct 或 record。
- 视图数包含主窗口和主容器；新项目另设 10 个实际业务视图，不依赖容器或弹窗补足数量。
- 统计只代表当前手写源码快照，不是课程规定计数方法的唯一解释，也不证明新项目已经满足数量要求。

参考目录：D:/2026Autumn Semester File/FullStack/Finish/daily-poetry-a

Git HEAD：947982998212fd50d1f53c7e4fbcf5da79bd9607；完整文件 SHA256、类型路径/行号见 reference-baseline.json。

## 复核命令

在项目根目录使用 PowerShell 7 和已安装的 .NET 8 SDK 执行以下命令。脚本只输出 JSON，不修改参考目录；不加重定向即可在终端复核。

```powershell
& '.\docs\tools\Measure-ReferenceProject.ps1' -ReferenceRoot 'D:\2026Autumn Semester File\FullStack\Finish\daily-poetry-a'
```

## 工程分项

| 工程 | 类 | 接口 |
|---|---:|---:|
| DailyPoetryA | 22 | 0 |
| DailyPoetryA.Library | 41 | 10 |

## 类型逐项清单

行号以清单生成时的源码为准。Program 位于 DailyPoetryA/Program.cs:8。

| 类型 | 完整名称 | 源码位置 |
|---|---|---|
| class | DailyPoetryA.App | DailyPoetryA/App.axaml.cs:8 |
| class | DailyPoetryA.Converters.ByteArrayToBitmapConverter | DailyPoetryA/Converters/ByteArrayToBitmapConverter.cs:9 |
| class | DailyPoetryA.Converters.CountToBoolConverter | DailyPoetryA/Converters/CountToBoolConverter.cs:7 |
| class | DailyPoetryA.Converters.NegativeConverter | DailyPoetryA/Converters/NegativeConverter.cs:7 |
| class | DailyPoetryA.Converters.PoetryToStringConverter | DailyPoetryA/Converters/PoetryToStringConverter.cs:8 |
| class | DailyPoetryA.DesignViewModels.ResultDesignViewModel | DailyPoetryA/DesignViewModels/ResultDesignViewModel.cs:8 |
| class | DailyPoetryA.Library.Helpers.ErrorMessageHelper | DailyPoetryA.Library/Helpers/ErrorMessageHelper.cs:3 |
| class | DailyPoetryA.Library.Helpers.PathHelper | DailyPoetryA.Library/Helpers/PathHelper.cs:3 |
| class | DailyPoetryA.Library.Models.Favorite | DailyPoetryA.Library/Models/Favorite.cs:6 |
| class | DailyPoetryA.Library.Models.Poetry | DailyPoetryA.Library/Models/Poetry.cs:3 |
| class | DailyPoetryA.Library.Models.PoetryQuery | DailyPoetryA.Library/Models/PoetryQuery.cs:3 |
| class | DailyPoetryA.Library.Models.TodayImage | DailyPoetryA.Library/Models/TodayImage.cs:3 |
| class | DailyPoetryA.Library.Models.TodayPoetry | DailyPoetryA.Library/Models/TodayPoetry.cs:3 |
| class | DailyPoetryA.Library.Services.BingImageOfTheDay | DailyPoetryA.Library/Services/BingImageService.cs:104 |
| class | DailyPoetryA.Library.Services.BingImageOfTheDayImage | DailyPoetryA.Library/Services/BingImageService.cs:108 |
| class | DailyPoetryA.Library.Services.BingImageService | DailyPoetryA.Library/Services/BingImageService.cs:8 |
| class | DailyPoetryA.Library.Services.ContentNavigationConstant | DailyPoetryA.Library/Services/IContentNavigationService.cs:7 |
| class | DailyPoetryA.Library.Services.FavoriteStorage | DailyPoetryA.Library/Services/FavoriteStorage.cs:6 |
| class | DailyPoetryA.Library.Services.FavoriteStorageConstant | DailyPoetryA.Library/Services/FavoriteStorage.cs:54 |
| class | DailyPoetryA.Library.Services.FavoriteStorageUpdatedEventArgs | DailyPoetryA.Library/Services/IFavoriteStorage.cs:19 |
| class | DailyPoetryA.Library.Services.FilePreferenceStorage | DailyPoetryA.Library/Services/FilePreferenceStorage.cs:5 |
| class | DailyPoetryA.Library.Services.JinrishiciData | DailyPoetryA.Library/Services/JinrishiciService.cs:162 |
| class | DailyPoetryA.Library.Services.JinrishiciOrigin | DailyPoetryA.Library/Services/JinrishiciService.cs:155 |
| class | DailyPoetryA.Library.Services.JinrishiciSentence | DailyPoetryA.Library/Services/JinrishiciService.cs:167 |
| class | DailyPoetryA.Library.Services.JinrishiciService | DailyPoetryA.Library/Services/JinrishiciService.cs:8 |
| class | DailyPoetryA.Library.Services.JinrishiciToken | DailyPoetryA.Library/Services/JinrishiciService.cs:151 |
| class | DailyPoetryA.Library.Services.MenuNavigationConstant | DailyPoetryA.Library/Services/IMenuNavigationService.cs:7 |
| class | DailyPoetryA.Library.Services.PoetryStorage | DailyPoetryA.Library/Services/PoetryStorage.cs:8 |
| class | DailyPoetryA.Library.Services.PoetryStorageConstant | DailyPoetryA.Library/Services/PoetryStorage.cs:54 |
| class | DailyPoetryA.Library.Services.RootNavigationConstant | DailyPoetryA.Library/Services/IRootNavigationService.cs:7 |
| class | DailyPoetryA.Library.Services.TodayImageServiceCheckUpdateResult | DailyPoetryA.Library/Services/ITodayImageService.cs:11 |
| class | DailyPoetryA.Library.Services.TodayImageStorage | DailyPoetryA.Library/Services/TodayImageStorage.cs:6 |
| class | DailyPoetryA.Library.Services.TodayPoetrySources | DailyPoetryA.Library/Services/ITodayPoetryService.cs:9 |
| class | DailyPoetryA.Library.ViewModels.DetailViewModel | DailyPoetryA.Library/ViewModels/DetailViewModel.cs:8 |
| class | DailyPoetryA.Library.ViewModels.FavoriteViewModel | DailyPoetryA.Library/ViewModels/FavoriteViewModel.cs:9 |
| class | DailyPoetryA.Library.ViewModels.FilterType | DailyPoetryA.Library/ViewModels/QueryViewModel.cs:145 |
| class | DailyPoetryA.Library.ViewModels.FilterViewModel | DailyPoetryA.Library/ViewModels/QueryViewModel.cs:113 |
| class | DailyPoetryA.Library.ViewModels.InitializationViewModel | DailyPoetryA.Library/ViewModels/InitializationViewModel.cs:7 |
| class | DailyPoetryA.Library.ViewModels.MainViewModel | DailyPoetryA.Library/ViewModels/MainViewModel.cs:8 |
| class | DailyPoetryA.Library.ViewModels.MainWindowViewModel | DailyPoetryA.Library/ViewModels/MainWindowViewModel.cs:7 |
| class | DailyPoetryA.Library.ViewModels.MenuItem | DailyPoetryA.Library/ViewModels/MainViewModel.cs:92 |
| class | DailyPoetryA.Library.ViewModels.PoetryFavorite | DailyPoetryA.Library/ViewModels/FavoriteViewModel.cs:85 |
| class | DailyPoetryA.Library.ViewModels.QueryViewModel | DailyPoetryA.Library/ViewModels/QueryViewModel.cs:11 |
| class | DailyPoetryA.Library.ViewModels.ResultViewModel | DailyPoetryA.Library/ViewModels/ResultViewModel.cs:9 |
| class | DailyPoetryA.Library.ViewModels.TodayDetailViewModel | DailyPoetryA.Library/ViewModels/TodayDetailViewModel.cs:8 |
| class | DailyPoetryA.Library.ViewModels.TodayViewModel | DailyPoetryA.Library/ViewModels/TodayViewModel.cs:8 |
| class | DailyPoetryA.Library.ViewModels.ViewModelBase | DailyPoetryA.Library/ViewModels/ViewModelBase.cs:5 |
| class | DailyPoetryA.Program | DailyPoetryA/Program.cs:8 |
| class | DailyPoetryA.ServiceLocator | DailyPoetryA/ServiceLocator.cs:12 |
| class | DailyPoetryA.Services.AlertService | DailyPoetryA/Services/AlertService.cs:7 |
| class | DailyPoetryA.Services.ContentNavigationService | DailyPoetryA/Services/ContentNavigationService.cs:7 |
| class | DailyPoetryA.Services.MenuNavigationService | DailyPoetryA/Services/MenuNavigationService.cs:7 |
| class | DailyPoetryA.Services.RootNavigationService | DailyPoetryA/Services/RootNavigationService.cs:6 |
| class | DailyPoetryA.ViewLocator | DailyPoetryA/ViewLocator.cs:9 |
| class | DailyPoetryA.Views.DetailView | DailyPoetryA/Views/DetailView.axaml.cs:7 |
| class | DailyPoetryA.Views.FavoriteView | DailyPoetryA/Views/FavoriteView.axaml.cs:5 |
| class | DailyPoetryA.Views.InitializationView | DailyPoetryA/Views/InitializationView.axaml.cs:5 |
| class | DailyPoetryA.Views.MainView | DailyPoetryA/Views/MainView.axaml.cs:5 |
| class | DailyPoetryA.Views.MainWindow | DailyPoetryA/Views/MainWindow.axaml.cs:5 |
| class | DailyPoetryA.Views.QueryView | DailyPoetryA/Views/QueryView.axaml.cs:5 |
| class | DailyPoetryA.Views.ResultView | DailyPoetryA/Views/ResultView.axaml.cs:5 |
| class | DailyPoetryA.Views.TodayDetailView | DailyPoetryA/Views/TodayDetailView.axaml.cs:5 |
| class | DailyPoetryA.Views.TodayView | DailyPoetryA/Views/TodayView.axaml.cs:5 |
| interface | DailyPoetryA.Library.Services.IAlertService | DailyPoetryA.Library/Services/IAlertService.cs:3 |
| interface | DailyPoetryA.Library.Services.IContentNavigationService | DailyPoetryA.Library/Services/IContentNavigationService.cs:3 |
| interface | DailyPoetryA.Library.Services.IFavoriteStorage | DailyPoetryA.Library/Services/IFavoriteStorage.cs:5 |
| interface | DailyPoetryA.Library.Services.IMenuNavigationService | DailyPoetryA.Library/Services/IMenuNavigationService.cs:3 |
| interface | DailyPoetryA.Library.Services.IPoetryStorage | DailyPoetryA.Library/Services/IPoetryStorage.cs:6 |
| interface | DailyPoetryA.Library.Services.IPreferenceStorage | DailyPoetryA.Library/Services/IPreferenceStorage.cs:3 |
| interface | DailyPoetryA.Library.Services.IRootNavigationService | DailyPoetryA.Library/Services/IRootNavigationService.cs:3 |
| interface | DailyPoetryA.Library.Services.ITodayImageService | DailyPoetryA.Library/Services/ITodayImageService.cs:5 |
| interface | DailyPoetryA.Library.Services.ITodayImageStorage | DailyPoetryA.Library/Services/ITodayImageStorage.cs:5 |
| interface | DailyPoetryA.Library.Services.ITodayPoetryService | DailyPoetryA.Library/Services/ITodayPoetryService.cs:5 |

## 视图逐项清单

- DailyPoetryA/Views/DetailView.axaml
- DailyPoetryA/Views/FavoriteView.axaml
- DailyPoetryA/Views/InitializationView.axaml
- DailyPoetryA/Views/MainView.axaml
- DailyPoetryA/Views/MainWindow.axaml
- DailyPoetryA/Views/QueryView.axaml
- DailyPoetryA/Views/ResultView.axaml
- DailyPoetryA/Views/TodayDetailView.axaml
- DailyPoetryA/Views/TodayView.axaml
