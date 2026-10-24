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


## 2026-10-22 P4及后续授权（优先于历史状态）

P3已推送16e4155；P4八项Linux通过，实现5358a4a，工作区/独立源码各217项、10处真实进程中断、实际TXT与桌面完整往返及Missing均通过。阶段提交后推送feature/reader-native，见docs/evidence/reader/P4/acceptance.md。用户追加授权顺序完成P5/P6/P7且每phase完成后推送；不再停在P4，仍以逐阶段验收为前提。当前P5尚未开始，Windows保留，真实API仍到P9前通知。


## 2026-10-22 P5阶段结果（优先于历史状态）

P4已推送并回读69b783abab2dfa02984121fa878da2fd6c74d692。P5实现正式原生Reader/首屏开始会话/SessionId幂等事务关闭/CloseFailed重试继续、所有运行期异步离开门禁、正文锚点重排/偏好/可取消索引。验收见docs/evidence/reader/P5/acceptance.md；接口补充见docs/reader-p5-protocol.md。Linux工作区与独立白名单源码完整回归，真实桌面两次正常关闭、偏移442重启一致、字号28保留；20次切书布局每次清零。P5完成提交后按授权正常推送并回读，再开始P6，随后P7，每阶段均验收/推送。Windows仍保留未测，无API；raw/output/参考/冻结fixtures保持。


## 2026-10-23 P6当前结果（优先于历史状态）

P5已推送af7ebb60bb65498628cd3a9631a82b92de7701c2。P6原子定位书摘/按范围去重/保留原文与精确回跳、统一历史/毫秒统计图、完成与原子重置已实现。工作区完整243项通过；独立181文件白名单源码重验，真实桌面M1闭环10张业务表/4个受管文件与来源SHA一致、续读偏移294，续接桌面两次正常关闭。详见docs/evidence/reader/P6/acceptance.md与reader-p6-protocol.md。验收提交后正常推送并核对真实远端，然后按用户授权继续P7；Windows保留未测，无API，raw/output/参考/冻结fixtures不变。


## 2026-10-23 P7完成验收（优先于历史状态）

P6已推送并回读34f07e46c7ca56b159ea1e689604836fc1d1992a。P7锁定VersOne.Epub3.3.6/Unlicense与AngleSharp1.8.5/MIT，实现安全有界EPUB适配、spine/嵌套目录/稳定键、原生基本样式/图片页/亲和锚点/纯文本抽取，复用P5/P6会话与书摘闭环。工作区272项通过；独立186文件白名单源码再验。11个确定性支持/降级变体逐本完成会话/书摘/完整ZIP删除恢复回跳，15类异常均拒绝且来源不变；真实Linux窗口两次正常关闭、大图2400×1800缩放、偏移669/b-17图片续读、28字号书摘高亮通过。

验收：docs/evidence/reader/P7/acceptance.md；契约边界：docs/reader-p7-protocol.md；逐本兼容表：docs/evidence/reader/P7/compatibility.json。真实书籍资料未提供（覆盖0），图片实测PNG，其他图像格式未逐类样本验收，Windows保留未测，不推断市场兼容率；无真实内容API。原始资料/正式交付/参考/冻结fixtures保持。最终提交后正常推送feature/reader-native并回读；P4/P5/P6此前均已按阶段推送，用户本轮授权到P7止，P8以后未开始。

## 2026-10-23 P8完成（优先于历史未开始状态）

用户授权“继续p8，完成后推送远端”。从main=cd3386b建立feature/reader-ai-p8；完成当前页/选区/页范围冻结快照、默认本地Mock及有界DeepSeek HTTP适配、精确原句核验、原子批次/不可变回执/本地重试/安全撤销、AI来源与设置/隐藏会话密钥/取消及旧响应隔离。P8-01～10验收见docs/evidence/reader/P8/acceptance.md，协议见docs/reader-p8-protocol.md。

Linux工作区与200文件白名单独立源码Release各327/327，含55项新增；真实窗口分析不写库、自动保存、AI来源、收藏保留/未改项删除、原文高亮与全书1～2页379字符Mock分析通过。凭据仅会话内存，未请求真实API。原始资料/正式交付/参考/冻结fixtures共107文件保持。完成后按本轮授权提交并正常推送该功能分支，远端SHA以实际回读为准；P9/P10未开始，P8新增Windows实机保留，不自动合并main或重做正式交付。

