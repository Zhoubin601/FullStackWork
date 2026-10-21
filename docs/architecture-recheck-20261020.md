# 最新提交架构独立复核

> 后续状态：本文件P1评估属于0313917修复前。2026-10-21已修复并通过93项完整测试和71项架构复验，见[p1-fix-20261021.md](p1-fix-20261021.md)。以下原始评估保留，不代表当前修复后的未关闭状态。

日期：2026-10-20。代码基准：`0313917`（本轮从 `origin/main` 获取）。本次重新读取原始指导书、v4 约束、参考实际源码及应用代码，没有把旧审查的“通过”作为当前验证结果。范围为架构、真实命令接线的静态审查及本轮 Release 程序集的运行时探针；构建、单元测试和真实窗口结果由本轮主审记录。

## 结论

在本次检查的代码范围内，**指导书八项机制及用户要求保留的 DailyPoetryA 具体架构均有真实实现和调用入口，没有发现需要阻断验收的架构偏离**。服务与 VM 为实际容器单例，主视图直接绑定 MainViewModel；未引入 ShellViewModel、导航宿主或页面工厂替代参考机制。此结论不能单独证明全部功能链路已通过运行验证。

## 来源与防止历史结果误用

- 直接提取 `raw/期末项目与报告要求.docx` 的 `word/document.xml` 正文及表格。SHA256：`352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d`。
- 阅读当前 `docs/plan.md:11`、`:67`、`:101`、`:108`、`:123`、`:127` 的严格约束，区分原始指导书允许技术差异与用户进一步要求具体机制一致。
- 直接比较 `daily-poetry-a/DailyPoetryA` 与 `Code/ReadJournal` 的定位器、应用资源、导航实现、ViewLocator，并检查两套 Library 的基类、主容器、启动与初始化 VM。
- 对 `docs/reference-baseline.json` 的 55 个参考 C# 文件与 9 个参考视图重新逐字节计算 SHA256，64 项全部一致。参考副本没有修改。
- 本次独立提取当前源码：14 个具体 VM，全部有 AddSingleton 注册和定位器 getter；14 个服务接口全部具有单例映射，另有 DataPaths 单例，共 29 项注册。共有 14 个 axaml 视图文件；主窗口直接构造，其他 13 个类型均符合 ViewLocator 命名映射。

## 本轮运行验证

源码扫描和运行时探针均重新执行，证据保存在 `docs/evidence/recheck-20261020/architecture/`，没有覆盖旧证据。静态扫描明确标识当前提交 `0313917`，五项完整性断言全部通过。

在主审本轮构建所得 Release DLL 上，使用 Avalonia.Headless 初始化实际 App 资源、实际 Microsoft DI 容器及实际 ViewLocator。**71 项检查全部通过**：Current 与应用资源同实例、14 个 VM 单例、14 个服务接口单例、13 个反射视图的构造/缓存/DataContext、全部 12 个导航常量、切菜单重置栈、参数注入和到达目标字段、重复目标不重注入、返回及栈底保护。此处不是用 Mock 替换容器或导航服务。

探针引用 test 输出目录，另对 UI 输出、test 输出和探针复制目录的两个程序集逐个 SHA256 核对，三处完全一致：

- `ReadJournal.dll`：`ae437aad9f957a4c3779aaf6812e18ffe641760376477b6f44e83edba7e4982f`。
- `ReadJournal.Library.dll`：`0c31a5bd11056d7c3ebd43b8a774fb60f2fa1b6684315a7bada2662c63b0329f`。

结果分别见 `static-checks.json`、`runtime-checks.json`、`runtime-probe.log` 和 `assembly-hashes.json`。探针项目构建日志为 `runtime-build.log`，构建 0 警告、0 错误。Headless 构造视图不等同于真实桌面窗口操作或业务持久化验收。

主审另复跑同一71项探针及静态规则，全部通过，并将证据脚本沿用的“本轮修复后”元数据改为准确的“0313917本轮只读复核”，以免误认本次改动了应用。新增`architecture/run-static.py`可重跑静态结果；它读取既有检查器、输出到本轮目录，不覆盖历史证据。应用程序集未重建或改写。

## 逐项对应

