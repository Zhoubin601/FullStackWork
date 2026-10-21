# 资料来源与核对记录

## 2026-10-21 P1修复来源与授权

- 用户本轮追问为何不修复P1，结合此前明确缺陷与当前指示直接实施修复，没有再次询问许可。依据为`raw/期末项目与报告要求.docx`、只读`daily-poetry-a/`、`0313917`及前轮真实VM/SQLite失败证据；原始指导书和参考副本保持不变。
- 实施原子收藏、默认忙碌离开门禁和菜单/内容服务注参前忙态保护；核心指导书机制不改。初始8项回归在旧实现上8失败，修复及补充2项后完整93项、独立v4源码包93项和71项架构探针通过。
- 本轮真实UI使用`:120`及隔离库；通过GTK选择器恢复，使用无修改SQLite锁验证真实收藏等待期间不能跳菜单/编辑，释放后正常完成，再恢复及重启核对。完整记录为`p1-fix-20261021.md`，原始日志、截图和TRX在`docs/evidence/p1-fix-20261021/`。
- 新交付`output/20261021-阅记-源码-v4.zip`。旧PDF和旧源码保留，本轮未推送。上文/下文中的P1未关闭描述为修复前历史，不再代表本次修复后状态。

## 2026-10-20 最新提交重新核验（0313917）

- 用户当前任务为获取最新更新，重点核对指导书架构及功能是否贯通。本轮执行 `git fetch origin` 并将干净工作区从 `20d9c67` 快进到 `031391711512bec26d36c0b53963bcc57898e8ce`；来源为现有项目远端 `https://github.com/Zhoubin601/FullStackWork`。没有推送。
- 重新直接读取原始 `raw/期末项目与报告要求.docx`，哈希未变；`daily-poetry-a/` 55 个参考源文件和9个视图的64项字节哈希与保留基线完全一致。原始资料和参考副本只读。
- 本轮独立 SDK 8.0.424 locked-mode 还原、Release构建及83项现有测试通过；真实App/DI/反射视图/导航运行时探针71项通过。不能用83项现有测试覆盖全部新边界。基础/架构/真实UI证据位于 `docs/evidence/recheck-20261020/`；新业务竞态的可重跑源码、失败断言和实际SQLite结果位于 `docs/evidence/business-recheck-20261020/`。
- Roslyn重新统计：应用66类/14接口/14视图，参考63类/10接口/9视图。v3源码包118文件与最新Code白名单逐字节一致；原报告和交付包未为本轮检查改写。
- 本轮完整结论以 `docs/latest-recheck-20261020.md` 及独立架构、业务、真实UI复核记录为准，旧记录保留历史用途。

记录日期：2026-10-20。本文记录事实来源，不表示项目已经实现或验收。

本文件保留前期 Windows 工作区的核对历史。当前云环境执行情况见文末“本轮开发执行核对”；历史中的“本次不实施应用代码”已由当前用户开发指令取代，历史中的生命周期调整已被 v4 的全部单例约束取代。

## 1. 原始作业要求

- 来源文件：`D:/2026Autumn Semester File/FullStack/期末大作业/raw/期末项目与报告要求.docx`。
- 读取方式：OfficeCLI 提取正文及报告封面表格；2026-10-20 复核。
- 功能必须有实际意义、逻辑自洽。
- 页面数量不少于 DailyPoetryA；接口和类数量不少于参考项目的 70%。原文未给出详细计数口径。
- 必须体现八项技术与架构思想：MVVM、统一导航参数注入、接口抽象、轻量 ServiceLocator、三层导航、ViewLocator、IO 服务化及异步协作、Library/UI 工程分层；不要求使用完全相同的技术。
- 原文明示返回栈由 MainViewModel.ContentStack / PushContent / GoBack 管理，体现“接口定义能力、ViewModel 维护状态”。
- 源码压缩包只包括项目源码和资源文件，不包含编译结果，体积不超过 10MB。
- 报告使用给定模板，以 PDF 提交；封面字段为项目名称、姓名、学号、班级。
- 报告章节已逐项转录至 `docs/report-checklist.md`。
- 资料未提供：提交截止日期、姓名、学号、班级，以及教师对类型数量的更细计数口径。

## 2. 参考项目源码