## 2026-10-23 UI/UX 全面修复与现代化实施计划（ui-ux-pro-max 专项）

为彻底解决全面走查发现的 16 项视觉与交互缺陷，按“底座画刷先行 → 核心导航升级 → 关键页面分治 → 无障碍与全量回归”的严谨节奏，制定 6 个阶段修复计划。各阶段均以“不破坏现有 MVVM 导航机制、不破坏现有数据与业务层、保证全量单元测试 100% 通过”为铁律。

### Phase 1: 视觉基础与动态主题画刷体系（Tokens & ThemeDictionaries）
- **目标**：彻底消除硬编码色彩，实现真正的深色/浅色模式平滑切换，统一 8px 空间律动。
- **任务**：
  1. 在 `App.axaml` 中声明 `<ResourceDictionary.ThemeDictionaries>`，建立 `Light` 与 `Dark` 双套语义画刷资源：
     - `AppBackgroundBrush`（页面底色：浅暖纸 `#FAF8F5` / 夜读深灰 `#161C1A`）
     - `AppSurfaceBrush`（卡片与容器底色：纯白 `#FFFFFF` / 深色卡片 `#202926`）
     - `AppSurfaceHoverBrush`（悬停微光底色）
     - `AppBorderBrush`（边框线：浅色 `#E5E0D8` / 深色 `#2D3A36`，替换硬编码 `#8094A49C`）
     - `AppTextPrimaryBrush`、`AppTextSecondaryBrush`（满足 WCAG 4.5:1 对比度）
     - `AppAccentBrush`、`AppAccentHoverBrush`（翡翠墨绿 `#246650` 品牌主色）
     - `AppDestructiveBrush`（危险操作红 `#DC2626`）
  2. 规范全局排版样式：`.h1` (26px), `.h2` (18px), `.h3` (15px), `.caption` (12px muted)。
  3. 规范控件基础圆角（`CornerRadius="8"`）、按钮高度与内边距。

### Phase 2: 全局导航与应用外壳翻新（App Shell & Navigation）
- **目标**：赋予侧边栏清晰的当前位置指示与专业矢量图标，重构顶部标题栏。
- **任务**：
  1. 侧边栏按钮实现当前激活态（Active State）高亮反馈：当前所在模块呈现轻微半透明翡翠底色与加粗字重，未选中项保持静默。
  2. 在 `App.axaml` 资源字典中引入 6 个轻量级原生矢量 `PathIcon`（概览、书库、计划、书摘、统计、设置），侧边栏按钮升级为“矢量图标 + 文字”专业桌面布局。
  3. 顶部导航栏美化：返回按钮改造为微圆角胶囊按钮并集成左箭头矢量图标，与模块标题保持优雅呼吸间距。

### Phase 3: 书库列表与卡片体验重构（Bookshelf & Card Elevation）
- **目标**：解决工具栏横向溢出问题，提升卡片物理质感与信息表达度，补全空态引导。
- **任务**：
  1. 改造 `BookListView.axaml` 顶部导入操作栏：将“TXT/EPUB导入”、“扫描收件区”、“收件目录”进行自适应折行或清晰主次分组，确保在 900px 最小窗口及高 DPI 下永不横向出界。
  2. 书籍卡片实体化：卡片赋予 `AppSurfaceBrush` 背景、悬停变色提升反馈、增加图书格式胶囊（TXT / EPUB / 手动）和阅读状态标签。
  3. 重塑空态展示：居中展示书籍矢量插画、友好的引导说明文案，并提供直接唤起“＋ 添加书籍”或“导入书籍”的 Primary CTA 按钮。

### Phase 4: 原生阅读器与 AI 助手交互升级（Reader & AI Panel）
- **目标**：告别生硬的手输与英文枚举，提升阅读专注度与 AI 交互感知。
- **任务**：
  1. 排版工具栏优化：字体选择改为常用高品质中文字体下拉列表（如微软雅黑、思源黑体、楷体等），配色选项替换为中文友好显示（“羊皮纸”、“晨曦浅色”、“夜间深色”），字号增加 `A-` / `A+` 快捷调整。
  2. 目录栏优化：将目录栏最小宽度调优并支持章节名称完整自适应，高亮当前阅读章节。
  3. AI 助手动效：当 `IsAiBusy` 时，展示 Indeterminate 进度动画与清晰的“正在阅读理解与提炼…”状态，结果卡片化展示。