| 要求 | 当前实际证据 | 判断 |
|---|---|---|
| MVVM 与薄视图 | `Code/ReadJournal.Library/ViewModels/ViewModelBase.cs:4` 继承 ObservableObject；实体在 Models；13 个普通视图 code-behind 只调用 InitializeComponent。`Code/ReadJournal/Views/MainWindow.axaml.cs:6` 仅把平台 Closing 转发到 VM 的 CanCloseAsync，再按结果关闭窗口 | 对应 |
| 统一参数入口 | `ViewModelBase.cs:27` 为 public virtual SetParameter(object parameter)；各详情/编辑 VM 覆写并调用 base。`Code/ReadJournal/Services/MenuNavigationService.cs:18`、`ContentNavigationService.cs:18` 在切换前注入；基类参数版本用于抑制旧请求 | 对应 |
| 接口与构造注入 | `Code/ReadJournal/ServiceLocator.cs:25` 起注册数据库、存储、业务、提示、文件及导航接口；`BookListViewModel.cs:21`、`OverviewViewModel.cs:15` 等构造参数接收接口，未由 VM 手动 new 服务。MainViewModel/记录编辑 VM 作为页面协作依赖由同一容器构造 | 对应 |
| 真正 IoC 与定位器 | `ServiceLocator.cs:10` 保存 readonly IServiceProvider，`:23` 创建局部 ServiceCollection，`:53` BuildServiceProvider，`:55` 起 getter 使用 GetService；所有注册均 AddSingleton，无手工实例字典、自建容器、第二次 BuildServiceProvider | 对应 |
| 资源 Current | `Code/ReadJournal/App.axaml:11` 注册键为 ServiceLocator；`ServiceLocator.cs:17` 调用 Application.Current.TryGetResource 并缓存实例；`:16` 返回已缓存值 | 对应 |
| 三层导航及常量 | Root 方法仅替换 MainWindowViewModel.Content；Menu 定位、传参后 SetMenuAndContent；Content 定位、传参后 PushContent。三实现构造函数没有反向注入主 VM，在 NavigateTo 执行时访问 Current。3 个接口文件各自包含常量类，2 根目标、6 菜单目标、4 内容目标与 switch 一一对应 | 对应 |
| 主 VM 管返回栈 | `Code/ReadJournal.Library/ViewModels/MainViewModel.cs:16` 构造注入 IMenuNavigationService；`:13` 持有 ContentStack；`:32` PushContent，`:37` 切菜单清栈，`:42` 栈底保护及 GoBack；导航接口没有栈成员 | 对应 |
| 自动视图定位 | `Code/ReadJournal/ViewLocator.cs:5` 实现 IDataTemplate，`:8` 匹配 ViewModelBase，`:12` 按完整类型名替换，`:13` Type.GetType，`:15` Activator.CreateInstance 及按视图 Type 缓存，`:16` 设置 DataContext。`App.axaml:12` 注册数据模板 | 对应 |
| IO 与异步 | VM 目录未发现 File/Directory/HttpClient/SQLiteConnection 或数据库查询回调；Overview 使用 IReadingRecordStorage.GetRecentSummariesAsync，数据库查询已在 `ReadingRecordStorage.cs:8`。启动 VM 仅通过 IDatabaseService 检查/初始化；数据库、备份、文件、偏好和提示由服务实现。`DatabaseService.cs:18`、`:51` 在工作线程运行数据库任务，VM 通过异步命令 await | 对应 |
| Library/UI 分层 | Library csproj 仅 MVVM Toolkit 与 sqlite，未引用 Avalonia 或 UI 工程；UI 引用 Library，负责 Views、导航实现、定位器、文件选择及对话框。Library 中接口、实体和 VM 不使用 Avalonia 控件类型 | 对应 |

上表为机制细分，因此行数超过原始指导书的八项。

## 真实接线复核

1. `MainWindow.axaml:1`：DataContext 由资源定位器取得 MainWindowViewModel；Loaded 调用公开 OnInitializedCommand；Content 绑定 VM.Content。启动命令位于 `MainWindowViewModel.cs:13`，经根导航进入初始化/主界面，已初始化时再经菜单导航进入概览。
2. `InitializationView.axaml:1`：Loaded 和重试都绑定公开 OnLoadedCommand。`InitializationViewModel.cs:9` 初始化后依次根导航、菜单导航；异常由基类 RunAsync 记录，未提前切走。
3. `MainView.axaml:1`：菜单按钮调用 MainViewModel.OnMenuTappedCommand，返回按钮调用 GoBackCommand，主区 ContentControl 直接绑定 Content。命令经 CanLeaveAsync 决定是否继续导航；没有另一层壳 VM。
4. 其余 11 个业务/弹窗视图 Loaded 均绑定资源定位器中的本页公开 OnLoadedCommand。该属性定义在 `ViewModelBase.cs:11`；重试按钮使用当前 DataContext 同一命令。各视图 XAML 的 x:Class、对应文件与定位器属性均存在。
5. 列表项操作从 DataTemplate 经 `$parent[UserControl].DataContext` 回到本页 VM；书籍进入详情/编辑，书摘进入书摘编辑，计划进入计划编辑，导航调用全部使用相应常量，没有直接 new 内容视图或散落的导航字符串。
6. 阅读记录弹窗走 `BookDetailViewModel.cs:48` 的同例编辑 VM SetParameter，再经 IAlertService.EditRecordAsync 交给 `Code/ReadJournal/Services/AlertService.cs:24` 的 Window.Content；此处仍以 VM 为 Content，自动由 ViewLocator 渲染。保存使用 Saved 事件关闭弹窗，未压入主栈。规划明确记录弹窗不充当额外内容导航页，此路径是已定义的模态交互扩展。

