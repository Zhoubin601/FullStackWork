# 本轮指导书架构严格对应检查

日期：2026-10-20。检查基线：Git `20d9c67` 及本轮修复后的当前工作区；表内 Code 行号为最终修复后源码行号。本检查直接读取原始 DOCX、只读参考副本和当前 Code，不以旧过程文档或旧测试结果代替源码证据。

## 结论

指导书列出的八项架构机制均有实际实现，并且保留了参考项目的具体组装、导航和视图定位机制。本轮发现并修复一处查询职责越层：基线 `OverviewViewModel.cs:24` 在 IDatabaseService 委托内直接编写 SQLite 查询，现已迁移到 IReadingRecordStorage / ReadingRecordStorage，概览仅调用业务接口。修复后静态检查完整通过，最终 Release DLL 的独立架构探针 71 项全部通过。首轮只检索 SQLite 显式类型名漏掉了泛型调用，已扩展检查规则并更正结论。功能是否全部可用还须结合本轮重新执行的自动化测试和真实 UI 验证，不能由架构结论直接推导。

原始要求文件 `raw/期末项目与报告要求.docx` 的 SHA256 为 `352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d`。本轮使用 ZipFile/XML 完整提取正文，核对了“技术与架构要求”八节。原文允许采用不同技术；本项目既定规划进一步要求保留老师的具体机制，本次按这一更严格范围检查。

## 八项逐项对应

下列路径相对仓库根目录。Code 行号以本轮修复后的实际源码为准，参考行号对应未修改的参考副本。

| 指导书要求 | 当前实现证据 | 对应的参考源码 | 结果 |
|---|---|---|---|
| MVVM 视图与视图模型分离 | `Code/ReadJournal.Library/Models/Book.cs:3` 为带 SQLite 标记的 POCO；`Library/ViewModels/BookDetailViewModel.cs:24` 通过依赖与命令承载交互；所有业务 `Views/*.axaml.cs:3` 仅初始化控件，`Views/MainWindow.axaml.cs:6` 仅转发异步关闭确认 | `daily-poetry-a/DailyPoetryA.Library/ViewModels/ViewModelBase.cs:5`；参考 Views 的 XAML/薄 code-behind | 符合；没有在视图中实现存储或业务规则 |
| 统一导航参数注入 | `Code/ReadJournal.Library/ViewModels/ViewModelBase.cs:27` 保留 `public virtual void SetParameter(object parameter)`；`BookDetailViewModel.cs:54` 及三类内容编辑器 override；UI `Services/MenuNavigationService.cs:18`、`ContentNavigationService.cs:18` 在切换前统一调用 | 参考 `ViewModelBase.cs:6`；参考 Menu/Content 导航服务的参数入口 | 符合；具体页面不解析来源路由 |
| IService 抽象与构造注入 | `Code/ReadJournal.Library/Services/I*.cs` 定义 14 个服务接口；如 `BookDetailViewModel.cs:24` 和 `SettingsViewModel.cs:15` 注入存储/提示/文件服务接口；`Code/ReadJournal/ServiceLocator.cs:25` 起注册接口映射 | 参考 `DailyPoetryA/ServiceLocator.cs:78` 起接口映射及 TodayViewModel 构造注入 | 符合；VM 对 MainViewModel/弹窗编辑 VM 的引用属于 VM 协作，没有引用 UI 实现 |
| 轻量 ServiceLocator 与真实 IoC | `Code/ReadJournal/ServiceLocator.cs:10` 保存 readonly IServiceProvider；`:23` 局部 ServiceCollection；`:24`—`:52` 全部 AddSingleton；`:53` BuildServiceProvider；`:55` 起属性 getter 调用 GetService；`:17` 从 Application.Current.TryGetResource 获取并缓存 Current；`App.axaml:11` 注册资源 | 参考 `ServiceLocator.cs:13`、`:24`、`:66`、`:68`、`:97` 及 `App.axaml:9` | 符合；没有手工实例字典、替代容器或页面工厂 |
| 三层导航与主 VM 返回栈 | `Code/ReadJournal/Services/RootNavigationService.cs:7` 替换根 Content；`MenuNavigationService.cs:19` 调用 SetMenuAndContent；`ContentNavigationService.cs:19` 调用 PushContent。`Library/ViewModels/MainViewModel.cs:13` 持栈，`:32` 压栈，`:37` 清栈切菜单，`:42` 返回并保护栈底。导航服务无构造依赖，NavigateTo 内延迟定位；MainViewModel 构造注入 IMenuNavigationService | 参考三导航实现；参考 `MainViewModel.cs:11`、`:49`、`:52`、`:78`、`:82` | 符合；接口只定义导航能力，栈及菜单状态归 MainViewModel |
| ViewLocator 自动渲染机制 | `Code/ReadJournal/ViewLocator.cs:5` 实现 IDataTemplate；`:8` 匹配 VM；`:12` 按完整类型名替换；`:13` Type.GetType；`:15` Activator.CreateInstance 并按 View 类型缓存；`:16` 绑定 DataContext；`App.axaml:12` 注册数据模板 | 参考 `ViewLocator.cs:9`、`:16`—`:28`、`:32` 及 `App.axaml:14` | 符合；无特殊壳 VM 到 MainView 映射 |
| IO 与 UI 反馈服务化、异步协作 | VM 无直接 File、Directory、HttpClient 或数据库查询调用。`Code/ReadJournal/Services/FileService.cs:11`—`:17` 封装选择器/异步文件；`AlertService.cs:20`—`:29` 封装确认与弹窗；`Library/Services/DatabaseService.cs:14`、`:18`、`:48`、`:51` 异步门禁和工作线程执行 SQLite。`ReadingRecordStorage.cs:8` 封装最近记录查询，`OverviewViewModel.cs:24` 仅调用业务接口 | 参考 IPoetryStorage/IPreferenceStorage/IAlertService 等服务接口与实现 | 符合；原概览查询越层已修复，未用异步包装掩盖职责混合 |
| Library/UI 双工程边界 | `Code/ReadJournal.Library/ReadJournal.Library.csproj:1` 只有 MVVM Toolkit 和 sqlite-net-pcl；UI `Code/ReadJournal/ReadJournal.csproj:1` 单向引用 Library 并承载 Avalonia、DI；Library 无 Avalonia 或 ReadJournal.Views/ReadJournal.Services 依赖 | 参考 Library/与UI工程的职责划分 | 符合；Library 的 Models/Services/ViewModels 可独立测试 |

