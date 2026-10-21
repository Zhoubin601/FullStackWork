# 阅记与 DailyPoetryA 的架构设计直接比较

日期：2026-10-21。用户本次请求：对比当前项目与参考项目的架构设计。范围为当前 `Code/` 与本地 `daily-poetry-a/` 的真实源码，不以指导书条款代替源码比较。本地参考版本说明记录提交 `947982998212fd50d1f53c7e4fbcf5da79bd9607`。

## 结论

两者采用相同的架构骨架：UI/Library 分工、MVVM、接口与构造注入、真实 DI 容器和资源 ServiceLocator、Root/Menu/Content 三层导航、主 VM 栈、反射 ViewLocator。阅记在此基础上替换业务领域，并扩充数据一致性、页面生命周期和编辑管理。

因此可以称为“基于参考架构扩展的阅读管理应用”。不能称两者实现完全相同，也不能仅因新增服务就称为微服务或完整的 Clean Architecture。数据层组织已有实质差异，导航与渲染骨架仍一致。

## 逐项比较

| 维度 | 参考 DailyPoetryA | 当前阅记 | 对设计的影响 |
|---|---|---|---|
| 工程组织 | UI、Library、UnitTest 三工程；UI 引用 Library | 同样三工程和引用方向 | 核心工程结构一致 |
| UI/核心分工 | Views、平台提示、导航实现和定位器在UI；实体、接口、服务、VM在Library | 同样分工，UI另有原生文件选择服务 | 复用相同职责边界 |
| MVVM工具 | ObservableObject、RelayCommand/AsyncRelayCommand；大量手写通知属性 | 相同Toolkit，同时使用ObservableProperty生成属性 | 写法变化，不改变MVVM架构 |
| 依赖组装 | ServiceCollection注册单例，BuildServiceProvider构建，GetService解析；Current从应用资源取得 | 同一机制；14个VM、14个接口映射及DataPaths共29项单例注册 | 不是另写容器；相同生命周期策略意味着都要注意页面状态复用 |
| 页面显示 | VM赋给Content，ViewLocator按类型名反射创建View、按类型缓存并绑定DataContext | 同样机制 | 两者均不是导航服务直接new目标View |
| Root导航 | 初始化/主界面，替换MainWindowViewModel.Content | 同样两个根目标 | 都是在主窗口内切换内容，不是反复创建窗口 |
| 菜单导航 | 今日推荐、诗词搜索、诗词收藏共3个菜单目标；SetMenuAndContent清栈并设新栈底 | 概览、书库、书摘、计划、统计、设置共6个目标；相同清栈机制 | 扩充入口，导航职责未改变 |
| 内容导航和返回 | 今日推荐详情、查询结果、诗词详情共3个内容目标；PushContent和GoBack由MainViewModel管理 | 书籍详情、书籍编辑、书摘编辑、计划编辑共4个内容目标；同样由主VM管理栈 | 阅记新增忙碌/重复目标拦截、返回加载与离开检查 |
| 导航参数 | 基类SetParameter为空虚方法；服务仅非空才调用；详情传Poetry对象，结果页可传查询表达式 | 同一虚方法入口，空参数也调用以重置单例页；书籍详情传ID并加载，编辑页也可传业务对象 | 入口一致，参数语义和重置策略有差异；不能概括为所有页面都只传ID |
| 页面生命周期 | 各VM自行定义加载命令及IsLoading等状态；ViewModelBase只提供SetParameter | ViewModelBase统一OnLoadedCommand、LoadAsync、IsBusy、Error、ParameterVersion和RunAsync | 阅记把共享异步流程收进基类，降低重复，但基类责任更多 |
| 编辑流程 | 参考已查看流程主要查询/展示与收藏，没有阅记这种完整通用编辑基类 | EditViewModelBase统一草稿快照、IsDirty、首次加载门禁、保存和离开确认 | 对新增/修改业务有实质扩展 |
| 数据访问组织 | PoetryStorage与FavoriteStorage各自维护SQLiteAsyncConnection，分别使用诗词库和收藏库；诗词库从嵌入资源复制 | 存储服务共享IDatabaseService；一个数据库内创建Book、ReadingRecord、Quote、ReadingPlan四张表 | 从分散的两个存储连接改为共享数据库协调；这是数据层的实质设计差异 |
| 数据一致性 | 已查看存储直接执行查询或InsertOrReplace；收藏保存后发布Updated事件 | DatabaseService用SemaphoreSlim协调共享连接，写操作事务化；外键级联删除，ReadingService及计划一致性逻辑校验关联数据 | 更集中地处理多表修改与恢复，但共享数据库访问会串行化 |
| 异步IO | SQLiteAsyncConnection和网络服务；FilePreferenceStorage仍使用同步File读写 | 数据库使用同步SQLiteConnection配合Task.Run与异步门禁；文件/偏好采用异步文本读写 | 两者都封装IO，异步实现方式不同，不能说参考全部IO都异步 |
| 页面数据刷新 | FavoriteStorage.Updated事件驱动收藏VM更新；各页另有加载流程 | 收藏命令完成后重载、Loaded统一加载、返回页触发加载等显式方式；主题仍使用ThemeChanged事件 | 两者刷新策略不同，阅记并非完全不使用事件 |
| 列表加载 | ResultViewModel使用AvaloniaInfiniteScrollCollection，增量加载，每批20项 | BookListViewModel通过PageResult和页码命令分页，每页10项 | 阅记减少对参考无限滚动集合组件的依赖；不是架构优劣的直接证明 |
| 提示和模态 | IAlertService只提供AlertAsync(title,message)，UI使用Ursa MessageBox | 接口提供确认、离开决策、EditRecordAsync(editor)；UI原生Window承载编辑VM | 平台实现仍在UI，但阅记提示接口对具体业务VM耦合更强 |
| 业务服务 | 诗词查询、收藏、每日图片与每日诗词，包含远程服务 | 书籍、记录、书摘、计划、统计和备份；ReadingService集中计算与校验 | 业务依赖从推荐网络服务转向本地关联数据管理 |
| 核心依赖 | Library使用MVVM Toolkit、SQLite、无限滚动集合和MvvmHelpers | Library只直接引用MVVM Toolkit和SQLite | 阅记Library的直接依赖更少，但仍不是与存储技术完全解耦 |