## 明确的差异与边界

- 参考导航只在参数非空时调用 SetParameter；当前导航每次调用以清理单例新建页的旧参数。当前仍保持“定位目标 → 参数注入 → 切换/压栈”的机制，属于单例草稿保护。
- 内容导航在注入参数前检查同例已在栈内，主 VM 再检查一次；服务不直接改栈。该保护避免重复入口覆盖原参数，未转移栈状态归属。
- GoBack 除参考的出栈操作外，显式触发返回页加载；视图 Loaded 也有加载绑定。重复加载由基类/异步命令保护，运行结果仍需功能复核确认。
- MainViewModel 被编辑 VM 注入用于调用 GoBack，符合当前规划“保存后通过主 VM 返回”；没有把导航栈暴露在服务接口中。
- IDatabaseService 含 SQLiteConnection 委托，属于 Library 内的存储抽象；当前业务 VM 不执行该委托。未把“完全不依赖任意数据库技术”误列为指导书要求。
- 本次没有新增或修复应用代码，没有执行 Windows 桌面；不以静态命令存在替代真实 UI 绑定、操作成功及持久化运行证据。

## 追加：异步业务操作与恢复交错的独立复核

本次架构对应结论不等于功能已全部验收。业务审查代理随后提供了 P1 复现，本审查已直接读取其 `Program.cs`、`result.json` 和单线程同步上下文运行日志，接受为**确定性的服务/VM 并发缺陷证据**，问题在当前代码中尚未修复。

触发链路是 `QuoteListViewModel.cs:22` 的“GetAsync → 修改收藏值 → SaveAsync 整条书摘”与 `SettingsViewModel.cs:21` 的恢复交错。非编辑列表使用 `ViewModelBase.cs:29` 默认允许离开，`MainViewModel.cs:30` 因而允许仍在忙碌的列表切至设置；恢复事务替换了同 ID 的书摘，旧收藏操作随后由 `QuoteStorage.cs:22` 全量 Update，覆盖刚恢复的正文、感想和页码。数据库串行锁与单次事务只能保证每次回调内部原子，不能使分开的 Get 与 Save 成为一个业务原子操作。

复现使用本轮实际 DatabaseService、QuoteStorage、BackupService、QuoteListViewModel、SettingsViewModel 和 MainViewModel；单线程 PumpContext 保留 await 的 UI 线程续体语义。它只在另一条真实 ReadAsync 回调中持有真实数据库 gate，模拟较慢的先前读取。随后正常调用收藏命令、主 VM 菜单命令、设置恢复命令，待各请求入队后释放门闩；没有把收藏 GetAsync 返回值人为挂起，也没有手工调用后续 SaveAsync。观察到书籍为 `RESTORED book`，而书摘正文/感想/页码回到 `OLD quote text` / `OLD thoughts` / `1`，收藏变为 true，恢复仍显示成功，双方 Error 均为空。

文件选择、确认和菜单转发为测试替身，故本项不能描述为“真实桌面点击已复现”。菜单替身执行 SetParameter 后 SetMenuAndContent，与当前实际菜单服务同序。真实 UI 可达性的代码依据是：离开的详情/概览页多阶段 LoadAsync 仍可能继续执行后续数据库读，与当前列表的加载交错；当前列表完成后仍可能有旧页的后续读持锁。此时用户可触发收藏并切往设置恢复。门闩用于扩大慢 IO 时间窗口，风险不依赖多个 UI 线程并行操作。

保留指导书机制的最小修复方向是：变更操作未完成时阻止页面离开，并同时保护直接内容导航入口。仅给 QuoteListViewModel 增加 `CanLeaveAsync => !IsBusy` 仍不完整：其 `AddQuoteCommand`、`EditQuoteCommand` 直接调用内容导航，绕过主 VM 的 CanLeaveAsync；用户仍可先进入无修改的编辑页，再切设置。数据层另可采用字段级原子收藏操作及数据集版本校验，防止恢复前的请求修改恢复后的同 ID 实体。上述均是正确性保护，可以保留现有单例容器、三层导航接口、主 VM 栈和反射 ViewLocator，无需改造架构。本轮仅列明缺陷与建议，未修改源码。
