# 本轮业务闭环与异步导航审查

日期：2026-10-20。审查基线为本轮已 fetch 的 `20d9c67`（当时 HEAD 与 origin/main 相同）。原始资料采用 `raw/期末项目与报告要求.docx`，实现和边界以实际 `Code/` 为证据；原文件、参考源码、历史证据未修改。本记录属于过程文档。

## 当前结论

书籍、阅读记录、书摘、计划、统计和备份恢复的正常调用链均存在，未见空实现。进一步检查发现了历史 68 项测试未覆盖的异步导航缺陷：旧书籍删除操作可能删除新导航目标，或跳出后来打开的编辑草稿；补齐删除上下文保护后，又通过确定时序测试发现新目标的 Loaded 可能被旧操作的 IsBusy 吞掉，导致详情空白。它们影响“功能是否打通”的结论，不能仅依据旧测试通过予以忽略。

删除上下文、Loaded 等待和设置页操作离开保护均已完成。最终 Release 全套 **83 项全部通过**；源码 v3 独立解包、锁定依赖还原、Release 构建及同一 83 项也全部通过。新证据为 `docs/evidence/architecture-flow-20261020/final.trx` 与 `clean.trx`。细项修复和真实窗口结果见 `docs/architecture-flow-review-20261020.md`。

## 正常链路与约束复核

| 链路 | 当前实际代码与核对结果 |
|---|---|
| 书库 → 新增/编辑书籍 → SQLite → 返回书库/详情 | BookList 的 Add/Open 命令通过内容导航；BookEdit 的 SaveCore 调用 IBookStorage；BookStorage 在事务中检查最大关联页码、维护创建时间；返回触发重载 |
| 详情 → 阅读记录弹窗 → 进度/统计/计划 | BookDetail 配置 ReadingRecordEdit，弹窗 Saved 关闭后重载；ReadingRecordStorage 写入/删除均在同事务检验历史计划一致性；ReadingService 由有效记录派生当前页、累计页数和最早完成日；StatisticsService 使用 long 汇总 |
| 书摘搜索/收藏/编辑/删除 | QuoteList 命令与 UI 绑定到存储；收藏读取新实体后更新；QuoteStorage 验证父书和页码；书籍删除通过 SQLite 外键级联清理 |
| 计划创建/编辑/状态与建议 | ReadingPlanStorage 检查父书、目标日、同书重复计划；既有计划 StartDate 从数据库原值保留，不会因编辑 VM 构造新实体而被重置；计划状态与建议由当前进度派生 |
| JSON 导出/恢复 | BackupService 先完整反序列化并校验版本、必要字段、ID、关联、业务规则；恢复在共享串行事务内先写自动备份，再删除和恢复四表；自动备份失败不进入替换，插入失败由事务回滚 |
| 演示数据/主题 | 演示导入先确认空业务库；主题通过 IPreferenceStorage 独立落盘，不进入业务备份 |
| 编辑保护 | EditViewModelBase 有首次成功加载门禁，保存只接受已提交快照；Book/Quote/Plan 的加载合并未改字段，记录弹窗参数就绪后允许保存；CanLeave 的保存失败不离页 |

以上是当前源码的独立复核，不将历史测试数字当作本轮执行结果。真实 SQLite、全套自动化和真实窗口证据由主代理汇总。

## 本轮确定缺陷与复现

### 1. 删除等待期间切换到另一书籍，会使用新书籍 ID 提交删除

- 基线位置：`Code/ReadJournal.Library/ViewModels/BookDetailViewModel.cs` 原第31行删除命令；修复后第31～44行。
- 触发：A 详情点击删除，`GetDeleteImpactAsync(1)` 等待 → 菜单切回书库 → 打开 B，复用同一详情单例并 `SetParameter(2)` → A 的关联统计完成 → 确认。
- 基线实际结果：mock 精确记录 `GetDeleteImpactAsync(1)`，随后却调用 `DeleteAsync(2)`。这是可变 `_id` 跨 await 被复用，不是随机延迟推测。
- 永久回归：`PendingImpactReadCannotDeleteBookOpenedByLaterNavigation`；使用实际 MainViewModel 菜单命令与同样的目标参数/栈变化。正确结果是在上下文已变化时不弹旧确认、不提交删除，保留新详情。
- 风险：会误删新书及其级联记录，属于明确的数据正确性缺陷。

### 2. 旧删除结束时，直接返回会绕过新编辑页的未保存确认

