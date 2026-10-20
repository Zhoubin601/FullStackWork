# 最新架构与功能贯通复核

日期：2026-10-20。本轮用户要求获取最新更新，重点检查架构是否严格对应指导书、功能是否打通。

## 最终结论

已获取远端最新状态（20d9c67，无新增提交）。本轮发现并修复 **1 处架构职责越层、4 类异步流程问题**；修复后八项架构要求均与指导书和真实参考机制对应，核心业务链路在 Linux 真实桌面实测贯通。Release 83 项及源码 v3 独立解包 83 项全部通过，最终 DLL 的架构探针 71 项通过，真实窗口绑定诊断 0 条。

Windows 未实测。检查完成后，用户已明确授权将本轮成果提交并推送到 origin/main，同步范围与核验方式见 `docs/git-sync.md`。原始资料、参考源码及旧版交付均未改写。

## 范围和依据

- 已执行 `git fetch origin --prune`；本地 `work`、远端 `origin/main` 均为 `20d9c67`，没有待合并更新。初始工作区干净。
- 原始依据：只读 `raw/期末项目与报告要求.docx`；参考依据：只读 `daily-poetry-a/`；实现依据：`Code/` 实际源码。
- 延续已确认的严格约束：真实 IoC 容器、全部服务/VM 单例、应用资源定位器、三层导航、统一参数入口、主 VM 返回栈、反射缓存 ViewLocator 及 Library/UI 分层。
- 历史记录只作为线索。本轮重新安装 SDK、锁定依赖还原、Release 构建、运行自动化测试，并用隔离数据和独立 X11 会话验证真实窗口。

## 执行计划

1. 同步远端并直接读取原始指导书（已完成）。
2. 并行核对架构、业务边界及真实 UI 流程。
3. 对确定缺陷修复并回归，保留修复前复现证据。
4. 汇总严格对应关系、已打通链路和验证边界。

## 修复与当前验证

原版本的核心架构机制与参考一致，但概览页在数据库服务委托中直接编写 SQLite 查询，尚未完全遵守“取数据逻辑从 VM 剥离”的严格职责要求。本轮将其迁入现有 IReadingRecordStorage/ReadingRecordStorage，OverviewViewModel 改为注入业务存储接口；没有增设壳 VM、页面工厂或替代容器。

| 本轮问题 | 已实施修复 | 永久回归 |
|---|---|---|
| 概览 VM 委托中直接查询表 | 最近记录查询及跨书籍关联移至阅读记录存储服务 | OverviewArchitectureTests：3 项，包含真实 SQLite 排序/关联/上限、空库、业务接口注入 |
| 旧删除读取/确认等待期间，单例新参数覆盖目标 ID | 删除开始固定 ID/参数版本；每个提交前 await 后检查页面身份和版本，失效则取消 | DeleteNavigationRaceTests |
| 旧删除提交完成后无条件返回，绕过新页面草稿确认 | 只允许仍属原详情上下文的操作执行返回 | 同上；6 项覆盖删除等待、确认、提交、同实例重开和正常成功 |
| 新页面 Loaded 被旧操作 IsBusy 吞掉，详情保持空白 | Loaded 等待当前操作结束，再读取最新参数；其他业务命令继续防重入 | BusyLoadRegressionTests：2 项；另有新书详情回归 |
| 恢复全库时仍可切菜单/关闭窗口 | 设置页操作期间 CanLeaveAsync 拒绝离开，成功/失败后释放 | SettingsNavigationTests：4 项 |

已完成锁定依赖还原和 Release 构建；原有 68 项加本轮 15 项，共 **83 项通过、0 失败、0 跳过**。本轮实际 TRX 为 `docs/evidence/architecture-flow-20261020/final.trx`，日志为同目录 `test-final.log`。修复前和部分修复后的失败证据均保留，不用最终通过覆盖原始复现。

数量重新按 Roslyn 手写类型口径计算：应用 66 类、14 接口、14 个 XAML 视图，其中 10 个为业务页面；参考 63 类、10 接口、9 个视图。保守最低线为 45 类、7 接口、9 视图，全部达到。不要使用统计工具对“应用自身数量”计算出的 70% 作为课程最低线。

原始指导书哈希未变；参考的 55 个 C# 文件及 9 个视图共 64 个文件与历史字节哈希一致。新证据为 `source-verification.json` 和两个 `*-inventory.json`。