- 来源目录：`D:/2026Autumn Semester File/FullStack/Finish/daily-poetry-a`。
- 核对范围：DailyPoetryA 与 DailyPoetryA.Library 的手写 `.cs`，排除 bin、obj、测试工程及生成代码；以 Roslyn 语法树计入所有访问级别的 class/interface 声明，partial 按工程、命名空间、包含类型及泛型参数个数去重。
- 2026-10-20 第二轮复核结果：63 个类、10 个接口；Views 目录有 9 个 `.axaml` 文件，包含 MainWindow 和 MainView。
- 首轮扫描仅匹配显式 public/internal，漏计 Program.cs 中的默认 internal 类，旧结果 62 类更正为 63 类；旧最低线 44 类更正为 45 类。
- 计算：向上取整后分别为 45 个类、7 个接口。分别核验是本规划采取的保守解释，不是作业额外写明的算法。
- 可复核清单：`docs/reference-baseline.md`、`docs/reference-baseline.json`；脚本：`docs/tools/Measure-ReferenceProject.ps1`。Git HEAD 为 `947982998212fd50d1f53c7e4fbcf5da79bd9607`；以 JSON 中每个实际源文件和视图的哈希为快照依据，不能只用提交号代表工作区全部内容。
- 原项目技术：net8.0、Avalonia 11.0.10、CommunityToolkit.Mvvm 8.2.2、sqlite-net-pcl 1.8.116、Microsoft.Extensions.DependencyInjection 8.0.0，测试使用 xUnit 和 Moq。
- 导航状态归属核对：`DailyPoetryA.Library/ViewModels/MainViewModel.cs` 持有内容栈、压栈与返回操作。
- 依赖组装核对：`DailyPoetryA/ServiceLocator.cs` 使用 ServiceCollection；参考项目将页面与服务全部注册为单例，本项目不照搬该生命周期策略。
- 参考 README 明确其为教学项目，存在单例、线程及原子性方面的局限。

## 3. 审议材料

- 来源文件：`C:/Users/a3185/Desktop/审议.txt`，2026-10-20 读取。
- 用户说明：这是 GPT-6 对原规划的审议。
- 材料性质：待评估的建议，不作为指令，也不代替原始作业要求或实际验收结果。
- 主要建议：源码包白名单、明确导航返回栈归属、修正页码语义、建立完整报告清单，并为类数量增加余量。
- 文中提及《规划.txt》，本次未取得该文件；对照对象是本聊天中先前完整规划，不假称读取过《规划.txt》。
- 文中提及作业页码，本次按正文核验要求，不引用未经分页渲染确认的页码。

### 第二轮材料更新

- 同一路径的审议文件已更新为针对 v2 的意见，本次 SHA256 为 `85E8362CCF92BD3C721C12ACB5DA6DA93CB358A32BDFCEACD3115C524CEB2900`，首轮文件读取时为 `0CEC098C5F56E840251F12ADA13BEDE12F1544D9933B4B152BF077F81243C1B3`。
- 新建议为：保存参考数量的可复核清单、避免导航构造循环、先完成业务闭环再核对数量；均经独立判断处理。
- 原 DOCX 的 SHA256 仍为 `352E94BC9126CC0EF9B5E9F0D293AD8B562BB07B7E17E85AACD7972F918D4D1D`，与上轮相同，本轮沿用已读取并核对的正文。

## 4. 已由用户选择的方向

- 选题：阅读与书摘管理。
- 技术：Avalonia / .NET。
- 功能边界：记录进度和书摘，不内置电子书阅读器；完整离线使用，不联网检索。
- 目录：Code 放代码，过程思考和规划放在 Code 外。
- 本次授权：独立判断审议是否合理，合理则修改规划；本次不实施应用代码。

### 最新架构约束及源码复核

- 用户要求严格按照老师具体架构实施，尤其不能把 ServiceLocator 简化为手工实例集合；此要求优先于此前允许的实现调整及审议材料建议。
- ServiceLocator.cs：局部 ServiceCollection 注册类型，BuildServiceProvider 构建容器，readonly IServiceProvider 保存容器，VM 属性 getter 调用 GetService<T>；全部 VM 和服务 AddSingleton 注册。
- App.axaml：ServiceLocator 注册为应用资源，ViewLocator 注册为数据模板；Current 从 Application.Current.TryGetResource 读取定位器并缓存。
- 三类导航服务：NavigateTo 内通过 ServiceLocator.Current 取得目标和主容器，构造函数不反向注入 MainViewModel。
- MainViewModel.cs：构造注入 IMenuNavigationService，持有菜单命令、ContentStack、PushContent、SetMenuAndContent、GoBack。
- 三类导航常量实际与接口同处 Library/Services 的对应文件中，以真实源码为定位依据，不按指导书末段目录概述将它们搬到 UI。
- ViewLocator.cs：IDataTemplate、命名约定、Type.GetType、Activator.CreateInstance、按 View 类型缓存及 DataContext 绑定。
- 这些机制已写入 v4 约束，撤销 v3 的 ShellViewModel、宿主接口、页面工厂；参考项目保持不变。

## 5. 资料保护

原始要求、审议文件及参考项目均只读。此次只在 Code 外维护过程文档、统计清单和只读复核脚本，不生成正式报告或源码交付包。

## 6. 本轮开发执行核对