表中简写 `Library/` 指 `Code/ReadJournal.Library/`，简写 `Views/` 指 `Code/ReadJournal/Views/`。

## 目录概述与实现差异的判定

1. 指导书末段把 NavigationConstant 概述在 UI 壳内，但在“三层服务化导航架构”一节明确说常量归接口。真实参考源码的三类常量位于 `daily-poetry-a/DailyPoetryA.Library/Services/I*NavigationService.cs`，例如 IContentNavigationService.cs:7、IMenuNavigationService.cs:7。当前应用同样放在 Library 对应接口文件内，并且业务调用统一使用常量。这符合真实示例及既定规划，不应把常量移入 UI 而使 Library 反向依赖 UI。
2. 指导书第一节概述“Library 内三层”时提到 Views，但真实参考的 Views 位于 UI 工程。当前应用遵循真实源码及指导书最后的双工程边界：Library 放 Models/Services/ViewModels，UI 放 Views/导航实现/平台服务/定位器。
3. MainWindow 是 App 直接创建的宿主，MainWindow.axaml:1 从资源绑定 MainWindowViewModel；它不经 ViewLocator。参考 `DailyPoetryA/App.axaml.cs:15` 也是直接创建 MainWindow。这是同样的根宿主机制，不是缺失了名为 MainWindowView 的视图。

## 业务保护带来的增量

以下增量保持原有架构职责，没有替换老师机制：

- Menu/Content 导航即使参数为空也调用 SetParameter，使单例编辑页重置为新草稿；参考只在参数非空时调用。入口与“先传参、再切换”顺序相同。
- ContentNavigationService.cs:17 在传参前拦截已在栈内的单例；MainViewModel.cs:34 同时防止重复压栈。这样避免同一实例覆盖旧参数。
- MainViewModel.cs:22—:30 以异步离开确认保护关闭、菜单和返回；EditViewModelBase.cs:40 实现保存/放弃/取消语义。返回栈仍由主 VM 管理。
- ViewModelBase.cs:14—:24 保留公开 Loaded 异步命令并按参数版本重载，等待旧操作结束后继续处理新参数；编辑器门禁与快照属于业务正确性措施。
- BookDetailViewModel.cs:33—:43 固定删除目标与参数版本，避免异步确认后作用于新详情或返回新页面；SettingsViewModel.cs:24 在备份/恢复忙碌时拒绝离开。这些保护不改变三层导航和主 VM 返回栈的归属。
- DatabaseService 使用串行执行与事务；BackupService.cs:21 在数据库工作线程内同步等待纯文件服务完成自动备份。它保持“先备份成功、再替换”的同一事务边界，调用线程由 DatabaseService.cs:51 的 Task.Run 承载，不在 UI 线程同步等待，不判为 UI 卡死或架构偏离。

