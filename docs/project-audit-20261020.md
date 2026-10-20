# 当前项目合规性与功能贯通检查

> 本文件为修复前的历史审查。用户随后已授权修复，当前状态和复验结果见 [缺陷修复记录](repair-results-20261020.md)。


日期：2026-10-20。检查对象：当前 `Code/`、原生参考副本 `daily-poetry-a/`、源码交付包和报告草稿。

## 结论

**基础数量、工程架构和源码包符合指导书；正常业务主流程在本轮实际桌面运行中可用，但不能认定功能已经完全打通，也不能认定作业交付已完成。**

本轮从现有正式 ZIP 解压，在独立目录还原、构建、运行原有测试，40 项全部通过。随后以正确行为为断言增加 5 项定向检查，全部复现失败，对应 3 类实际缺陷：加载覆盖输入、保存期间输入丢失、计划状态变化导致自己的备份不可恢复。另有部分编辑页加载失败缺少页内重试入口。

报告草稿的模板章节完整，但封面姓名、学号、班级仍未填，`output/` 没有正式 PDF。Windows 实测尚缺；这是既有项目规划的验收项，原始指导书未指定操作系统。

本轮只新增检查记录、诊断用例及运行证据，未修改应用源码、原有测试、参考源码、原始资料或正式 ZIP/PDF。

## 依据与已满足的指导书条款

原始依据：`raw/期末项目与报告要求.docx`，本轮直接解析 DOCX XML；原文件 SHA256 为 `352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d`。业务正确性另按 `docs/plan.md` v4 核对，不把规划附加要求混称为指导书原文。

| 项目 | 本轮检查结果 |
|---|---|
| 页面 | 参考 9 个视图；当前 10 个业务页面，另有初始化、主容器、主窗口和记录弹窗，共 14 个视图文件 |
| 类与接口 | Roslyn 同口径重扫：参考 63 类/10 接口，最低 45 类/7 接口；当前 65 类/14 接口 |
| 架构 | MVVM、统一 SetParameter、接口和构造注入、真实 IoC ServiceLocator、三层导航及主 VM 返回栈、反射缓存 ViewLocator、异步 IO 服务、Library/UI 分层均有当前实现证据 |
| 源码包 | `output/20261020-阅记-源码-v1.zip` 为 93,138 字节、110 文件，无 bin/obj/编译产物；与当前 Code 白名单文件逐文件字节一致 |
| 报告结构 | 实际 PDF 草稿 18 页，所需章节齐全，14 张截图已嵌入 |
| 报告交付 | 尚未完成：封面身份缺失，正式 PDF 未交付 |

完整逐条映射和源码行号见 [指导书合规复核](requirements-audit-20261020.md)。ZIP SHA256：`ae11c13d96614e8b127a0db2e5735fcbbf1a002bc9710e2064068f57c411dee4`。

## 本轮实际验证

环境为 Debian 13 / linux-x64、.NET SDK 8.0.424、运行时 8.0.30、Release。使用 Xvfb 的实际 X11 窗口操作，未用 Headless 或直接写数据库代替 UI 录入。

| 检查 | 结果与边界 |
|---|---|
| 独立解包还原 | 新建 NuGet 包目录，执行 `dotnet restore ReadJournal.sln --locked-mode --no-cache`，退出 0；初次还原使用网络 |
| Release 构建 | 退出 0，0 错误；3 条固定 SQLite 依赖的 NETSDK1206 旧 Alpine RID 警告 |
| 原有测试 | 40 通过、0 失败、0 跳过，包含真实 IoC、导航、SQLite、业务规则、恢复和回滚检查 |
| 新增诊断 | 5 项均失败，证明下面列出的缺陷；不属于原有 40 项测试，不把失败记作验收通过 |
| 实际新增书籍 | 从书库进入编辑，录入 Audit Book A、100 页，保存后列表和详情可见 |
| 实际阅读记录 | 在详情弹窗录入第 10～20 页、15 分钟；详情和统计显示当前进度 20/100、累计 11 页/15 分钟 |
| 实际书摘 | 从详情录入摘录、感想、第 20 页并收藏，返回详情和书摘列表可见 |
| 实际计划 | 从详情建立当天到期计划，建议每日 80 页；计划列表和概览一致 |
| 实际设置与备份 | 通过系统文件选择器导出 JSON，包含书籍/记录/书摘/计划各 1 条；选回该文件并确认恢复成功，自动备份存在；主题可切换并持久化 |
| 实际重启 | 正常关闭后再次启动，书籍、进度、记录、计划、书摘及深色偏好保留 |
| 页面与绑定 | 本轮访问全部 10 个业务页面，首次及重启日志的 Binding 诊断共 0 条 |