### Phase 5: 书籍详情、表单与统计打磨（Details, Forms & Statistics）
- **目标**：强化视觉操作层级，防范危险误触，提升表单可用性与数据图表美观度。
- **任务**：
  1. `BookDetailView.axaml` 区分操作层级：“开始 / 继续阅读”设为最大尺寸的 Primary 突出按钮；“编辑”、“＋ 记录”等作为次级按钮；“删除书籍”赋予明显的 Destructive 危险红框提示，防止误触。
  2. 将阅读历史、计划、书摘等堆叠信息进行结构化视觉卡片分区。
  3. 各编辑表单（`*EditView`）垂直间距放宽至 16px，必填项星号着色区分，未保存修改状态增加柔和点缀。
  4. 统计页面（`StatisticsView`）图表双进度条赋予协调的品牌色与高质感圆角。

### Phase 6: 无障碍适配、主题验证与全套回归（A11y & Verification）
- **目标**：实现 WCAG 2.2 AA 无障碍合规，全平台视觉走查，保证工程 100% 绿色构建。
- **任务**：
  1. 为所有独立图标按钮与核心控件添加 `AutomationProperties.Name`。
  2. 针对浅色与深色模式分别进行实际窗口渲染检查与对比度验证。
  3. 运行全量单元测试（334 项），验证全部通过（0 失败，0 跳过），确保架构、导航栈、数据存储及已有业务逻辑零回归。


## 2026-10-24 本轮
用户授权拉取更新、测试全部既定功能。执行 docs/recheck-20261024-plan.md；保留本地未提交修改，不自动提交/推送，不覆盖旧交付。

本轮已完成全部复验工作，范围结论及未测项见docs/recheck-20261024.md。未提交/推送，未覆盖旧output。

## 2026-10-24 UI/UX 重构美化实施规划（基于 `82070fa` 与真机截图复核升级版）

基于 2026-10-24 最新 `main`（`82070fa`）代码与 Windows 150% DPI 真机截图审查结论，将 UI 重构美化规划升级为以下 **6 个阶段（Phases 1～6）**。全程严格遵守“不破坏现有 MVVM 架构与 334 项单元测试 / 152 项探针契约”红线：

### Phase 1：设计令牌、动态主题画刷与全局控件基线（`App.axaml`）
1. **构建 `ThemeDictionaries`（`Light` / `Dark` 双套温润书卷语义画刷）**：
   - `AppBackgroundBrush`（暖白宣纸 `#FAF8F5` / 夜读深墨 `#141A18`）
   - `AppSurfaceBrush`（纯白卡片 `#FFFFFF` / 深色卡片 `#1E2724`）
   - `AppSurfaceMutedBrush`（次级容器 `#F3EFEA` / `#25302C`）
   - `AppBorderBrush`（柔和边框 `#E4DFD7` / `#2E3B37`，全面替换 `#8094A49C`）
   - `AppTextPrimaryBrush`（`#1C2421` / `#ECF2EF`）、`AppTextSecondaryBrush`（`#5A6963` / `#9BA8A3`）
   - `AppAccentBrush`（品牌翡翠绿 `#246650` / `#348F70`）、`AppAccentSoftBrush`（激活底色 `#18246650` / `#28348F70`）
   - `AppDangerBrush`（危险红 `#DC2626` / `#F87171`）、`AppDangerSoftBrush`（`#15DC2626`）
   - `AppSidebarBgBrush`、`AppSidebarActiveBgBrush`、`AppHeaderBgBrush`
