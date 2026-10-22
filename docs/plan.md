# “阅记”阅读与书摘管理应用执行规划

版本：v4；修订日期：2026-10-20。状态：已按老师示例架构实施，最新一轮架构严格对应复核及缺陷修复见 docs/architecture-flow-review-20261020.md；Release与源码v3独立解包均83项通过。旧版规划文件已按用户要求清理，本文件为当前唯一有效规划。

## 1. 目标、约束与目录

面向个人，完成“录入书籍 → 制定计划 → 记录阅读 → 整理书摘 → 查看统计”的离线业务闭环。

已确认采用 .NET 8、Avalonia、MVVM、SQLite；第一版记录阅读进度和书摘，不内置电子书阅读器，不联网检索，不增加账号、云同步。默认在 Windows 桌面完成验收。

以原始《期末项目与报告要求.docx》为课程要求，并遵从用户进一步明确的约束：严格采用老师 DailyPoetryA 的具体架构和调用方式，独立实现阅读业务。不得仅以思想相同为理由替换其 IoC、ServiceLocator、导航、ViewLocator 或生命周期组织。依赖首先使用参考项目固定版本，依赖升级不属于第一版目标。

### 工作量与交付约束

- 参考基线：9 个视图文件、63 个类、10 个接口。计数不含测试及生成代码。已使用 Roslyn 语法树复核，包含未显式声明访问修饰符的 Program 类；逐项清单、文件哈希和复核脚本见 `docs/reference-baseline.md`、`docs/reference-baseline.json`、`docs/tools/Measure-ReferenceProject.ps1`。
- 页面：实现 10 个独立、可导航、有实际功能的业务视图，另外设置初始化页、主窗口和主容器；弹窗不用于补足页面数量。
- 类型：以 50 个有实际职责的手写类、至少 10 个接口为开发目标；最低验收线分别为 45 类、7 接口。UI 与 Library 同口径统计，不因缺少显式访问修饰符而漏计；partial 按工程与完整类型名去重，类与接口分别计数，不把枚举和 record 类型另计入类以补足数量。v2 的 44 类门槛因漏计 Program 而更正。
- 课程未细化计数口径，保留参考与本项目的类型清单及分类依据，不能以人为拆分、空接口或重复类凑数。
- 源码压缩包只交付必要源码、配置及资源，最终大小不超过 10MB；按不超过 10,000,000 字节检查。
- 报告严格对应原模板，以 PDF 提交。姓名、学号、班级和截止日期资料未提供，不编造。

### 目录职责

| 目录 | 用途 |
|---|---|
| Code/ | 解决方案、UI、Library、单元测试及构建打包脚本 |
| docs/ | 来源记录、分析、规划、待确认信息、测试记录和报告草稿 |
| raw/ | 原始资料，只读 |
| assets/ | 已选定的长期复用素材；构建所需资源须在 Code 中有独立可交付版本 |
| memory-bank/ | 已确认方向及以后继续工作所需进度 |
| output/ | 验收后的源码压缩包、正式报告；不存放本次过程文档 |

## 2. 功能与页面

| 独立业务视图 | 功能与主要入口 |
|---|---|
| 阅读概览 | 在读书籍、近期记录、待完成计划及快捷入口；菜单首页 |
| 我的书库 | 按书名、作者搜索，按阅读状态筛选，分页；菜单入口 |
| 书籍详情 | 资料、当前进度、阅读记录、关联书摘和计划；从书库或概览进入 |
| 书籍编辑 | 新增、修改书名、作者、分类、总页数和简介；从书库或详情进入 |
| 书摘列表 | 按内容、书籍搜索，筛选收藏；菜单入口 |
| 书摘编辑 | 编辑摘录、个人感想、页码和收藏状态；从列表或书籍详情进入 |
| 阅读计划 | 查看进行中、已完成、已逾期的计划，删除计划；菜单入口 |
| 计划编辑 | 选择书籍、设置目标完成日期、展示建议每日页数；从计划列表或书籍详情进入 |
| 阅读统计 | 累计阅读分钟数、累计阅读页数、完成书籍数和近七日趋势；菜单入口 |
| 设置与数据 | 主题、演示数据载入、JSON 备份恢复；菜单入口 |

补充交互：

- 阅读记录在书籍详情弹窗新增、修改和删除，不另计为业务页面。
- 书籍删除前提示级联影响，确认后删除关联记录、书摘与计划；删除计划不影响阅读记录。
- 编辑未保存时，返回、切换菜单或关闭窗口均提供保存、放弃、取消离开选择；保存失败时留在编辑状态。
- 列表首次进入加载数据；详情返回列表时刷新受影响数据，同时保留可继续使用的搜索条件。
- 搜索空结果、首次使用、加载失败和保存失败有明确提示与重试或新增入口。
- 中文界面、侧边导航、统一表单和列表，支持浅色与深色主题。不引入大体积封面素材。

