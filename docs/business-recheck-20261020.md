# 最新版本业务链路独立复核

> 本文为0313917的修复前证据。2026-10-21已按用户要求修复，当前结果见[p1-fix-20261021.md](p1-fix-20261021.md)；旧失败复现与断言不覆盖、不改写。

日期：2026-10-20。代码基线：最新 `origin/main` 提交 `0313917`。本次直接读取当前 `Code/` 的 VM、服务、实体、导航实现及 XAML；既有审查仅作定位线索，不沿用其通过结论。本文件为过程记录，未修改应用、永久测试、原始资料、参考源码和交付包。

## 结论

正常的书籍 → 阅读记录 → 进度/计划/统计，以及书摘 CRUD/收藏、演示数据、主题、JSON 导出/恢复均具有完整调用链，没有发现空实现或按钮未接命令。**仍有一个 P1 数据完整性缺陷：恢复前已启动的书摘收藏操作，可以在恢复完成后使用旧实体覆盖恢复的新书摘，界面同时提示恢复成功。** 因此不能给出“所有功能与边界均已打通”的无条件结论。

该缺陷在实际应用 Release Library、真实 SQLite、实际 VM 命令和单线程同步上下文下完成 5 次受控时序复现，5 次均出现相同覆盖。完整证据见 `docs/evidence/business-recheck-20261020/`。此证据属于服务和 VM 集成验证；没有把它冒称为实际 Avalonia 窗口点击复现。

## 正常调用链复核

| 功能 | 直接核对到的路径与保护 |
|---|---|
| 新增/修改书籍 | BookList Add/Open → 内容导航 → BookEdit.Load/Save → IBookStorage → DatabaseService.WriteAsync；存储检查标题、总页数和最大关联页码，维护创建时间，返回时重载 |
| 阅读记录与进度 | BookDetail Add/Edit → 记录弹窗 → ReadingRecordEdit.Save → ReadingRecordStorage；记录采用闭区间页数，进度取最大结束页，最早读至书末的日期为完成日；新增、修改、删除记录均检验历史计划一致性 |
| 删除书籍 | BookDetail 捕获 ID/ParameterVersion，获取关联影响前后和确认后核对当前上下文；事务删除 Book 后由外键级联清理记录、书摘和计划；旧操作结束不弹出新页面 |
| 书摘搜索/编辑/删除 | QuoteList XAML 对应实际命令；QuoteEdit 装载书籍选项并保存；QuoteStorage 检验父书、正文和页码；收藏正常路径可用，但跨恢复竞态见下文 |
| 计划及建议 | ReadingPlanEdit → ReadingPlanStorage；新建禁止完成书籍和重复计划；已存在计划保留开始日期；状态与每日建议由剩余页数和目标日期计算 |
| 统计 | StatisticsService 在单次 ReadAsync 中获取书籍与记录，long 汇总页数/分钟数，生成近七天活动与完成书数 |
| 备份恢复 | BackupService 完整校验必要字段、ID、父子关联、页码和历史计划一致性；事务内先自动备份当前四表，再替换实体，恢复保留 ID；文件失败不进行替换。但单个事务不能排除事务后旧 VM 的下一次写入 |
| 编辑与单例重载 | EditViewModelBase 首次加载门禁、字段合并、提交快照、ID 回填、离开保存确认俱在；ViewModelBase.OnLoaded 等待旧操作并按 ParameterVersion 重载最新参数 |
| 设置操作门禁 | SettingsViewModel.CanLeaveAsync 在自身 IsBusy 时拒绝离开，防止恢复中进入新页面；该保护不能阻止恢复前已经从其他页面启动的操作 |

上述表格是本次直接代码核对；正常真实 UI 与全套测试结果由主审单独汇总，不将其替代本次竞态证据。

## P1：旧书摘收藏操作跨越恢复边界，静默覆盖新数据

### 根因位置

- `Code/ReadJournal.Library/ViewModels/QuoteListViewModel.cs:22`：收藏由 `GetAsync` → 修改实体 `IsFavorite` → `SaveAsync` 两次独立数据库操作组成；第二次提交携带读取时的完整旧实体。
- `Code/ReadJournal.Library/Services/QuoteStorage.cs:16`：SaveAsync 仅校验当前 ID 和父书存在；`:22` 使用 `c.Update(quote)` 更新所有字段，没有恢复代次或实体版本校验。同 ID 在恢复后被保留，旧实体仍能通过校验。
- `Code/ReadJournal.Library/ViewModels/ViewModelBase.cs:29`：默认 CanLeaveAsync 总返回 true，QuoteList 没有覆写。因此收藏 IsBusy 时可由实际 MainViewModel 菜单命令离开。
- `Code/ReadJournal.Library/ViewModels/MainViewModel.cs:29`：菜单经当前 Content.CanLeaveAsync 允许离开；当前列表 IsBusy 未拦截。
- `Code/ReadJournal.Library/Services/BackupService.cs:17`：恢复事务本身串行，但旧操作的后半次 Save 可以排在该事务之后；自动备份无法阻止后续旧写入。
- `Code/ReadJournal.Library/Services/DatabaseService.cs:48`：每次操作先经 InitializeAsync 取得/释放 gate，再独立等待实际操作 gate。受控队列验证中，既有读取、恢复写入与旧收藏保存可以形成“读旧值 → 恢复 → 存旧值”的合法串行次序。