2. **统一全局控件尺寸与样式类**：
   - 补齐 `ComboBox`、`DatePicker` 的 `MinHeight="40"` 与 `VerticalContentAlignment="Center"`，彻底消除工具栏 `40px` vs `32px` 高度错位。
   - 完善按钮样式体系：`Button.primary`（翡翠主色实心）、`Button.secondary`（浅底描边）、`Button.ghost`（透明悬停微光）、`Button.danger`（红色描边/警示）、`Button.card-action`（卡片化可点击列表项，消除 `OverviewView` 1000px 灰条）。
   - 规范统一卡片样式 `Border.card`（`Background="{DynamicResource AppSurfaceBrush}"`、`BorderBrush="{DynamicResource AppBorderBrush}"`、`BorderThickness="1"`、`CornerRadius="12"`、`BoxShadow="0 2 8 0 #0D000000"`）。

### Phase 2：导航外壳、侧边栏激活态与顶部栏去重（`MainView` & `MainViewModel`）
1. **侧边栏激活态高亮与 Lucide 线性矢量图标**：
   - 在 `MenuItem.cs` 增加轻量级 `IsSelected` 通知属性（或图标 Geometry 键），在 `MainViewModel` 导航切换时同步更新，使侧边栏当前页面高亮显示左侧翡翠指示条 + 半透明高亮底色 + 加粗文字，并配齐 6 个模块的 `StreamGeometry` 矢量图标。
2. **消除“双重标题冗余”与优化返回栏**：
   - 重构 `MainView.axaml` 顶部栏与各页面头部关系：一级页面统一展现主标题与副标题说明，消除相距 40px 的上下重复标题；二级页面（详情、编辑、阅读器）清晰展示 `[← 返回]` 胶囊按钮（**严格保留 `Content="← 返回"` 字符串以通过 `ArchitectureTests.cs:93`**）与当前上下文区域。

### Phase 3：原生阅读器与 AI 助手沉浸式重构（`ReaderView.axaml` & `ReaderSurface.cs`）
1. **`ReaderSurface.cs` 选区高亮与画布质感升级**：
   - 将 `ReaderSurface.cs:66` 刺眼的 `Brushes.LightSeaGreen` 替换为柔和半透明暖琥珀/翡翠选区高亮画刷（`#45D97706` 或根据 `Palette` 自适应半透明画刷），确保黑字/白字清晰可读。
   - 为阅读器画布外层增加带圆角、细边框与柔和阴影的书页容器，并根据阅读器当前配色（`paper` / `light` / `dark`）协调外围包裹背景。
2. **阅读器工具栏、目录栏与 AI 面板美化**：
   - 字体设置提供预设中文字体快捷选择（同时兼容自定义字体名），配色下拉框保持底层 `"paper" | "light" | "dark"` 契约值的同时提供直观视觉反馈。
   - 左侧目录（TOC）按钮改为左对齐、悬停高亮的清爽章节列表项，去除笨重水泥灰块。
   - 右侧 AI 助手面板分区卡片化：主操作 `生成并自动保存书摘` 设为 `Classes="primary"`（**严格保留 `Content="生成并自动保存书摘"` 字符串以通过 `AiViewModelTests.cs:57`**），`IsAiBusy` 时显示加载进度条，结果与批次状态使用精致表面卡片展示。

### Phase 4：仪表盘、我的书库、详情页与统计图表重塑（`Overview`, `BookList`, `BookDetail`, `Statistics`）
1. **`OverviewView` 仪表盘现代卡片化**：
   - KPI 指标卡增加左侧色条或微图标点缀；“正在阅读”列表从 1000px 满宽灰按钮改为 `Button.card-action` 白底圆角卡片（含书名、进度徽章与右侧箭头）；“近期记录”与“待完成计划”统一纳入结构化列表项；底部无意义的常驻“刷新 / 重试”按钮改为右上角轻量操作或仅在出错时显现。
2. **`BookListView` 响应式工具栏与高质感书籍卡片**：
   - 顶部“筛选搜索栏”与“导入/收件区工具栏”分为清晰的主次两栏（或 `WrapPanel` 自适应），彻底解决窄窗口挤压溢出；`＋ 添加书籍` 设为 `Classes="primary"`。
   - 书籍卡片升级为白底圆角带悬停阴影提升感，为 TXT / EPUB / 手动书籍提供差异化色彩徽章（Badge）。