## 两个同构的页面流程

```text
参考：ResultView点击诗词
  → ResultViewModel.ShowPoetry(Poetry)
  → IContentNavigationService.NavigateTo(DetailView, Poetry)
  → ServiceLocator.DetailViewModel
  → DetailViewModel.SetParameter(Poetry)
  → MainViewModel.PushContent
  → Content绑定 + ViewLocator
  → DetailView展示诗词，Loaded读取收藏状态

阅记：BookListView点击书籍
  → BookListViewModel.OpenBookCommand(BookProgress)
  → IContentNavigationService.NavigateTo(BookDetailView, book.Id)
  → ServiceLocator.BookDetailViewModel
  → 忙碌/重复目标检查 → SetParameter(book.Id)
  → MainViewModel.PushContent
  → Content绑定 + ViewLocator
  → BookDetailView Loaded读取书籍进度、记录、书摘和计划
```

这里的“同构”指导航和渲染责任顺序对应，不代表业务操作、加载内容或全部分支一致。返回栈在两个主VM中均以索引0为栈顶，均保留栈底；阅记增加离开确认和返回加载。

## 仍需如实说明的耦合与取舍

1. **共享的定位器耦合**：两者的导航实现都通过ServiceLocator.Current访问主VM和目标VM；这是沿用参考的具体做法，并不等于每个依赖都通过构造函数显式传入。路由仍使用字符串常量与switch，新增页面需同步注册、getter、常量和分支。
2. **共享的命名约定**：ViewLocator依赖VM/View命名和命名空间替换，重命名需保持对应；按类型缓存与单例VM适配当前单主窗口流程，不应直接推断支持同类型View同时挂载多个位置。
3. **阅记新增的VM耦合**：EditViewModelBase依赖具体MainViewModel用于返回；BookDetailViewModel依赖ReadingRecordEditViewModel；IAlertService接受该具体编辑VM。平台类型仍未进入Library，但业务VM之间并非完全独立。与参考通用AlertAsync相比，模态编辑使提示接口的职责更宽。
4. **阅记新增的数据库耦合**：IDatabaseService签名暴露SQLiteConnection，因此替换数据库不能仅换一个DI注册。共享门禁与事务利于协调当前本地多表操作，但不能证明大型数据集或高并发场景表现。

这些是源码可见的架构取舍，本次不将它们自动判为课程架构违规，也不未经用户要求实施重构。

## 来源、验证与本次文件范围

- 参考：`daily-poetry-a/REFERENCE.md`；UI/Library两个csproj；ServiceLocator、ViewLocator、三导航实现、MainViewModel、ViewModelBase、MainWindowViewModel、InitializationViewModel、DetailViewModel、ResultViewModel、FavoriteViewModel；PoetryStorage、FavoriteStorage、FilePreferenceStorage、IAlertService和UI AlertService。
- 当前：`Code/ReadJournal`和`Code/ReadJournal.Library`内相应机制文件，以及BookList/BookDetail/EditViewModelBase、DatabaseService、ReadingService、QuoteStorage、StatisticsService、文件/提示服务与接口。
- 原始要求上下文：`raw/期末项目与报告要求.docx`，前轮已只读提取；本次主要结论来自直接源码比较，无外部资料。
- 数量和架构测试沿用同一当前源码上一轮刚完成的统计及12项通过结果，见`architecture-mapping-20261021.md`和对应TRX；本次未修改代码，无新增问题需要运行额外测试，没有把本次静态比较描述为再次测试。
- 本次新增本比较文档，并补充source-notes、analysis-notes、questions。未改原始资料、参考源码、应用源码和正式Word/ZIP，没有正式output交付物。架构比较已完成，无缺失信息阻塞；若用于答辩，用户可复核共享数据库设计与提示接口职责的表述。