### 确定性复现步骤

1. 真实 SQLite 中建书籍 ID=1、书摘 ID=1；旧书摘为 `Text=OLD quote text`、`Thoughts=OLD thoughts`、`Page=1`、`IsFavorite=false`。
2. 准备合法备份，保留相同 ID，改书名为 `RESTORED book`，书摘为 `RESTORED quote text`、`RESTORED thoughts`、`Page=2`、`IsFavorite=false`。
3. 在真实 DatabaseService 的一次额外无修改读取 `ReadAsync(c => 0)` 内持有 gate，以门闩扩大慢 I/O 的排队窗口。这不是暂停收藏 Get 返回后的任意位置，也没有手动调用收藏 Save。
4. 执行真实 `QuoteList.ToggleFavoriteCommand`，它的 Get 进入数据库队列；此时列表 IsBusy=true。
5. 执行实际 `Main.OnMenuTappedCommand` 切换到设置；记录 `CanLeaveWhileBusy=true`，实际当前 Content 已换到 Settings。
6. 执行实际 `Settings.RestoreCommand`，文件选择和确认替身立即返回合法路径/同意，恢复进入数据库队列。
7. 释放第 3 步门闩，后续真实服务和 VM 自行执行。单线程 SynchronizationContext 运行所有 VM 续体，未并发手调 UI 属性。
8. 等待收藏与恢复命令都完成，再通过实际存储读取并在进程退出后用 Python sqlite3 直接检查落盘结果。

### 结果

| 字段/状态 | 应保留的备份值 | 实际结果 |
|---|---|---|
| 书名 | RESTORED book | RESTORED book |
| 书摘正文 | RESTORED quote text | **OLD quote text** |
| 书摘感想 | RESTORED thoughts | **OLD thoughts** |
| 书摘页码 | 2 | **1** |
| 收藏状态 | 恢复前旧操作不应继续影响恢复后的数据 | true |
| 设置提示 | 不得在恢复内容被静默覆盖时无提示完成 | “恢复成功。原数据已自动备份至：…” |
| Settings.Error / QuoteList.Error | 应拒绝过期操作或保证恢复内容完整 | 均为空 |

5 次运行全部同样覆盖，SQLite 进程外读取也确认正文、感想、页码被旧值持久化。期望行为断言 `AssertExpected.py` 实际返回 **1**，失败信息为恢复成功后正文仍为 `OLD quote text`。它是本次新增复现，不属于原有 83 项永久测试。

### 证据层级与前提

- 实际使用：当前编译的 ReadJournal.Library、DatabaseService、SQLite 数据库文件、QuoteStorage、BackupService、QuoteListViewModel、SettingsViewModel、MainViewModel、菜单命令和实体规则。
- 测试适配：菜单服务替身执行与实际服务相同的 SetParameter → SetMenuAndContent；文件服务使用内存 JSON；提示服务直接同意；同步上下文使用单线程队列；数据库装饰器只暂停一次额外无修改读取。
- 前提：有较慢的在途数据库操作，使用户能在旧收藏完成之前进入设置、选择文件并确认恢复；备份包含同 ID 书摘。旧页面多阶段异步读取可在离开后继续排队，当前无全应用操作屏障。
- 5/5 是受控排队验证结果，不表示真实操作的发生概率。未在实际 Avalonia 窗口或 Windows 上点击复现此竞态。
- 先前探索中，若仅暂停已经持锁的收藏 Get，恢复最后完成，未发生覆盖；该探索未作为最终失败证据保存。最终证据只包含额外无修改读持锁的确认方案。

### 修复建议（本轮未执行）

应在业务恢复边界协调所有在途修改，防止旧操作在恢复后继续写入。可选择全应用修改操作登记/排空，或恢复代次及事务内过期校验；同时统一菜单、内容页跳转与关闭的忙碌门禁。仅禁止离开设置页不能保护恢复前已启动的操作；仅把收藏改为局部更新虽然能避免覆盖正文，仍可能把旧收藏意图施加到恢复后同 ID 的其他书摘。采用任何方案都应把本复现转为永久回归，并检查书籍、记录、计划和删除的同类恢复边界。

## 复跑与范围

运行前先完成 Code 的 Release 构建。SDK 路径按本机安装位置调整：

```sh
/workspace/scratch/dotnet/dotnet run --project docs/evidence/business-recheck-20261020/Probe.csproj -c Release
python docs/evidence/business-recheck-20261020/AssertExpected.py /workspace/scratch/recheck-business/result.json
```

第二条命令在 `0313917` 上预期退出 1，证明期望行为尚未满足。探针数据库和运行文件位于 scratch，不进入交付包；原始源码哈希在 `validation.json`。探针工程只引用已构建 Library DLL，无 ProjectReference，不会重编译或改写应用。

本轮未修改 Code、未重新打包、未更改正式报告、未推送远端。该 P1 尚未修复；Windows 实机、所有多进程/异常断电场景未验证。