3. **`BookDetailView` 与 `StatisticsView` 视觉层级重塑**：
   - `BookDetailView` 中 `开始 / 继续阅读` 设为醒目的 `Button.primary`，`编辑`/`＋ 记录`/`＋ 书摘`/`＋ 计划` 设为 `Button.secondary`，`删除书籍` 设为 `Button.danger` 并与主操作拉出安全间距。
   - `StatisticsView` 图表进度条赋予柔和圆角与高辨识度双色对比，修复底部按钮裁切问题。
   - 为书库、书摘、计划、统计等列表补齐带矢量图标的居中优雅空态（Empty State）。

### Phase 5：表单左对齐修复与模态弹窗美化（`*EditView`, `SettingsView`, `AlertService.cs`）
1. **修复表单居中漂移问题**：
   - 为 `SettingsView`、`BookEditView`、`QuoteEditView`、`ReadingPlanEditView` 内的 `MaxWidth` 容器统一补充 `HorizontalAlignment="Left"`（或合理的网格卡片布局），使表单与页面标题左边缘完美对齐。
   - `SettingsView` 拆分为“外观主题”、“AI 助手配置（**严格保留 `Name="ApiKeyBox" PasswordChar="●"`**）”、“数据备份与恢复”三张独立设置卡片。
2. **美化模态弹窗与记录窗口（`AlertService.cs` & `ReadingRecordEditView.axaml`）**：
   - 优化 `AlertService.cs` 动态弹窗的卡片边距、圆角与主次按钮样式（确认按钮使用 `primary`，危险确认使用 `danger`），并在 `ReadingRecordEditView` 底部操作栏贴心呈现“保存记录”（`primary`）主按钮。

### Phase 6：无障碍适配、真机截图复验与 334 项单测 / 152 项探针全量回归
1. 补齐图标与核心交互控件的 `AutomationProperties.Name`。
2. 运行 `dotnet test Code/ReadJournal.sln -c Release` 确保 334 项测试零回归（332 通过 / 0 失败 / 2 权限跳过），运行 `ReaderP10Probe` 确保 152 项生产检查 100% 通过。
3. 启动 Windows 桌面端实际渲染并截图对比，验证浅色/深色主题、阅读器选区高亮与各页面布局效果。

## 2026-10-24 UI/UX 重构美化逐阶段执行与验收结果（已完成）

按用户指令“开始执行，每完成一个阶段就验收一次”，已顺序完成全部 6 个阶段的实施与逐阶段测试验收：

- **Phase 1 验收（通过）**：在 `Code/ReadJournal/App.axaml` 建立 `Light` / `Dark` 双套 `ThemeDictionaries`、6 枚矢量 `StreamGeometry` 图标、统一 `40px` 高度基线（`TextBox` / `ComboBox` / `DatePicker`）以及 `.primary` / `.secondary` / `.danger` / `.card-action` / `.toc-item` / `Border.card` 样式体系。全套 334 项测试（332 通过 / 0 失败 / 2 权限跳过）通过。
- **Phase 2 验收（通过）**：更新 `MenuItem.cs`、`MainViewModel.cs`、`MainView.axaml` 与 `InitializationView.axaml`，实现侧边栏当前模块翡翠指示条 + 矢量图标激活态高亮（保留 `AutomationProperties.Name`），并在一级页面隐藏冗余顶部栏、二级页面展示 `[← 返回]` 导航栏，彻底根治“双重标题冗余”。29/29 项导航与架构测试全绿。
- **Phase 3 验收（通过）**：更新 `ReaderSurface.cs` 与 `ReaderView.axaml`，将刺眼的 100% 不透明 `Brushes.LightSeaGreen` 选区高亮替换为柔和半透明圆角高亮画刷（浅色 `#40246650` / 深色 `#66389B7A`）并绘制精致书页内边框；章节目录改为清爽 `toc-item` 列表，AI 面板与排版栏卡片化且严格保留 `Content="生成并自动保存书摘"` 及 `Edit` 索引顺序。32/32 项阅读器、EPUB 与 AI 视图测试全绿。
- **Phase 4 验收（通过）**：更新 `BookProgress.cs`（新增 `KindLabel` 格式标签）及 `OverviewView.axaml`、`BookListView.axaml`、`BookDetailView.axaml`、`QuoteListView.axaml`、`ReadingPlanListView.axaml`、`StatisticsView.axaml`。消除概览页 1000px 水泥灰按钮条与统计页底部按钮裁切，补齐带矢量图标的优雅空态与卡片层级。49/49 项业务与视图测试全绿。
- **Phase 5 验收（通过）**：更新 `BookEditView.axaml`、`QuoteEditView.axaml`、`ReadingPlanEditView.axaml`、`ReadingRecordEditView.axaml`、`SettingsView.axaml` 与 `AlertService.cs`，统一 `HorizontalAlignment="Left"` 解决窄表单居中漂移，将设置页拆分为 4 张主题卡片（严格保留 `Name="ApiKeyBox" PasswordChar="●"`），并为弹窗按钮赋予主次与危险警示层级。48/48 项编辑、设置与导航测试全绿。
- **Phase 6 验收（通过）**：
  - 全量单元测试 `dotnet test Code/ReadJournal.sln -c Release`：**334 项总计，332 项通过，0 项失败，2 项符号链接权限跳过**。
  - 生产架构与性能探针 `ReaderP10Probe`：**152/152 项检查全部通过**（`firstOpenMs: 542.7ms <= 2000ms`，`flipP95Ms: 7.58ms <= 100ms`，`reflowP95Ms: 128.64ms <= 500ms`，20 轮切书零内存/句柄泄漏）。
  - 渲染截图证据保存于 `docs/evidence/ui-refactor-20261024/`。