## 3. 工程、服务与导航

### 工程分层

建立 ReadJournal 桌面 UI、ReadJournal.Library、ReadJournal.UnitTest 三个工程。

- UI：Views、ViewLocator、ServiceLocator、导航实现、用户提示和文件选择的平台实现。
- Library：Models、ViewModels、服务接口、业务服务与 SQLite 实现，不依赖 Avalonia 控件类型。
- UnitTest：xUnit、Moq 与临时 SQLite 数据库，测试业务规则和真实存储行为。

### ServiceLocator 与 IoC：按老师源码实施

以参考项目 DailyPoetryA/ServiceLocator.cs 为结构基准，保留以下完整机制，不能只保留类名：

1. ServiceLocator 构造函数内创建局部 ServiceCollection，注册类型与接口映射。
2. 调用 serviceCollection.BuildServiceProvider() 构建真实 IoC 容器，用 private readonly IServiceProvider _serviceProvider 保存。
3. 服务和 ViewModel 由容器解析与构造，构造函数依赖由容器自动注入。禁止手动 new 全部服务和 ViewModel，再放进字典或属性模拟容器。
4. ServiceLocator 暴露各 ViewModel 属性，getter 使用 _serviceProvider.GetService<TViewModel>()，与老师示例一致。
5. 在 App.axaml 的 Application.Resources 中以 ServiceLocator 为资源键注册。静态 Current 从 Application.Current.TryGetResource 取得定位器并缓存，不能另建第二个容器。
6. ViewModel 通过构造函数注入接口；UI 工程中的导航实现按老师方式，在 NavigateTo 方法执行时通过 ServiceLocator.Current 取得目标和容器 ViewModel。

