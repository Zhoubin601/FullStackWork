# ContentNavigationService 工作全链条

下面按"接口 → 实现 → 注册 → 调用方 → 下游效果"逐层梳理，并标注代码位置。

## 1. 接口与常量
`DailyPoetryA.Library/Services/IContentNavigationService.cs:3`（接口）与 `:7`（常量）
```csharp
public interface IContentNavigationService {
    void NavigateTo(string view, object parameter = null);
}
public static class ContentNavigationConstant {
    public const string TodayDetailView = nameof(TodayDetailView);  // "TodayDetailView"
    public const string ResultView      = nameof(ResultView);       // "ResultView"
    public const string DetailView      = nameof(DetailView);       // "DetailView"
}
```
只暴露一个方法，用字符串常量标识目标视图，`parameter` 是弱类型的传参载体。

## 2. 实现
`DailyPoetryA/Services/ContentNavigationService.cs:7`
```csharp
public void NavigateTo(string view, object parameter = null) {
    ViewModelBase viewModel = view switch {
        ContentNavigationConstant.TodayDetailView => ServiceLocator.Current.TodayDetailViewModel,
        ContentNavigationConstant.ResultView      => ServiceLocator.Current.ResultViewModel,
        ContentNavigationConstant.DetailView      => ServiceLocator.Current.DetailViewModel,
        _ => throw new Exception("未知的视图。")
    };
    if (parameter != null) { viewModel.SetParameter(parameter); }
    ServiceLocator.Current.MainViewModel.PushContent(viewModel);
}
```
三步：(a) 字符串 → ViewModel 单例（从 `ServiceLocator` 取，三个目标都是单例）；(b) 多态调用 `SetParameter` 注入参数；(c) `MainViewModel.PushContent` 把 ViewModel 推到内容栈顶。

## 3. DI 注册
`DailyPoetryA/ServiceLocator.cs:83`
```csharp
serviceCollection.AddSingleton<IContentNavigationService, ContentNavigationService>();
```
单例注册，所有 ViewModel 共用同一个实例。

## 4. 调用方（5 处，全部通过构造函数注入）
| 调用方 | 触发方法 | view 常量 | parameter 类型 | 目标 ViewModel |
|---|---|---|---|---|
| `TodayViewModel.cs:69` | `ShowDetail()` | `TodayDetailView` | `TodayPoetry` | `TodayDetailViewModel` |
| `QueryViewModel.cs:85` | `Query()` | `ResultView` | `Expression<Func<Poetry,bool>>` | `ResultViewModel` |
| `ResultViewModel.cs:74` | `ShowPoetry(Poetry)` | `DetailView` | `Poetry` | `DetailViewModel` |
| `FavoriteViewModel.cs:57` | `ShowPoetry(Poetry)` | `DetailView` | `Poetry` | `DetailViewModel` |
| `TodayViewModel.cs:76` | `Query()` | ⚠️ `MenuNavigationConstant.QueryView` | `PoetryQuery` | **会抛异常** |

⚠️ **发现一个潜在 bug**：`TodayViewModel.Query()` (`TodayViewModel.cs:75-79`) 调用的是 `_contentNavigationService.NavigateTo(MenuNavigationConstant.QueryView, ...)`，但 `ContentNavigationService` 的 switch 只认 `ContentNavigationConstant` 的三个值（`TodayDetailView`/`ResultView`/`DetailView`），不认 `"QueryView"`，会落入 `_ => throw new Exception("未知的视图。")`。这里要么应该改用 `IMenuNavigationService`（菜单级导航，由 `MenuNavigationService` 处理 `QueryView`），要么 `ContentNavigationService` 需要补 `QueryView` 分支。对比 `TodayDetailViewModel.Query()` (`TodayDetailViewModel.cs:30-34`) 用的是 `_menuNavigationService.NavigateTo(MenuNavigationConstant.QueryView, ...)`，是正确的对照写法。

⚠️ **两种修法的行为差异**：改用 `IMenuNavigationService` 与补 `QueryView` 分支并非等价——`MenuNavigationService.NavigateTo` 调用 `MainViewModel.SetMenuAndContent` (`MainViewModel.cs:52`)，会先 `ContentStack.Clear()` 再 `PushContent`，**清空导航历史**；而 `ContentNavigationService.NavigateTo` 调用 `MainViewModel.PushContent` (`MainViewModel.cs:49`)，仅 `ContentStack.Insert(0, ...)`，**保留历史**。从 `TodayViewModel.Query()` 的语义看（从"今日推荐"跳到"诗词搜索"主页面），改用 `IMenuNavigationService` 更合理（菜单级切换本就该重置历史），但若希望保留回退路径则需在 `ContentNavigationService` 补分支。