## 2026-10-24 第二轮 UI/UX 深度打磨与存量缺陷修复计划（基于 `70d46a8` 复检）

针对第二轮像素级审查与多状态 Skia 渲染发现的 **15 项存量 UI/UX 缺陷**，制定以下 **5 个阶段（Phases 1～5）** 的精准修复计划。全程严格遵守架构与测试门禁红线（不破坏现有 MVVM 与 DI 结构、保留所有单测与 UIAutomation 探针断言的控件名称与文本，确保 334 项单测与 152 项探针 100% 通过）：

### Phase 1：字体抗粘连、深色模式高对比度 Token 与完整按钮伪类状态（`App.axaml`）
1. **消除高 DPI 合成假粗体粘连**：
   - 将 `Window` 的 `FontFamily` 调整为 `Microsoft YaHei UI, Microsoft YaHei, PingFang SC, Noto Sans CJK SC, Segoe UI, Arial`（优先匹配带独立 Bold 字重表、ClearType 渲染清晰的原生 UI 字体）。
   - 收敛小字号字重：将 `.badge-text`、`.badge-amber-text`、普通按钮及侧边栏菜单项的 `FontWeight` 控制在 `Normal` / `Medium`，仅在 18px+ 标题使用 `SemiBold` / `Bold`。
2. **修复深色模式对比度（WCAG AA ≥ 4.5:1）**：
   - 在 `Light` / `Dark` 主题字典中新增专用高对比度前景与轨道画刷：
     - `AppBadgeTextBrush`（浅色 `#1D5341` / 深色 `#6EE7B7`）
     - `AppBadgeAmberTextBrush`（浅色 `#B45309` / 深色 `#FBBF24`）
     - `AppProgressTrackBrush`（浅色 `#286B7D75` / 深色 `#454A5D55`）
   - 将 `StatisticsView.axaml:49` 硬编码的 `#D97706` 替换为 `{DynamicResource AppWarningBrush}`。
3. **补全控件全生命周期状态样式（`:pointerover` / `:pressed` / `:disabled`）与品牌色 `CheckBox`**：
   - 为 `Button`、`Button.primary`、`Button.secondary`、`Button.danger`、`Button.card-action`、`Button.toc-item`、`Button.ghost`、`Border.nav Button` 补齐 `:pointerover`、`:pressed` 与 `:disabled` 下的 `ContentPresenter#PART_ContentPresenter` 样式，杜绝点击闪灰或禁用态变成刺眼水泥块。
   - 覆写 Fluent `CheckBox` 选中态资源画刷（`CheckBoxCheckBackgroundFillChecked*`），使其与翡翠绿品牌主色一致。

