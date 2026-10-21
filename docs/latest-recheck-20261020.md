# 最新版本架构与功能复核

> 后续状态：用户于2026-10-21要求修复本轮P1，现已实施并通过93项完整测试、71项架构探针及新版独立源码包复验，见[p1-fix-20261021.md](p1-fix-20261021.md)。本文保留0313917修复前的检查与未关闭结论，不代表当前修复后的状态。

日期：2026-10-20。本轮按用户要求获取最新代码，重点核对指导书架构及功能贯通情况。本文为本轮已完成的检查记录；历史审查不代替本轮验证。

## 基线与来源

- 初始分支 `work` 为 `20d9c67`，工作区干净；执行 `git fetch origin`，发现远端 `origin/main` 新增提交 `031391711512bec26d36c0b53963bcc57898e8ce`，以 `git merge --ff-only origin/main` 同步成功。本轮没有推送。
- 原始指导书：`raw/期末项目与报告要求.docx`，直接以 ZipFile/XML 读取，SHA256 `352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d`，提取正文存于 `docs/evidence/recheck-20261020/requirements.txt`。
- 架构参照：只读 `daily-poetry-a/` 实际源码及既定 `docs/plan.md`。64 个参考 C# / XAML 文件与保留基线哈希一致。
- 实现依据：最新 `Code/`。在隔离数据目录中进行验证，不修改原始资料、参考源码或交付物。

## 已执行的验证

- 本轮安装 .NET SDK 8.0.424，locked-mode 还原成功，Release 构建 0 错误；3 条既有 NETSDK1206 警告来自 SQLite 原生包的旧 Alpine RID。
- 现有自动化测试 **83 通过、0 失败、0 跳过**，证据：`docs/evidence/recheck-20261020/recheck.trx` 及 `test.log`。此数字只代表现有用例，不代表所有时序均正确。
- Roslyn 类型重新统计：参考 63 类、10 接口、9 视图；应用 66 类、14 接口、14 视图（含10业务页）。按参考计算的最低要求为45类、7接口、9视图，均满足。
- 最新源码 v3 包 118 文件、109,424 字节，与 Code 白名单逐文件字节一致，缺失、额外文件及编译产物目录均为0。SHA256 `089ca8f42b4777cb290cd0ee119214ac365b015d2e92ebc01a1d36a2faf6e8a0`，证据：`source-package.json`。

## 架构与功能结论

**核心架构对应；当前不能判定功能验收完全通过。** 本轮新增发现一处恢复与旧书摘操作之间的并发一致性缺陷，应用源码未修改，问题仍未关闭。现有83项测试通过不覆盖此时序。

架构独立复核见 `architecture-recheck-20261020.md`：八项机制均在真实代码和XAML接线中对应，真实App资源/DI/反射视图/导航运行时探针 **71项通过**。UI、测试和探针实际使用的两个应用程序集哈希完全一致，没有将旧二进制结果套用于新源码。

| 指导书机制 | 当前实际机制 | 结果 |
|---|---|---|
| MVVM | Models、Library/ViewModels、UI/Views按职责分层；薄code-behind只负责平台事件桥接 | 对应 |
| 统一导航参数 | ViewModelBase.SetParameter；Menu/Content服务先注参后切换；单例参数版本保护 | 对应 |
| 接口抽象/构造注入 | 14个服务接口由真实DI映射，VM依赖接口 | 对应 |
| 轻量ServiceLocator | ServiceCollection → AddSingleton → BuildServiceProvider → readonly IServiceProvider → getter GetService；Current从App资源读取 | 对应 |
| 三层导航 | Root/Menu/Content及统一常量；MainViewModel持ContentStack、PushContent、GoBack；接口不持栈 | 对应 |
| ViewLocator | IDataTemplate、命名映射、Type.GetType、Activator.CreateInstance、按View类型缓存并设置DataContext | 对应 |
| IO服务化/异步 | VM调用存储、文件、提示接口；SQLite/文件IO在服务，概览查询已从VM迁入存储层 | 对应 |
| Library/UI双工程 | Library无Avalonia/UI工程依赖；UI单向引用Library并负责平台实现 | 对应 |

常量放在Library对应导航接口文件中，与真实参考及指导书“常量归接口”一致。阅读记录按既定规划通过IAlertService打开模态编辑器，仍通过SetParameter与ViewLocator渲染，不作为三层导航机制缺失。严格对应指上述架构机制和职责对应，不表示业务内容、全部方法或文件与示例一模一样。

## 未关闭问题：P1 恢复后的书摘可被旧收藏操作覆盖

位置：`Code/ReadJournal.Library/ViewModels/QuoteListViewModel.cs:22`、`ViewModelBase.cs:29`、`Code/ReadJournal.Library/Services/QuoteStorage.cs:16`及`BackupService.cs:17`。