## 5. SetParameter 多态分发
`ViewModelBase.SetParameter` (`ViewModelBase.cs:6`) 是空虚方法，各子类 override：
- `TodayDetailViewModel.SetParameter` (`:24`)：`TodayPoetry = parameter as TodayPoetry`
- `ResultViewModel.SetParameter` (`:44`)：类型守卫 `Expression<Func<Poetry,bool>>`，赋值 `_where`、重置 `_canLoadMore`、清空 `PoetryCollection`
- `DetailViewModel.SetParameter` (`:18`)：类型守卫 `Poetry`，赋值 `Poetry` 属性
- `QueryViewModel.SetParameter` (`:20`)：接收 `PoetryQuery`（但当前链路下 ContentNavigationService 不会路由到 QueryViewModel，此 override 实际不会被 ContentNavigationService 触发）

## 6. PushContent 与界面更新
`MainViewModel.cs:49`
```csharp
public void PushContent(ViewModelBase content) =>
    ContentStack.Insert(0, Content = content);
```
`Content = content` 通过 `SetProperty` 触发 `PropertyChanged`，Avalonia 的 `MainView.axaml` (`:119-121`) 仅通过 `Content="{Binding Content}"` 绑定驱动当前显示的 UserControl 切换（`ViewLocator`/`DataTemplate` 会按 ViewModel 类型解析对应 View，`ViewLocator` 在 `App.axaml:13-15` 注册）。`ContentStack` 不参与显示切换，仅维护导航历史：`MainViewModel.GoBack()` (`:82`) 通过 `RemoveAt(0)` + 恢复 `Content` 实现回退，回退按钮可见性由 `MainView.axaml:101` 的 `ContentStack.Count` 绑定控制。

## 全链条时序（以"搜索→结果→详情"为例）
```
QueryViewModel.Query()                                    [Library/ViewModels/QueryViewModel.cs:50]
  → 构造 Expression<Func<Poetry,bool>> where              [:61-83]
  → _contentNavigationService.NavigateTo(ResultView, where)  [:85]
      → ContentNavigationService.NavigateTo                [Services/ContentNavigationService.cs:8]
          → ServiceLocator.Current.ResultViewModel          [:12-13]  (单例)
          → ResultViewModel.SetParameter(where)             [ResultViewModel.cs:44]  → _where/清空集合
          → MainViewModel.PushContent(resultViewModel)      [MainViewModel.cs:49]
              → Content = resultViewModel  (PropertyChanged)
              → ContentStack.Insert(0, ...)
                  → Avalonia MainView 绑定切换 → ResultView 显示
                      → ResultViewModel.AvaloniaInfiniteScrollCollection.OnLoadMore
                          → poetryStorage.GetPoetriesAsync(_where, ...)  [ResultViewModel.cs:22]
                      → 用户点击某首诗 → ShowPoetryCommand
                          → ResultViewModel.ShowPoetry(poetry)  [:73]
                              → NavigateTo(DetailView, poetry)  [:74]
                                  → DetailViewModel.SetParameter(poetry)  → Poetry = poetry
                                  → MainViewModel.PushContent(detailViewModel)
                                      → DetailView 显示，OnLoadedCommand 异步加载 Favorite
```

## 小结
- **职责**：`ContentNavigationService` 专门负责"内容区"（主区域）的 ViewModel 级导航，与 `MenuNavigationService`（侧边菜单切换 Today/Query/Favorite 三大主页面）、`RootNavigationService`（根窗口 Initialization/Main 切换）分层并行。
- **机制**：字符串路由 + ServiceLocator 单例 + SetParameter 多态传参 + MainViewModel.ContentStack 栈式推送 + Avalonia 属性绑定驱动 View 切换。
- **需关注**：`TodayViewModel.Query()` 误用 `MenuNavigationConstant.QueryView` 调用 `IContentNavigationService`，运行时会抛 `未知的视图。` 异常，建议修正。