## 本轮重新生成的静态检查证据

文件：`docs/evidence/strict-architecture-20261020/static-checks.json`。

- 29 项 AddSingleton 注册：14 个服务接口映射、14 个具体 ViewModel、1 个 DataPaths。
- 14 个服务接口全部有且仅有一项单例映射。
- 14 个具体 ViewModel 全部有单例注册和 GetService 属性；13 个按命名约定对应 ViewLocator 视图，MainWindowViewModel 对应直接创建的 MainWindow。
- Root 2、Menu 6、Content 4，共 12 个导航常量与 switch 映射完整。
- 全量扫描 VM 的 ReadAsync/WriteAsync、Table/Query/ExecuteScalar、已知连接接收者的 Execute/Insert/Update/Delete、显式文件/目录/HTTP/SQLite 类型：修复后命中为 0。IDatabaseService 仅余 MainWindowViewModel 与 InitializationViewModel 的启动职责依赖。脚本 `StaticArchitectureCheck.py` 可重新运行；它是保守的源码规则检查，不冒称 C# 语义分析器。
- JSON 保存关键最终源码 SHA256，区分 Git 基线与本轮修复后的工作区。
- 静态检查验证文件存在、x:Class、注册和属性映射。静态检查不能替代运行时反射创建与绑定诊断；动态结果应结合本轮自动化测试和 UI 检查记录。

## 本轮独立运行时探针

在本轮最终修复后的 Release 构建完成后，重新构建 `/workspace/scratch/architecture-probe` 独立项目引用最终输出 DLL，不引用 ProjectReference、不重建或改动 Code。Avalonia.Headless 以实际 App 启动，无 Mock 容器或替代定位器。独立探针构建 0 警告、0 错误，**71 项断言全部通过**；运行结果内记录实际载入的 UI/Library 程序集 SHA256，均与最终 Code Release 输出相同：

- Current 与真实应用资源为同一实例，底层为 Microsoft DI 的 ServiceProvider。
- 14 个具体 VM 和 14 个接口服务全部可从真实容器解析，重复获取均为同一实例。
- 13 个 ViewLocator 目标全部实际反射创建成功，类型名、DataContext 与按类型缓存均正确。
- 2 个根目标、6 个菜单目标、4 个内容目标全部可达；菜单清栈与选择状态正确，参数版本递增一次。切换前注入的具体顺序另由前述源码行号核对。
- 四类内容页的参数到达实际私有状态；同实例重复导航不再注入参数或压栈；返回及栈底保护正确。

运行结果为 `docs/evidence/strict-architecture-20261020/runtime-checks.json` 和 `runtime-probe.log`；独立探针源文件副本为同目录 `RuntimeProbe.cs`、`RuntimeProbe.csproj`。探针只验证实际容器、反射映射和导航机制，未把 Headless 结果冒称为真实桌面 UI 功能验证。

核查未改写 raw、参考源码或升级依赖；应用修复及源码 v3 交付见总复核记录。本文件与独立探针记录修复后的最终状态。

## 查询边界复核及修复

重新全量搜索 ViewModels 的 `Table<`、`ReadAsync`、`WriteAsync`、`Query`、`Insert`、`Execute` 等调用后，发现基线只有 OverviewViewModel.cs:24 直接在数据库连接上组织查询。MainWindowViewModel 对 `IDatabaseService.IsInitialized` 的判断、InitializationViewModel 对 `InitializeAsync` 的调用分别属于根启动与初始化职责，不属于业务查询越层。

已将最近记录查询移动到现有 `IReadingRecordStorage.cs:6` / `ReadingRecordStorage.cs:8` 的 `GetRecentSummariesAsync(int limit)`，由存储服务在同一 ReadAsync 快照内取得书名、排序和限制条数；`OverviewViewModel.cs:15` 改为注入 IReadingRecordStorage，`:24` 仅调用该接口。此调整不新增架构层、不改变单例/定位器/导航机制。

新增 `Code/ReadJournal.UnitTest/OverviewArchitectureTests.cs` 三项永久回归：真实 SQLite 的跨书籍名称关联、日期主序和同日 ID 次序、限制条数；真实空库；严格 Mock 验证概览仅经业务接口加载且无 IDatabaseService 构造依赖。统一构建与测试结果见本轮总验证记录。上述静态 JSON 和 71 项探针均已在此修复后重新生成，不沿用修复前结果。
