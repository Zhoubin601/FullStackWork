# 参考源码核对与阶段 1 验收

日期：2026-10-20。参考副本随 origin 提交 `0dc2d02` 同步至 `daily-poetry-a/`，`REFERENCE.md` 标明原参考提交 `947982998212fd50d1f53c7e4fbcf5da79bd9607`。参考目录只读，应用开发只在 Code 中进行。

## 实际核对位置

| 文件（相对 daily-poetry-a） | 源码证据及采用方式 |
|---|---|
| DailyPoetryA/ServiceLocator.cs:13、17～27、66～99 | private readonly IServiceProvider；Current 从应用资源获取并缓存；构造内局部 ServiceCollection，全部 AddSingleton，BuildServiceProvider；属性 GetService |
| DailyPoetryA/App.axaml:7～16 | 应用资源注册定位器；Application.DataTemplates 注册 ViewLocator |
| DailyPoetryA/ViewLocator.cs:9～35 | IDataTemplate；完整类型名称替换、Type.GetType、Activator.CreateInstance、按视图 Type 缓存、DataContext |
| DailyPoetryA/Services/RootNavigationService.cs:7～15 | 导航执行时定位，替换 MainWindowViewModel.Content |
| DailyPoetryA/Services/MenuNavigationService.cs:8～24 | 常量 switch 定位单例，参数先注入，再 SetMenuAndContent |
| DailyPoetryA/Services/ContentNavigationService.cs:8～24 | 常量 switch 定位单例，参数先注入，再 PushContent |
| DailyPoetryA.Library/Services/I*NavigationService.cs | 三接口及各自同文件常量；接口不暴露返回栈 |
| DailyPoetryA.Library/ViewModels/ViewModelBase.cs:5～6 | ObservableObject 与统一 virtual SetParameter(object parameter) |
| DailyPoetryA.Library/ViewModels/MainViewModel.cs:11～18、49～60、78～88 | 构造注入菜单服务；ContentStack、菜单状态、压栈、清栈切菜单及栈底返回保护 |
| DailyPoetryA.Library/ViewModels/MainWindowViewModel.cs:34～42 | 判断初始化后调用根导航，再调用菜单导航 |
| DailyPoetryA.Library/ViewModels/InitializationViewModel.cs:27～40 | 初始化存储后根导航 → 菜单导航；本项目增加失败留页和重试 |
| DailyPoetryA*/DailyPoetryA*.csproj | .NET 8；Avalonia 11.0.10；MVVM Toolkit 8.2.2；SQLite 1.8.116；DI 8.0.0；xUnit 2.4.2、Moq 4.20.70 |

已读取主容器、主窗口、初始化视图的实际 XAML，核对命令绑定、资源和事件机制。指导书允许技术差异，当前用户要求更加严格，按 v4 及本轮指令保留具体架构。常量实际在 Library 内，与指导书目录概述的差异依据真实源码处理。

## 确认存在的具体问题

1. `InitializationViewModel.cs:25` 的 `OnInitializedCommand` 是 private，`Views/InitializationView.axaml:14` 却通过公开属性绑定访问。新项目命令采用 public；参考副本不改写。
2. `TodayViewModel.cs:75～79` 使用 `_contentNavigationService.NavigateTo(MenuNavigationConstant.QueryView, ...)`，而 ContentNavigationService.cs 的 switch 只接受内容目标，默认抛出未知视图。新项目菜单跳转必须使用菜单服务及其常量。

单例参数清理、防止同实例重复入栈、编辑离开确认、数据写入串行化和跨表事务均为阅记新增业务保护；参考 README 明确未处理线程与原子性，不能称为原有机制。

## 实际统计复核

- 完整比对原 JSON 的 55 个源文件和 9 个视图：64 个文件字节 SHA256 全部一致，未归一化换行以掩盖差异。
- 保留原 PowerShell 脚本，云环境没有 pwsh，新增同口径 .NET SDK 自带 Roslyn 分析工具 `docs/tools/TypeInventory`。
- 实际命令：`dotnet build docs/tools/TypeInventory/TypeInventory.csproj`（0 警告、0 错误）；`dotnet docs/tools/TypeInventory/bin/Debug/net8.0/TypeInventory.dll daily-poetry-a DailyPoetryA DailyPoetryA.Library`，结果保存 `docs/reference-baseline-cloud.json`。
- 将 Summary、SourceFiles、Types、Views 与旧 JSON 完整比较，完全一致：63 类、10 接口、9 视图、55 C# 文件。包含默认访问级别 Program，partial 按工程和完整类型名去重，排除测试、生成代码、bin/obj，不计 record、enum、struct。
- 最低线仍为 45 类、7 接口；新应用数量须独立实测。

## 阶段 1 结果

通过：指导书与完整规划已读取，唯一指定措辞已修正，真实参考源码与统计核对完成，需求、页面流转、实体关系和职责清单在 stage-1-design.md 可追溯。此前参考不可用阻塞已解除，按原顺序进入阶段 2。