新版源码：`output/20261020-阅记-源码-v3.zip`，118 个显式白名单文件，109,424 字节，SHA256 `089ca8f42b4777cb290cd0ee119214ac365b015d2e92ebc01a1d36a2faf6e8a0`。旧包和既有 PDF 均保留。既有 PDF 是前轮交付记录，本轮没有要求重做报告，本轮新增修复及 83 项结果以本文件和新日志为准。

## 独立解包与架构运行时复验

- 新版 ZIP 在独立临时目录解包，未复制 bin/obj；使用本轮首次还原填充的共享 NuGet 缓存执行 locked-mode 还原、Release 构建和测试，**83 项通过**。不能将此描述为另建空 NuGet 缓存或无依赖来源还原。
- 干净构建 0 错误，固定 SQLite 原生依赖产生 3 条既有 NETSDK1206 Alpine RID 警告；已验证 Linux x64，未验证 Alpine。
- ZIP 118 个文件与当前 Code 白名单逐文件字节一致，无编译结果或额外文件，详细结果为 `docs/evidence/architecture-flow-20261020/validation.json`。
- 最终 Release DLL 重新执行独立运行时架构探针，71 项断言全通过；JSON 记录所加载程序集哈希，与最终构建一致。覆盖实际容器、全部单例服务/VM、13 个反射视图及 12 个三层导航目标。
- 加强静态扫描覆盖通过委托推断的 Table/ReadAsync/WriteAsync/Query 等调用，当前 VM 查询及直接 IO 命中 0。依据脚本与证据在 `docs/evidence/strict-architecture-20261020/`，逐项对应见 `docs/architecture-strict-audit-20261020.md`。

## 真实桌面功能链路

使用独立 Xvfb :118 和全新隔离库，所有写入经真实窗口操作，SQLite 仅在事后只读核对。空库初始化、新建书籍、阅读记录、书摘收藏、阅读计划、概览/统计、系统文件选择器导出/恢复及重启全部完成，基线 10 个业务页面均实际可达。

切换到最终修复后的 Release 程序后，再次验证：

- 重启保留书籍、记录、书摘和计划，概览服务迁移后的最近记录正确显示 11 页 / 15 分钟。
- 作者修改保存返回成功；记录由第10～20页/15分钟改为第10～30页/25分钟，累计21页、当前进度30/120（25%）正确。
- 删除记录后重算；再删除书籍，Book/ReadingRecord/Quote/ReadingPlan 四表计数全部为 0，关联清理正确。
- 从先前系统选择器导出的 JSON 恢复，四表计数全部为 1，关联 ID、书摘收藏及计划日期一致；产生自动备份。恢复后正常离开设置到概览，书摘、计划和统计菜单均正常。

45 张本轮截图及两次有效运行日志保存在 `docs/evidence/architecture-flow-20261020/ui/`，其中 31～45 是最终二进制。有效日志的绑定诊断均为 0；最终程序集哈希与自动化及架构探针所用 DLL 一致。DBus/libX11 名称发现、SESSION_MANAGER 和软件 GLX 回退属于已记录环境提示，未冒称 GPU 或 Windows 验证。

逐流程桌面记录见 `docs/function-ui-audit-20261020.md`，最终四表数据、自动备份、程序集哈希及日志检查为 `ui/validation.json`；总机器验证记录为 `docs/evidence/architecture-flow-20261020/validation.json`。

## 本轮文件与验证边界

- 应用修改：`Code/ReadJournal.Library/Services/IReadingRecordStorage.cs`、`ReadingRecordStorage.cs`；`ViewModels/OverviewViewModel.cs`、`BookDetailViewModel.cs`、`ViewModelBase.cs`、`SettingsViewModel.cs`。
- 新永久测试：`Code/ReadJournal.UnitTest/` 内 DeleteNavigationRaceTests、BusyLoadRegressionTests、SettingsNavigationTests、OverviewArchitectureTests；更新 README 和白名单。
- 核查文档：本文件、严格架构对照、业务审查、UI审查；同步更新测试/交付清单、源码类型清单和来源/进度记录。
- 正式源码：`output/20261020-阅记-源码-v3.zip`。原始依据为 `raw/期末项目与报告要求.docx`，参考为 `daily-poetry-a/`，均保持只读。
- 用户可复核严格对照表与新源码；当前没有已确认但未修复的软件问题。结论覆盖本轮用例和 Linux x64 实测，不构成所有时序或平台的形式证明；Windows、Alpine、GPU 实测均未执行。既有 PDF 未为本轮重做；用户已授权将本轮成果同步至远端，实际提交/推送结果见同步记录与最终回复。