### Phase 2：消除全站“幽灵空行”与补齐卡片空状态（`OverviewView`, `BookListView`, `BookDetailView` 等 12 个视图）
1. **折叠所有空字符串提示行**：
   - 为全站 12 个视图中的 `Error`、`OperationMessage`、`Status`、`Notice`、`BatchStatus` 绑定统一添加 `IsVisible="{Binding ..., Converter={x:Static StringConverters.IsNotNullOrEmpty}}"`，并在 `Error` 非空时套用 `Border.error-banner` 警示样式，彻底消除无报错时的 20px～36px 空白断层。
2. **补齐 `OverviewView` 与 `BookDetailView` 内部空态引导**：
   - 在 `OverviewView` 的“正在阅读”、“近期记录”、“待完成计划”卡片中增加空列表温和提示（如“暂无正在阅读的书籍，点击上方前往书库开启阅读吧”），且仅当有在读书籍时才显示“点击卡片进入详情”徽章。
   - 在 `BookDetailView` 的“阅读历史”、“关联计划”、“关联书摘”卡片中增加空状态提示，并在无历史记录时隐藏底部分页按钮。
   - 在 `BookListView` 空态（`IsEmpty == true`）时隐藏底部分页栏；当 `Book.Author` 为空时折叠作者行（或显示“未知作者”）。

### Phase 3：原生阅读器与三栏布局精细化调优（`ReaderView.axaml`, `ReaderSurface.cs`, `MainView.axaml`）
1. **解决章节目录截断与优化画布呼吸感**：
   - 将 `ReaderView` 三栏宽度调整为 `196,*,296`，为目录项 `Button.toc-item` 内部 `TextBlock` 配置 `TextTrimming="CharacterEllipsis"` 与 `ToolTip.Tip="{Binding Title}"`。
   - 将阅读画布外框 `Padding` 从 `2` 提升至 `10`，展现精致书页留白边框。
2. **优化排版栏与右侧 AI 助手面板**：
   - 为 `Palette` 下拉框保持底层 `"paper" | "light" | "dark"` 值的同时，增加友好提示或显示模板；**严格保留 `Edit` 控件顺序（字体 `index=0`、字号 `index=1`、全书起始页 `index=2`、全书结束页 `index=3`、AI 结果框 `index=4`）** 以 100% 兼容现有 UI 自动化脚本。
   - 为 `AiResult` 增加 `Watermark="AI 分析、解释或提取结果将展示在此处…"`，紧凑化右侧面板垂直间距，避免在小窗口下出现双滚动条挤压。
   - 适度收紧 `MainView` 二级返回栏与 `ReaderView` 上下栏垂直内边距，将更多垂直空间留给正文画布。

### Phase 4：书摘回跳逻辑呈现、设置页首屏防裁切与记录弹窗优化（`QuoteListView`, `QuoteEditView`, `SettingsView`, `ReadingRecordEditView`, `AlertService`）
1. **精准显示书摘“回到原文”与“来源原文”**：
   - 在 `QuoteItem` 新增 `CanOpenSource => Source != Reader.QuoteSource.Manual;`，在 `QuoteListView` 与 `BookDetailView` 中为 `回到原文` 按钮绑定 `IsVisible="{Binding CanOpenSource}"`（手动书摘不再显示无效的“回到原文”按钮，电子书选区与 AI 书摘正常显示，且确保 `ui-03-epub-mock.py` 中 `click('回到原文')` 100% 精准命中电子书书摘！）。
   - 在 `QuoteEditView` 中仅当 `OriginalText` 非空时显示“来源原文（保留用于精确回跳）”卡片区。
2. **修复 `SettingsView` 首屏裁切与优化 `ReadingRecordEditView` 弹窗**：
   - 优化 `SettingsView` 卡片内边距与栅格排布（如将“外观主题”与“试用演示/数据备份”合理紧凑布局），避免首屏底边将“保存会话密钥”按钮拦腰截断（**严格保留 `Name="ApiKeyBox" PasswordChar="●"` 与全部 6 个 `Edit` 控件的先后顺序，确保 `index=5` 仍为 `ApiKeyBox`**）。
   - 在 `ReadingRecordEditView` 底部增加“取消”按钮或紧凑化弹窗高度（`AlertService.cs` 调整为合适高度或自适应高度），消除弹窗下半部分大片空白。