以下代码只是规划约束示意，省略了其余注册和 Current 实现，不是本轮应用代码：

    public class ServiceLocator {
        private readonly IServiceProvider _serviceProvider;

        public ServiceLocator() {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<MainViewModel>();
            serviceCollection.AddSingleton<BookDetailViewModel>();
            serviceCollection.AddSingleton<IMenuNavigationService, MenuNavigationService>();
            serviceCollection.AddSingleton<IBookStorage, BookStorage>();
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        public MainViewModel MainViewModel =>
            _serviceProvider.GetService<MainViewModel>();

        public BookDetailViewModel BookDetailViewModel =>
            _serviceProvider.GetService<BookDetailViewModel>();
    }

### 服务职责与生命周期

接口按实际职责划分：数据库初始化、书籍存储、阅读记录存储、书摘存储、阅读计划存储、偏好设置、阅读业务、统计、备份、提示、文件选择，以及根导航、菜单导航、内容导航，共 14 项服务职责。

- 按老师源码使用 AddSingleton 注册服务和全部 ViewModel，不再采用 v3 的瞬时页面 ViewModel。
- 单例业务页面接收新参数时重置相关数据、加载状态及编辑草稿；页面返回时根据实际修改刷新，不把首次初始化等同于每次进入。
- 共享数据库写入统一串行化，跨表修改使用事务。校验、错误处理和事务属于业务正确性措施，不据此替换老师架构。
- 撤销 v3 的 ShellViewModel、IMainNavigationHost、IPageFactory 设计，不引入自建容器、页面工厂或导航宿主替代老师机制。

### 三层导航与返回栈

| 组件 | 按老师示例确定的职责 |
|---|---|
| IRootNavigationService | NavigateTo(string view)，经定位器取得目标，替换 MainWindowViewModel.Content |
| IMenuNavigationService | NavigateTo(string view, object parameter = null)，取得单例目标、按需传参，调用 MainViewModel.SetMenuAndContent |
| IContentNavigationService | NavigateTo(string view, object parameter = null)，取得单例目标、按需传参，调用 MainViewModel.PushContent |
| MainViewModel | 持有 ContentStack、Content、菜单状态及菜单命令，维护 PushContent、SetMenuAndContent、GoBack |

- MainView 的运行时 DataContext 为 MainViewModel，直接绑定其内容、菜单状态和命令，不经过另一层壳 ViewModel。
- 菜单切换清空返回栈，内容导航压栈，GoBack 返回上一项，栈底返回不改变状态。栈归 MainViewModel，不归导航接口或服务。
- ViewModelBase 保留 public virtual void SetParameter(object parameter)。导航通过常量 switch 定位 ViewModel，在切换前调用 SetParameter；ViewModel 不解析导航来源或栈结构。
- 按真实参考源码，将 RootNavigationConstant、MenuNavigationConstant、ContentNavigationConstant 放在 Library/Services 内各自对应的导航接口文件中。业务调用统一使用常量，不能随意拼写目标字符串。
- 根常量为 InitializationView、MainView；菜单常量对应概览、书库、书摘列表、计划列表、统计、设置；内容常量对应书籍详情、书籍编辑、书摘编辑、计划编辑。

### 按示例避免构造循环

MainViewModel 像老师示例一样在构造函数中注入 IMenuNavigationService。导航服务不在构造函数中反向注入 MainViewModel、其他页面 ViewModel 或 ServiceLocator；只在 NavigateTo 执行时访问 ServiceLocator.Current 的属性。所有构造函数只保存依赖和建立命令，不触发导航或解析整套页面。这保留老师的调用方式，无需额外增加宿主接口或页面工厂。

### ViewLocator 与页面加载

- 按老师的 IDataTemplate 实现：匹配 ViewModelBase，根据完整类型名将 ViewModel 替换为 View，将 ReadJournal.Library 命名空间替换为 ReadJournal；使用 Type.GetType 和 Activator.CreateInstance 定位并创建视图。
- 视图按类型缓存，设置 DataContext，在 App.axaml 的 Application.DataTemplates 中注册。不增加 ShellViewModel 到 MainView 之类的特殊映射。
- XAML 事件绑定公开命令，code-behind 保持薄代码。数据加载由 ViewModel 的公开异步命令处理，文件、数据库和提示均经接口服务执行。
- 导航顺序与示例一致：定位单例目标 → SetParameter → 切换内容/压栈 → 页面 Loaded 命令异步加载。加载失败时保留目标页、重试入口和返回按钮；不采用 v3 的预加载成功后才提交栈。初始化失败留在初始化页重试。
- 业务路由采用列表 → 详情/编辑 → 返回；详情可进入自身编辑页、关联书摘编辑页或计划编辑页，不从详情直接打开另一份同类型详情。同一单例 ViewModel 不重复入栈；重复目标请求直接忽略且不修改原参数。不同书籍先返回书库再进入，不承诺同类详情的多份独立历史快照。
- 离开未保存编辑页前，由发起导航的公开命令完成确认，用户取消则不调用导航。保存后通过 MainViewModel.GoBack 返回并刷新受影响页面。异步命令阻止重复提交，使用 try/finally 收尾并在 UI 线程更新绑定状态。
- 检查命令绑定和导航目标映射，发现实际问题后记录具体位置并修正。修正错误不作为替换整套架构的理由。

### 架构验收

检查 ServiceLocator 是否真正调用 BuildServiceProvider、保存 IServiceProvider 并从容器解析；检查构造函数依赖是否自动注入、全部 VM/服务的单例注册、导航方法内的延迟定位、三类常量、MainViewModel 返回栈，以及 ViewLocator 的反射和缓存。不能以类名相同或接口数量足够代替这些实现检查。

## 4. 数据与业务规则

### 数据实体

| 实体 | 主要字段 |
|---|---|
| Book | ID、书名、作者、分类、总页数、简介、创建与修改时间 |
| ReadingRecord | ID、书籍 ID、阅读日期、起始页、结束页、分钟数 |
| Quote | ID、书籍 ID、摘录、感想、页码、收藏状态、创建与修改时间 |
| ReadingPlan | ID、书籍 ID、开始日期、目标完成日期 |

数据保存在用户本地应用数据目录，不写入安装目录或源码目录。SQLite 管理表结构版本；偏好设置由独立服务保存。搜索和统计针对完整数据集执行，不以当前已加载的一页数据计算。

### 页码、进度与统计

- 书名必填，总页数为正整数；书摘正文必填、必须关联书籍，页码可不填，填写时必须在 1 到总页数之间。
- 阅读记录使用实际页码的闭区间：`1 ≤ 起始页 ≤ 结束页 ≤ 总页数`，分钟数为正整数，阅读日期不能晚于用户本地当天。
- 单条阅读页数 = `结束页 - 起始页 + 1`。第 10～20 页计 11 页，第 10～10 页计 1 页。
- 无记录时当前进度为 0；有记录时取最大结束页。0 是内部进度值，不是用户可输入的实际页码。
- 默认新增起始页为当前进度加 1；已读完时提示进入重读输入，要求选择合法页段，不生成“总页数加 1”的记录。
- 允许重复或重叠阅读。累计阅读页数按各记录闭区间页数求和，含重读，可以超过书籍总页数；当前进度不累加，最高为 100%。界面明确使用“累计阅读页数”和“当前阅读进度”两个名称。
- 无记录为未开始；有记录但未到最后一页为在读；最大结束页达到总页数为已完成。完成只表示记录到书末，不另行推断未记录页段是否读过。
- 完成日期为有效记录中结束页等于总页数的最早阅读日期；完成书籍数每本只计一次，近七日按本地阅读日期分组。
- 修改或删除记录后重新计算进度、完成日期、计划和统计；修改总页数同样触发重算。总页数不得小于已有记录或书摘中的最大页码。

### 阅读计划

- 每本书最多一个未完成计划；新建计划开始日期为当天，目标日期不得早于当天，不给已经完成的书新建普通阅读计划。
- 目标为读至全书最后一页。未完成且当天晚于目标日期则逾期；达到总页数则完成。
- 未逾期时建议每日页数为剩余页数除以含当天的剩余天数后向上取整；当天到期按剩余页数展示。完成后为 0，逾期显示逾期及剩余页数，不除以 0 或负天数。

### 事务、备份与演示数据

- 删除关联数据、导入演示数据、恢复备份等跨表操作使用事务。
- 备份采用带格式版本的 JSON，包含四类业务实体；主题等偏好不进入业务备份，恢复后保留当前偏好。
- 恢复先校验格式版本、字段、ID 唯一性、关联关系和业务规则；校验通过后成功写出当前数据备份，再用事务替换业务数据。自动备份失败则不开始替换；替换失败则回滚，刷新页面只在成功后发生。
- 演示数据使用明确标注的虚构资料，由用户主动载入；仅允许在空业务库载入，避免与已有数据混合或重复导入。

## 5. 实施顺序与测试验收

| 阶段 | 工作 | 通过条件 |
|---|---|---|
| 1 资料与设计 | 来源、需求对应、页面流转、实体关系、类型职责清单 | 作业约束和本规划规则可逐项追溯 |
| 2 基础骨架 | 三工程、ServiceLocator/IoC、ViewLocator、三层导航、三类导航常量、数据库初始化 | 真实容器解析与示例调用链检查通过，无构造循环；首次及再次启动可进入主界面，失败可重试 |
| 3 核心闭环 | 书库、书籍编辑与详情、阅读记录、书摘 | 可完成录入到记录书摘流程，重启后数据保留 |
| 4 扩展闭环 | 计划、统计、主题、演示数据、备份恢复 | 修改数据后各页一致，失败时原数据完整 |
| 5 交付 | 自动化测试、真实 UI 操作、截图、报告与源码包 | 章节、证据、类型数量、页面数和包体积全部通过 |

推进方式：每阶段先完成可运行的纵向业务流程，再进入下一阶段；测试随规则和模块实现同步完成，不留到最后集中补写。阶段 1 只记录必要职责和页面，不预建 50 个空类；阶段 3/4 闭环完成后统计实际类型、页面及职责，阶段 5 再作最终核验。若数量不足，优先检查尚未实现的业务和合理分层；若仍低于课程门槛，记录真实差额并调整有实际意义的工作范围，不能宣布达标或添加占位类型。

测试至少覆盖：

- 验证 BuildServiceProvider 生成真实容器；从定位器多次获取同类 VM 为同一实例，构造依赖自动注入；MainViewModel 构造注入菜单服务，导航服务在方法执行时定位主 VM，不发生构造循环。
- 单页阅读、第一页、最后一页、整本阅读；0 页、逆序页码、超总页数、非正分钟数及未来日期均拒绝。
- 第 10～20 页计 11 页；重复该记录累计 22 页，当前进度仍为 20。
- 增删改记录、修改总页数后进度、完成日期、计划状态及统计一致；删除完成记录可使书籍回到在读。
- 计划当天到期、已逾期、已完成，以及建议页数分母边界。
- 搜索、分页、书摘收藏，书籍删除后的关联完整性；真实 SQLite 事务失败回滚。
- 按书库 → 书籍 A 详情 → 返回书库 → 书籍 B 详情操作，单例页面显示 B 且无 A 的过期数据；同实例不重复入栈，栈底返回、菜单重置、加载失败重试、重复点击和未保存离开流程正确。
- 无效 JSON、格式版本不支持、重复 ID、孤立引用、非法页码及恢复失败不破坏原数据；恢复往返后业务数据一致。
- 首次启动、再次启动、初始化失败重试，以及离线环境下所有核心业务可用。

自动化测试不能替代 XAML 绑定和实际 UI 验证；必须另行运行应用，保存关键界面和测试证据。实施前不填造成功结果。

## 6. 源码包白名单与报告交付

### 源码包

从 Code 内明确列举的文件建立交付清单，复制到临时打包目录后压缩；不先压缩整个工作区再排除。压缩包根目录直接包含解决方案文件与三个工程目录，不嵌套整个规划工作区。

允许进入清单的只有：

1. 解决方案及工程文件、构建实际使用的配置，如必要的 props、targets、NuGet 配置和固定依赖文件；检查不含本机绝对路径和凭据。
2. UI、Library 和测试工程的手写源码、XAML、必要 manifest 及测试配置。
3. 工程明确引用的必要资源，例如图标、演示 JSON；不通过扫描所有同扩展名文件来推定资源。
4. 可重现还原、构建和打包所需的源码脚本；脚本不得依赖 Code 外的资料目录。

bin、obj、.git、IDE 状态、缓存、TestResults、覆盖率、运行数据库及日志均禁止入包。docs、raw、memory-bank、assets 和 output 不能作为目录整体打包；需要的应用资源须已在 Code 内纳入清单。

归档前后检查清单和内容，验证大小 ≤ 10,000,000 字节。解压到全新临时目录，使用独立运行数据目录执行依赖还原、构建、测试及启动验证；记录是否使用网络或 NuGet 缓存，不能把“运行离线”误写成“首次还原不需要依赖来源”。验证产生的编译结果不回填源码压缩包。

### 报告

严格按 `docs/report-checklist.md` 的完整小节清单编写：封面、项目背景与意义、1.1～1.2、2.1～2.5、3.1～3.3、4.1～4.3、5 总结与展望。

设计图、界面截图、测试截图和结果来自实际实现；报告中的类数量引用最终源码清单。项目实现和测试未完成前，草稿标注为计划或尚未验证，不能写成已通过。

正式文件在 output 中使用日期、主题和版本命名；首次实际交付为 v1，文件名日期使用实际交付日期，与本规划版本 v4 分开管理。PDF 生成后逐页检查章节、图表、分页和中文显示，再交付。


## 2026-10-22 电子书与 AI 扩展的执行入口

现有 v1 阅读管理交付继续保留。后续电子书扩展以 `output/reader-plan-v2/20261022-阅记-原生阅读器与AI助手-完整实施规划-v2.md` 及同目录接口/验收表为依据，按 P0～P10 推进。本条仅新增扩展规划入口，不把历史阅读管理规划改写为已经支持电子书。本轮交付规划，开发阶段均未开始；真实 DeepSeek API 在 P9 前通知用户。

执行进度更新（2026-10-22）：用户已授权开始并逐阶段推进，当前状态以 docs/reader-execution.md 为准。P0 四项验收通过（Linux），实现提交 fef2575；旧库/JSON fixtures、样本生成器、96项测试及隔离真实窗口证据见 docs/evidence/reader/P0/acceptance.md。P1 尚未开始；Windows 字体/DPI 条件挂起，真实 API 仍至 P9 前通知。

## 2026-10-22 当前执行进度（优先于历史未开始状态）

P0 已按用户“推送后继续p1”推送 feature/reader-native，远端 8465e49 已核验。P1 独立原生分页实验通过，代码/证据提交 bd6eb50；实验14项、生产96项与Linux三档实际缩放通过，验收见 docs/evidence/reader/P1/acceptance.md。用户明确“你可以用linux，windows保留”，Windows 补验保留未测、不阻塞当前阶段。P1 本地提交未推送；P2 下一阶段，尚未开始。原始资料、Code 与正式交付未因 P1 改动。

## 2026-10-22 P2 当前进度（优先于前条）

用户“推送，然后开始p2”已执行：P1远端feature/reader-native=4b336d3；P2实现/证据本地9b24b07，七项通过。工作区与白名单独立源码各124项通过，4次实际窗口启动/正常关闭，旧字段无损、迁移回滚/锁/代际通过。详见docs/evidence/reader/P2/acceptance.md和reader-p2-contract-addendum.md。P3尚未开始，P2未再次推送；导入与Reader入口关闭，Windows保留未测。


## 2026-10-22 P3 当前进度（优先于前条）

按用户“推送，开始p3”，P2已推送feature/reader-native=8696045。P3实现/证据本地17690bb，八项Linux验收通过：完整ZIP及旧JSON输入、完整自动备份、安全校验、持久日志/目录替换/SQL提交、启动对账与上下文失效。最终工作区及159文件独立源码各169项通过，四点真实进程中断与损坏日志保留、最终桌面恢复/重开均通过，见docs/evidence/reader/P3/acceptance.md。P3未推送，P4未开始；Windows保留未测，真实API仍至P9前通知，原始资料/正式交付不改。
