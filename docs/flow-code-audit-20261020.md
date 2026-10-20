# 功能连通性代码审查（2026-10-20）

> 本文件为修复前的历史审查。用户随后已授权修复，当前状态和复验结果见 [缺陷修复记录](repair-results-20261020.md)。


审查对象：当前工作区 `Code/` 手写实现；只读源码，不修改应用或既有测试。依据：`docs/plan.md` v4 的业务闭环、导航、草稿保护、计划和备份规则。课程指导书的页面数、类型数、报告等合规性由本次独立要求审计核对，不把规划中的额外业务规则混称为指导书原文。

本记录是过程审查，不能代替本次构建、自动化测试和实际 UI 操作结果。新增复现用例位于 `/tmp/readjournal-audit/AuditRegressionTests.cs`，由主审计代理在独立解包工程执行。主审计代理已反馈 5 条复现全部触发预期的失败（证明缺陷存在），具体日志见主审计汇总。原有测试通过与新增复现失败应分别报告。

## 1. 已连通的代码链路

| 流程 | 当前实现证据 | 静态结论 |
|---|---|---|
| 启动和初始化 | `App.axaml` 注册资源；`MainWindowViewModel` 根导航；`InitializationViewModel` 初始化成功后转主容器和概览 | 已实现，数据库异常由基类捕获并留在初始化页，可重试 |
| IoC 和页面定位 | `ServiceLocator` 的 ServiceCollection→BuildServiceProvider→IServiceProvider→GetService；全部注册单例；`ViewLocator` 反射并缓存视图 | 已采用参考约定，未见自建字典容器或构造循环 |
| 三层导航 | Root/Menu/Content 三服务及常量；`MainViewModel` 保存返回栈；内容导航在 SetParameter 前拦截重复单例 | 路由映射完整 |
| 书库到资料编辑 | BookList UI 命令→BookEdit→BookStorage→真实 SQLite | 新增、修改、搜索、筛选、分页均有实现 |
| 阅读记录到进度 | BookDetail→ReadingRecordEdit 弹窗→ReadingRecordStorage；保存/删除后详情重载；统计从全量数据重新计算 | 正常链路完整；闭区间页数和重读统计有实现 |
| 书摘和计划 | 列表/详情→编辑→存储→返回时重载 | 正常链路完整；发现下面的异步输入和计划一致性缺陷 |
| 设置和备份 | 主题事件应用并落盘；虚构演示数据仅空库导入；恢复前校验、自动备份和 SQLite 事务 | 正常链路完整；备份恢复会拒绝某种应用自身可产生的数据状态 |
| 退出和草稿确认 | 菜单、返回、主窗口关闭均调用 CanLeaveAsync；保存失败保留草稿和错误提示 | 正常链路存在，保存等待期间仍可输入造成缺口 |

## 2. 明确缺陷

### P2：编辑页异步加载会覆盖已输入草稿

- 位置：`Code/ReadJournal.Library/ViewModels/BookEditViewModel.cs:19`；`QuoteEditViewModel.cs:21`、`:23`；`ReadingPlanEditViewModel.cs:23`、`:24`。
- 触发：打开已有书籍编辑页，数据库返回前输入新书名；或打开新增书摘页，在书籍选项载入前输入正文。
- 原因：`IsDirty` 只在加载开始检查；await 后只比较页面参数版本，直接赋值全部表单并调用 AcceptChanges。对应 XAML 没有在 IsBusy 时禁用输入。
- 后果：新输入被旧数据（新增书摘时为空正文）覆盖，且不再被识别为未保存修改。
- 复现用例：`LoadingExistingBookMustNotOverwriteDraftTypedDuringAwait`、`LoadingQuoteBookChoicesMustNotEraseTextAlreadyTyped`。测试用待完成的 Task 固定等待时序，避免靠随机延迟。
- 要求归属：规划第 2 节未保存编辑保护、第 3 节单例草稿管理；不是指导书直接列出的业务条文。

### P2：保存中的新输入被当作已保存并离开

- 位置：`Code/ReadJournal.Library/ViewModels/EditViewModelBase.cs:15`、`:20`、`:29`；例如 `BookEditViewModel.cs:26` 在 await 前构造提交对象。
- 触发：点击保存后，存储完成前继续修改仍可编辑的书名/正文等字段。
- 原因：提交的是点击时的实体；存储返回后 `AcceptChanges()` 却记录此刻的新草稿，随后 `AfterSaved()` 返回上一页。输入控件没有 IsBusy 禁用绑定。
- 后果：保存期间的新输入没有写入数据库，却被标记为 clean 并离开页面，用户看不到未保存提醒。
- 复现用例：`ChangesTypedDuringSaveMustNotBeMarkedSavedAndDiscarded`。
- 要求归属：规划未保存修改保护、异步交互正确性。

### P2：可通过合法计划编辑和记录删除产生两个未完成计划，并导出不可恢复的备份

- 位置：`Code/ReadJournal.Library/Services/ReadingPlanStorage.cs:21`、`:22`；`ReadingRecordStorage.cs:15`；`BackupService.cs:51`。
- UI 路径：A、B 各创建一个计划 → 记录 A 到书末 → 在计划编辑页把 B 计划的关联书籍改为 A → 删除 A 的完成记录。
- 原因：A 完成时，已有计划改关联至 A 被允许，形成两个已完成计划；删除完成记录使两条计划同时重新变为未完成，记录写入路径没有维护唯一性。
- 后果：违反“每本书最多一个未完成计划”。正常导出会成功，但恢复验证随后以“同一本书有多个未完成计划”拒绝应用自身导出的 JSON；业务状态与备份约束不一致。
- 复现用例：`RemovingCompletionRecordMustNotProduceTwoUnfinishedPlans`、`BackupExportedAfterLegalUiOperationsMustPassOwnValidation`，使用真实临时 SQLite，导出用 mock 捕获 JSON。
- 要求归属：规划第 4 节计划唯一性、修改/删除记录后计划一致性、备份往返一致性。

### P3：部分编辑页加载失败没有页内重试入口

- 位置：`Code/ReadJournal/Views/BookEditView.axaml:3`、`QuoteEditView.axaml:3`。
- 触发：读取已有书籍，或书摘/书籍选项读取失败。
- 表现：显示 Error，但只有保存按钮，没有重新加载按钮；用户需要返回再进入。计划编辑页已有“重新加载”按钮。
- 要求归属：规划第 2 节“加载失败……重试或新增入口”和第 3 节“加载失败时保留目标页、重试入口和返回按钮”。

## 3. 结论边界

正常业务路由在代码上已经连通，未发现 TODO 或 NotImplemented 占位，不能据此宣称所有功能完全打通。以上异常时序和跨业务状态缺陷需修复后再验收；Windows 桌面行为仍必须以目标平台实测为准。本次没有修改应用源码，也没有修正或重新生成正式交付物。