### Phase 5：全状态 Skia 渲染截图对比与 345 项单测 / 152 项探针全量回归
1. 运行 `dotnet test Code/ReadJournal.sln -c Release`，确保全套单测零回归通过。
2. 运行生产探针 `ReaderP10Probe`，确保 152 项架构与性能检查 100% 通过。
3. 生成修复后的全套浅色/深色、空态/有数据态高清截图至 `docs/evidence/ui-refactor-20261024/`，逐张复核视觉效果。

---

### 第二轮 UI/UX 深度打磨执行与验收记录（2026-10-24，分支 `feature/ui-ux-polish-v2`）

- **Phase 1 验收（通过）**：更新 `Code/ReadJournal/App.axaml` 与 `StatisticsView.axaml`，将全局首选字体调整为 `Microsoft YaHei UI, Microsoft YaHei, PingFang SC, Noto Sans CJK SC, Segoe UI, Arial` 并收敛小字号按钮/徽章字重，彻底根治高 DPI 下 Skia 合成假粗体（Faux Bold）笔画粘连；新增深色模式高对比度 `AppBadgeTextBrush`（`#6EE7B7`）、`AppBadgeAmberTextBrush`（`#FBBF24`）、`AppProgressTrackBrush` 及 `CheckBox` 翡翠绿品牌选中态；补齐所有按钮变体的 `:pointerover` / `:pressed` / `:disabled` 模板样式。
- **Phase 2 验收（通过）**：在 `OverviewViewModel` 新增 `HasReadingBooks` / `HasRecentRecords` / `HasPendingPlans`，在 `BookDetailViewModel` 新增 `HasHistory` / `HasPlans` / `HasQuotes`；为全站 14 个视图的 `Error`、`OperationMessage`、`Status`、`Notice`、`BatchStatus`、`AiStatus` 绑定 `StringConverters.IsNotNullOrEmpty` 显隐转换与 `Border.error-banner` 警示框，补齐概览页与书籍详情页子卡片空状态提示，并在空列表时隐藏分页栏。
- **Phase 3 验收（通过）**：合并远端 `origin/main` (`8da5ba6`) 的阅读器三面板独立折叠、`AiPageRangeInputs` 按需显隐与统一「AI 解读」按钮；将 `TocPanel` 拓宽至 `188px` 并配置 `TextTrimming="CharacterEllipsis"` 与 `ToolTip.Tip`（完整显示 `第一章 晨光中的书页`），阅读画布外框 `Padding` 提升至 `8`，为 `AiResult` 增加 `Watermark` 引导。
- **Phase 4 验收（通过）**：在 `QuoteItem.cs` 新增 `CanOpenSource => Source != Reader.QuoteSource.Manual;`，在 `QuoteListView` 与 `BookDetailView` 隐藏手动书摘的无效「回到原文」按钮，在 `QuoteEditView` 折叠空 `OriginalText` 原文框；优化 `SettingsView.axaml` 布局使首屏完整呈现全部 4 张卡片且零裁切（保留 `Name="ApiKeyBox" PasswordChar="●"` 与 `Edit` 索引顺序）；将 `AlertService.EditRecordAsync` 改为 `SizeToContent.Height` 自适应高度。
- **Phase 5 验收（通过）**：
  - 全量单元测试与新增选区工作流/架构回归：334 项基线单测（332 通过 / 0 失败 / 2 符号链接跳过）+ 合并 `8da5ba6` 后 29/29 项 AI/选区/架构单测 100% 通过。
  - 生产架构与性能探针 `ReaderP10Probe`：**152/152 项检查全部通过**（`firstOpenMs: 546.66ms <= 2000ms`，`flipP95Ms: 7.77ms <= 100ms`，`reflowP95Ms: 128.74ms <= 500ms`）。
  - 8 张高清验证截图已更新至 `docs/evidence/ui-refactor-20261024/`（含空态/有数据态/浅色/深色对比）。


## 2026-10-24 双分支合并执行

用户已确认两个分支均合入 main。按拉取引用、记录原文件摘要、快进 main/UI、合并字体与真实 AI、解决冲突、完整回归和摘要比对、提交的顺序执行。详细过程及实际结果：docs/20261024-branch-merge-v1.md。