- 基线位置同上，原命令在 DeleteAsync 完成后无条件 `main.GoBack()`。
- 触发：已确认删除 A，等待数据库提交期间 → 菜单切回书库 → 打开另一个编辑器输入草稿 → A 删除完成。
- 基线实际结果：新 BookEditViewModel 被弹出，内容回到书库，未请求草稿离开确认。
- 永久回归：`CompletionOfOldDeletionCannotDiscardDraftOnNewPage`；以及 `CommittedDeletionCannotPopReopenedDetailUsingSameSingleton`，验证同一详情实例换参数后也不可被旧操作退走。
- 正确结果：已提交的 A 删除可以完成，但旧操作不能导航新页面。

### 3. 删除上下文取消后，B 详情的 Loaded 被旧操作忙碌状态忽略

- 基线位置：`ViewModelBase.OnLoadedCommand` 原第16行调用 RunAsync；RunAsync 原第24行遇 IsBusy 直接返回。
- 触发：旧 A 删除正在等待关联统计 → 菜单和内容导航到 B → B Loaded 执行时单例仍 IsBusy → 旧 A 读取返回并被上下文补丁安全取消。
- 删除保护补丁后的实际结果：没有误删，但 B 的 Book 保持 null，Loaded 不会自动重试。
- 永久回归：`ReopenedDetailLoadsNewBookAfterOlderOperationCompletes`。正确结果是新 Loaded 等待当前操作释放后加载最新参数，最终 B.Id=2，Error 为空。

### 4. 全库恢复进行中，设置页仍允许切菜单或关闭窗口

- 位置：`SettingsViewModel` 原本沿用 `ViewModelBase.CanLeaveAsync()`，该默认方法总返回 true；恢复命令虽然设置 IsBusy，却没有对应的离开门禁。
- 确定复现：以 Task 门闩令 `IBackupService.RestoreAsync` 保持等待，真实执行 `Settings.RestoreCommand` → `Main.OnMenuTappedCommand`，内容实际切到 BookList；独立执行 `Main.CanCloseAsync` 实际返回 true。
- 业务影响：全库替换尚未结束时可进入其他页面或终止程序，无法保证用户观察到恢复完成后再基于新数据继续编辑。潜在旧编辑快照覆盖恢复后同 ID 数据的完整 SQLite 时序尚未独立复现，因此没有把它冒称为已证明的数据丢失。
- 永久回归：`Code/ReadJournal.UnitTest/SettingsNavigationTests.cs` 共4项，分别约束恢复等待期间阻止菜单、阻止关闭，以及恢复失败/成功后恢复正常离开能力。
- 修复前独立运行：2失败、2通过；仅编译外部临时测试工程，指定 `-p:BuildProjectReferences=false` 引用已有应用 DLL，没有重编译 Code 干扰主代理补丁。

## 本轮新执行证据

| 阶段 | 结果 | 路径 |
|---|---|---|
| 独立临时复现 | 2 项缺陷断言失败；另1项日期下界信息探针通过，未把它列为缺陷 | `docs/evidence/architecture-function-20261020/business-initial.log`、`.trx` |
| 首批永久回归，修复前 | 2失败、1通过；失败分别是误删新目标、退走新草稿 | `docs/evidence/architecture-function-20261020/deletion-before.log`、`.trx` |
| 删除上下文补丁后，Loaded 修复前 | 5通过、1失败；仅新详情自动加载失败 | `docs/evidence/architecture-function-20261020/deletion-context-only.log`、`.trx` |
| 设置页恢复离开保护修复前 | 2失败、2通过；确认恢复中可切菜单、可关闭 | `docs/evidence/architecture-function-20261020/settings-before-independent.log`、`.trx` |

首批修复前日志的类名与源码路径是随后重命名前的 `DeletionNavigationRegressionTests`；永久文件最终为 `Code/ReadJournal.UnitTest/DeleteNavigationRaceTests.cs`。这只是命名变化，修复前失败内容未改写。

## 边界

本轮没有以新的不明要求替代指导书；异步保护属于现有功能正确性，仍保留单例、三层导航、参数入口和 MainViewModel 栈。未对 Windows 桌面实测，也未宣称对所有可能的多进程并发进行形式证明。本轮自动化使用确定 Task 门闩复现导航时序；真实图形界面与 SQLite 验证结果以主代理最终汇总的新日志为准。