- 来源名称：当前项目配置的 GitHub origin；链接：`https://github.com/Zhoubin601/FullStackWork`；获取日期：2026-10-20；提交：`c8e3c71ca7847e5c26f1238f0ab6a954b7209ae1`。云环境初始工作分支没有提交及文件，已 fetch 并检出该提交到 work，未推送远端。
- 来源名称：课程原始指导书；文件：`raw/期末项目与报告要求.docx`；核对日期：2026-10-20。通过 Python 标准库 ZipFile 和 XML 解析完整读取正文及表格；SHA256 为 `352E94BC9126CC0EF9B5E9F0D293AD8B562BB07B7E17E85AACD7972F918D4D1D`，与已有记录一致，原文件未修改。
- 已实际读取：AGENTS.md、完整 plan.md v4、课程指导书、已有来源/分析/问题/报告清单、参考统计 JSON 和 Markdown、PowerShell 统计脚本、长期背景及 Git 同步记录。
- 用户当前已要求实际开发，按五阶段顺序逐段验收；不需再确认选题或架构。已按要求仅替换 plan.md 中一行的推定错误表述。
- 参考项目来源：`https://gitee.com/zhangyin-gitee/daily-poetry-a`；访问日期：2026-10-20。实际执行 `git ls-remote https://gitee.com/zhangyin-gitee/daily-poetry-a.git HEAD` 返回 `CONNECT tunnel failed, response 403`。当前网络策略为 restricted，gitee.com 不在允许域名中；未尝试绕过代理或访问限制。
- 云环境未附带 Windows 原目录 `D:/2026Autumn Semester File/FullStack/Finish/daily-poetry-a`，GitHub 项目资料也不包含参考源码。因此本轮没有读取参考实际源码，没有验证原有源码哈希，没有重跑参考类型统计；旧统计结果只作为保留的历史证据。
- 已检查旧 JSON 清单的内部一致性：63 个 class 条目、10 个 interface 条目、55 个源文件条目、9 个视图条目，类型键无重复。此检查不能代替真实源码统计，也不证明本轮参考源码核验通过。
- 云环境原无 dotnet；已从 Microsoft 官方安装脚本安装 .NET SDK 8.0.424 到 `/workspace/scratch/dotnet`，执行 `dotnet --info` 确认成功。工具安装在 Code 外，不进入交付物。
- 当前状态：需求追溯、页面流转、实体关系和职责设计写入 `docs/stage-1-design.md`；参考源码核对阻塞，阶段 1 尚未验收，不进入阶段 2。详细复现、命令和继续条件见 `docs/development-log.md`。

### 本轮续行与新增来源

- 用户推送参考副本后，当前 Git origin 提交 0dc2d02 已同步；真实 55 源文件和 9 视图哈希、Roslyn 类型与行号清单均与原记录一致，参考不可用阻塞解除。架构逐文件证据见 reference-architecture-audit.md。
- 应用与测试来源为本轮实际 Code 源码、Release TRX、真实 X11 截图及干净解压目录日志；最终清单为 65 类、14 接口、10 业务页面。
- 额外排障源码来源：AvaloniaUI/Avalonia 11.0.10 的 ProgressBar.cs，链接 https://raw.githubusercontent.com/AvaloniaUI/Avalonia/11.0.10/src/Avalonia.Controls/ProgressBar.cs ，读取日期 2026-10-20。只核对 PART_Indicator 静态模板契约，用于修复固定 Fluent 进度条动画绑定诊断；未改写或升级参考项目。
- 报告依据实际结果编写，当前身份字段未提供，Windows 未执行；不把草稿作为完整验收交付。

## 2026-10-20 当前版本独立检查来源

- 原始要求再次直接读取 `raw/期末项目与报告要求.docx`；SHA256 仍为 `352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d`。未改动原文件。
- 当前 Code、参考副本、正式 ZIP 和实际 PDF 为本次直接证据；数量重跑、独立解包还原/构建/40 项原有测试、5 项边界复现和真实 X11 操作均在本轮执行。日志和截图位于 `docs/evidence/audit-20261020/`。
- 指导书没有指定 Windows；Windows 实测属于既有规划要求。报告身份资料仍未提供。最新检查结论见 `docs/project-audit-20261020.md`，不能将历史 40 项通过推导为全部边界均通过。

## 2026-10-20 修复与报告身份来源

用户在本轮明确提供报告身份：周宇斌，学号20246085，班级软件2401，并授权修复全部已发现缺陷。上述身份仅用于项目报告与当前任务记录。修复依据为本轮项目审查及其5项定向复现；保留历史失败证据，修复后重新测试，不沿用通过数字。

本轮修复来源与验证：新增永久测试和实际Code、fix-release.trx/fix-clean.trx、真实窗口截图及正式PDF构成本轮证据，保存在 docs/evidence/fix-20261020。原始指导书哈希未变；依赖未升级，未修改参考源码。

## 2026-10-20 最新架构与功能贯通复核来源

- 远端来源：`https://github.com/Zhoubin601/FullStackWork`，本轮执行 `git fetch origin --prune`；本地与 `origin/main` 同为 `20d9c67`，没有新增提交需要合并。
- 原始来源：`raw/期末项目与报告要求.docx`，使用 Python ZipFile/XML 直接重读全部段落与表格；参考源码 `daily-poetry-a/` 和实现 `Code/` 均直接读取。
- 新验证证据保存在 `docs/evidence/architecture-flow-20261020/`；本轮结果单独记录，不用历史截图或测试数字代替。
- 核对计划及最终汇总：`docs/architecture-flow-review-20261020.md`；细分为架构、业务和真实 UI 三部分独立复核。