本轮只读 SQLite 核实 UI 落库内容。数据库版本为 1；实际备份、统计值及记录详情保存在 [validation.json](evidence/audit-20261020/validation.json)。图形环境缺少会话管理器和 DBus，GLX 软件渲染被框架拒绝后回退；真实操作仍成功。该环境结果不能代替 Windows 桌面验收。

构建及原有测试证据：[还原](evidence/audit-20261020/restore.log)、[构建](evidence/audit-20261020/build.log)、[40 项结果](evidence/audit-20261020/test.log)、[原始 TRX](evidence/audit-20261020/audit-baseline.trx)。UI 证据示例：[正常流程后的概览](evidence/audit-20261020/ui-overview-after.png)、[统计](evidence/audit-20261020/ui-statistics.png)、[恢复成功](evidence/audit-20261020/ui-restored.png)、[重启后详情](evidence/audit-20261020/ui-restarted-detail.png)。

## 未通过项与复现

### 1. 编辑加载可能覆盖用户输入（P2）

位置：`Code/ReadJournal.Library/ViewModels/BookEditViewModel.cs:19`、`QuoteEditViewModel.cs:21`、`ReadingPlanEditViewModel.cs:23`。

打开已有书籍编辑，读取尚未完成时输入新书名，随后旧书名会覆盖输入。新增书摘在书籍选项载入前输入正文，也会被空正文覆盖。加载前检查 IsDirty，但 await 后只检查参数版本，且输入控件未在忙碌时禁用。

本轮通过 TaskCompletionSource 固定存储返回时序，分别复现了书名从新输入回到旧值、书摘正文从输入回到空字符串。计划编辑存在相同代码模式，本轮未为计划再重复增加时序用例。

### 2. 保存期间的新输入未落库却被标为已保存（P2）

位置：`Code/ReadJournal.Library/ViewModels/EditViewModelBase.cs:15`、`:20`、`:29`，以 `BookEditViewModel.cs:26` 为实际提交路径。

点击保存后，存储返回前继续修改书名；提交实体仍为点击时的值，保存完成后 AcceptChanges 却取当前表单的新值，并自动返回。新增输入因此未保存，也不再触发未保存提醒。使用受控存储等待复现，实际 IsDirty 为 false，与应保留未保存修改的断言不符。

以上两项为异步等待边界缺陷；本轮使用模拟存储固定时序验证，未宣称快速本地磁盘上每次手动点击都能触发。

### 3. 计划状态可破坏备份恢复往返（P2）

位置：`Code/ReadJournal.Library/Services/ReadingPlanStorage.cs:21`、`:22`，`ReadingRecordStorage.cs:15`，`BackupService.cs:51`。

复现步骤：

1. A、B 两本书各创建一个计划。
2. 新增 A 的读至书末记录，A 计划变为完成。
3. 编辑 B 的已有计划，把关联书籍改为 A。当前实现允许 A 保留两个完成计划。
4. 删除 A 的完成记录，A 的两个计划同时变为未完成。
5. 导出备份成功，但该 JSON 不能通过同一应用的恢复前校验，错误为“同一本书有多个未完成计划”。

本轮两项检查使用真实临时 SQLite 分别证明重复计划出现和导出结果被自身 Validate 拒绝；导出文件服务以 mock 捕获 JSON，不涉及绕过业务存储或伪造数据库。

### 4. 编辑页加载失败缺少页内重试（P3）

`Code/ReadJournal/Views/BookEditView.axaml:3`、`QuoteEditView.axaml:3` 只有错误展示和保存入口；用户须返回后重进。计划编辑页已有重新加载按钮。此项为代码和 XAML 检查结论，本轮未额外注入桌面存储错误。

### 5. 报告尚未达到正式提交状态

`docs/report/report.tex:22` 及草稿 PDF 第一页的姓名、学号、班级均为“资料未提供”。需补齐真实信息后核对最终 PDF。已有正文结构完整，不能据此将草稿算作正式交付完成。

完整代码审查见 [功能连通性审查](flow-code-audit-20261020.md)。5 项诊断源码与复现方法保留在 [audit-regressions](tools/audit-regressions/README.md)，结果见 [失败日志](evidence/audit-20261020/regressions-final.log) 和 [TRX](evidence/audit-20261020/audit-regressions-final.trx)。

## 后续验收顺序

先补齐异步表单保护及计划状态约束，使 5 项诊断转绿；随后重跑原有 40 项测试和相关实际 UI 流程，重新生成并验证源码包。为书籍、书摘编辑补充失败重试。按已有规划补 Windows 实测。用户提供并复核姓名、学号和班级后，再完成正式 PDF。上述修复和定稿不在本轮“检查”中实施。