1. 书摘列表执行“切换收藏”，等待数据库读取；该页面CanLeaveAsync沿用基类默认true。
2. 用户可经MainViewModel菜单命令进入设置，并执行恢复。设置自身的忙碌保护只防止离开设置，不等待其他页面此前发起的操作。
3. 在共享数据库门禁内，旧读取、恢复、旧保存按此顺序完成。恢复是原子的，但QuoteList的读取和保存是两个独立操作，旧引用在恢复后仍可保存。
4. QuoteStorage只检查ID和父书存在，随后的整实体Update将恢复后同ID书摘正文、感想及页码覆盖成旧值；收藏位同时改变。两侧Error为空，设置仍显示“恢复成功”。

已在实际DatabaseService/QuoteStorage/BackupService及实际VM命令上确定性复现：备份期望正文`RESTORED quote text`、页码2，最终变成`OLD quote text`、页码1，书名仍为`RESTORED book`。业务审阅5次均复现，主审另独立执行同一探针1次亦复现，正确行为断言退出1（`docs/evidence/business-recheck-20261020/primary-confirmation.json`及`primary-assertion.log`）。这说明不是恢复未执行，而是恢复后旧写入覆盖了部分结果。

复现使用单线程SynchronizationContext模拟UI调度，并让另一条无修改真实数据库读取持门禁以模拟慢IO；没有暂停或篡改ToggleFavorite的GetAsync结果，也没有手工调用旧实体Save。文件选择、确认和菜单服务为测试替身，菜单按实际SetParameter→SetMenuAndContent语义转发。**这是VM+SQLite受控时序复现，不是真实GUI现场复现，也不证明普通操作的发生概率。** 独立审阅者核对了源码、排队过程和实际结果。证据与可重跑源码见`docs/evidence/business-recheck-20261020/`及业务复核记录。

建议在保持现有DI/单例/三层导航机制的前提下，增加跨页面数据变更与恢复之间的操作协调；恢复时应等待旧变更结束，或用数据集版本拒绝旧操作回写。收藏应在存储服务中原子更新字段，避免整实体旧快照覆盖；单独只改字段仍不能替代跨恢复的版本/协调保护。若采用忙碌时禁止导航，还需同时覆盖菜单、返回及列表直接进入内容页的Add/Edit入口。修复后应将本次确定性时序纳入永久回归。

## 真实桌面链路

本轮使用全新隔离数据及Xvfb :119运行实际Avalonia窗口，写入均经真实界面操作；SQLite仅作事后只读核对。

- 新增120页书籍，添加第10～30页、25分钟记录；详情/概览/统计均显示累计21页、25分钟，当前进度30/120（25%）。
- 新建页12的收藏书摘，以及目标2026-10-24的计划；计划显示剩余90页、建议每日18页。书库、编辑、详情、书摘、计划、统计、概览和设置的主要链路均可操作。
- 经真实系统文件选择器导出JSON，四种实体各1条；删除书籍后四张表均为0，级联清理正确。
- 经真实文件选择器恢复并确认，恢复后四张表各1条，原ID、父子关联、正文、收藏位和日期一致，并产生恢复前自动备份。
- 终止隔离应用进程后，以同一数据目录重新启动，概览、详情和统计仍为21页/25分钟，数据保持。无窗口管理器，Alt+F4未生效，本轮使用SIGTERM；不声称验证了窗口关闭事件。两次有效运行日志的绑定诊断均为0。

主审另直接核对`roundtrip.json`、`restore-data.json`、`deletion-counts.json`及重启统计截图；逐字段验证备份与恢复结果，包括日期精确转换为.NET ticks，恢复后与重启后的全部四表数据完全一致。机器汇总见`docs/evidence/recheck-20261020/validation.json`。正常UI闭环通过，不抵消上述受控并发时序已证实的P1问题。真实窗口完整记录见 `ui-recheck-20261020.md`，业务复现见 `business-recheck-20261020.md`。

## 本轮交付记录及范围

- 新增过程报告：本文件、`architecture-recheck-20261020.md`、`business-recheck-20261020.md`、`ui-recheck-20261020.md`。
- 新增证据：`docs/evidence/recheck-20261020/`及`docs/evidence/business-recheck-20261020/`；更新来源、分析及当前进度背景，避免把旧“无未修复问题”误作本版结论。
- 原始来源为课程DOCX、真实DailyPoetryA副本、最新Code及本轮运行记录，未引用外部项目替代课程依据。
- 收尾再次fetch后HEAD与origin/main仍为`0313917`，ahead/behind为0/0；本轮未修改Code、raw、参考源码、正式源码ZIP或PDF，也未提交/推送。
- Windows桌面和Alpine未实测。Linux X11存在DBus/会话管理和软件GL回退提示，业务操作正常；不宣称GPU硬件验证。
- 当前应先修复上述跨恢复旧写入，纳入永久回归后再作完整功能验收；本轮检查工作已完成。
