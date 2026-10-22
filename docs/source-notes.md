# 资料来源与核对记录

## 2026-10-22 阅读器 P0 实施与验收

- 用户授权按阶段开始执行。依据 raw 中的总体规划/课程指导书、output/reader-plan-v2 三份规划及前轮复核；生产源码仍为 0280730 的逻辑，P0 实现与证据提交为 fef2575。
- 固定原 schema 1 建库数据和 JSON/SQL fixtures，实际服务校验全部字段与旧业务状态；新增两项永久回归。工作区与白名单干净源码各 96 项通过。
- 实际 Xvfb/Openbox 窗口完成空库/旧库各两轮正常启动关闭，退出码均0，默认数据目录不存在且未创建；首次无WM关闭超时单独保留，未伪装通过。15 个纯合成 TXT/EPUB 样本重复生成字节一致。
- 记录位置 docs/evidence/reader/P0/acceptance.md；外部访问仅项目拉取及微软/NuGet/Debian 构建运行依赖，日期 2026-10-22，无新增产品研究结论。raw、正式规划和现有 output 未改；Windows 字体/DPI未验证，P1未开始。

## 2026-10-22 拉取阅读器 v2 规划并独立复核

- 来源：只读 raw 中的原始总体规划和课程 DOCX；最新提交 `0280730` 的 `output/reader-plan-v2/` 三份文件、既有编制/评估记录、实际 Code。已 fetch 并从 2133786 快进到 origin/main；最新提交不含应用代码修改。
- 本轮独立检查三份规划摘要/链接及 79 个唯一验收编号；补齐临时 SDK 8.0.424 后实际锁定还原、Release 构建及 94 项现有测试通过，不再仅引用历史 TRX。未做新增分页实验、Windows 实机或真实 AI 调用。
- 发现拟实施契约两处待补：Reader 内重置需先结束活动会话；批次明细级联删除后，计数不能重建原始新增 ID 回执。后者按契约 FK 规则用内存 SQLite 验证，不能误称现有应用缺陷。
- 结论、主观成功率前提及来源链接见 `docs/20261022-阅读器v2计划执行复核-v1.md`；实际证据见 `docs/evidence/reader-plan-review-20261022/`。未改 raw、正式规划或既有交付，未开发/提交/推送。
- 外部访问限于授权的项目拉取与构建依赖：GitHub 项目 origin、微软 SDK 下载、NuGet 锁定包还原；未用新的外部技术资料论证可行性。引用/下载日期：2026-10-22。

## 2026-10-22 电子书扩展规划评估

- 来源：用户上传《阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md》（附件本地名带 `1-` 前缀）、当前 `Code/`、只读课程指导书和历史验收记录。
- 本地提交 `2133786762fcfcac1906f3b07afa5460fde9a998`；本轮查询现有 origin/main 返回相同提交。未修改应用代码、原规划、原始资料或正式交付物；未提交/推送。
- 已直接核对页数校验、最大页推断进度、计划状态、四表 JSON 备份、user_version=1、单例与视图缓存、导航及关闭门禁。实际解析两份已有 TRX，均 94 项通过；本轮未重新运行测试，未做分页实验。常见 SDK 路径及 PATH 未发现 dotnet。
- 来源名称：当前项目 origin；链接：`https://github.com/Zhoubin601/FullStackWork`；引用日期：2026-10-22。除此没有新增外部技术资料。可行性、主观概率前提、实施建议及待验证项见 `docs/20261022-电子书扩展可行性评估-v1.md`；概率不属于统计数据。

## 2026-10-21 统一v1交付完成

本轮依据原指导书、用户实际保存的Word v3及当前源码，生成`output/v1/`中的23页DOCX/PDF及119文件114395字节源码ZIP。未修改的用户正文、封面和两张SVG图保留；返回说明、94项测试与包元数据同步，图21更新、图22/23新增。独立源码94项通过、当前程序集71项探针通过、独立包空库实际启动正常；全部23页已渲染检查。

旧output内3个交付文件的永久删除被自动审批以“blocked by policy”拒绝，已改为验证路径/哈希后移至`docs/report/release-v1/previous-deliveries/`。output只保留v1，旧文件没有永久删除。完整结果与限制见`release-v1-20261021.md`及`docs/report/release-v1/{validation,cleanup}.json`。原始指导书哈希不变，无外部资料。

## 2026-10-21 统一v1交付授权与来源

用户明确要求修改报告、新源码包、清除旧交付并统一放入v1目录。本轮报告来源为用户当前保存的`output/20261021-阅记-项目报告-v3.docx`，保留微调、图1/图2实际绘图及原图；原始要求仍为`raw/期末项目与报告要求.docx`。事实依据为当前Code、返回修复94项TRX、原Windows验收证据及本轮独立解包验证。执行顺序与删除边界见`release-v1-20261021-plan.md`；仅清理output现有3个旧交付，raw和用户数据不动。

## 2026-10-21 当前是否全部完成的状态核对

用户询问指导书要求是否全部完成。实际列出output：仅v2/v3 Word和源码v5，无正式PDF。重新统计当前Code为66类/14接口/14视图；v5 ZIP为119文件113557字节，CRC与白名单正常、无编译结果，但与Code的MainView、MainViewModel及两份测试共4个文件不同。officecli只读查看当前v3.docx，模板章节存在，正文仍为93项并引用v5，尚未包含当前94项与返回入口修复。

故结论为应用主要要求已实现，源码入包、报告同步和最终PDF交付尚需收尾。完整依据与准确路径见`requirements-status-20261021.md`。本次不以状态问题擅自修改用户已微调的正式Word或恢复旧PDF，未更改raw、参考和源码。

## 2026-10-21 左上角返回按钮反馈与修复

用户报告“返回按钮一点用没有”，补充指左上角按钮。直接读MainView、MainViewModel和三层导航后确认已有模块内页面栈；缺陷为菜单首页仍展示可执行返回入口，实际栈底GoBack无操作。已按既有参考机制隐藏无上一页时入口并同步命令可执行状态，不改变菜单清栈约定。

原始要求上下文来自`raw/期末项目与报告要求.docx`；具体业务约束来自已确认`docs/plan.md`及本地参考导航。新增真实MainView绑定回归在旧版失败，修复后94项全通过；Windows隔离库实际点击通过空编辑返回、详情返回、多级编辑返回及菜单首页状态。证据、准确修改清单和边界见`back-navigation-20261021.md`。正式报告/源码包未更新，原始资料与参考不变。

## 2026-10-21 与参考项目架构直接比较

- 用户进一步要求直接比较架构设计。本轮补读本地参考的REFERENCE、两个工程文件、启动/初始化/详情/查询结果/收藏VM，以及PoetryStorage、FavoriteStorage、FilePreferenceStorage和提示接口/实现；与当前数据库服务、业务校验、编辑基类、分页及模态交互逐项比较。
- 本地参考版本依据`daily-poetry-a/REFERENCE.md`的提交记录，不访问上游或用本地附加分析文档替代真实源码。
- 核心事实：参考两个存储各自持有SQLiteAsyncConnection并使用不同数据库；当前共享IDatabaseService管理四表、事务和门禁。参考结果页用无限滚动集合，当前书库用页码/PageResult。参考IAlertService仅AlertAsync，当前加入确认/离开/特定编辑VM参数，职责和耦合范围扩大。
- 完整证据清单与设计取舍见`architecture-comparison-20261021.md`。同一源码上一轮12项架构测试结果沿用，不宣称本轮重新测试；不修改原始资料、参考或应用代码。

## 2026-10-21 当前架构对应说明检查

- 原始来源：`raw/期末项目与报告要求.docx`，使用officecli-docx技能及`officecli view ... text`只读提取八项架构要求。原文允许“只需要反映对应的思想，但无需采用一模一样的技术”；现有已确认规划另要求保留参考具体机制。本轮不混淆两种要求。
- 参考来源：本地`daily-poetry-a/DailyPoetryA/{ServiceLocator.cs,ViewLocator.cs,Services/*NavigationService.cs}`和Library的MainViewModel/ViewModelBase；重新计算55个C#文件及9个视图哈希，共64项与`reference-baseline.json`一致。
- 实现来源：当前`Code/ReadJournal`与`ReadJournal.Library`的相应实现、两个工程文件、应用资源、主窗口和主视图XAML、书库/详情VM、数据库/存储/文件/提示服务及已有架构测试。逐项依据和调用链见`architecture-mapping-20261021.md`。
- 本轮重跑Roslyn统计：应用66类、14接口、14视图；参考63类、10接口、9视图。统计排除测试和生成代码，分部类去重。证据为`docs/evidence/architecture-mapping-20261021-{application,reference}-inventory.json`。
- 本轮当前源码重新构建并筛选运行ArchitectureTests及OverviewArchitectureTests，共12项全通过。TRX和日志分别在`docs/evidence/architecture-mapping-20261021/`及同级测试日志。历史93项/71项不冒充本轮执行结果。
- 原指导书SHA256前后不变：`352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d`。无外部来源，未修改原始资料、参考或业务代码；保留本轮开始时已存在的用户修改和删除。

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

## Codex 云端开发可行性查询（2026-10-20）

- 来源名称：OpenAI 官方文档 — Codex Cloud。
- 链接：https://learn.chatgpt.com/docs/cloud
- 引用日期：2026-10-20。
- 核对事实：可选择 GitHub 仓库建立云端环境，安装依赖和工具、测试并发布环境；任务可编辑代码、运行命令与检查，结果需审阅后提交或创建 PR。
- 来源名称：OpenAI 官方文档 — Cloud environments。
- 链接：https://learn.chatgpt.com/docs/environments/cloud-environments
- 引用日期：2026-10-20。
- 核对事实：支持配置安装及启动流程、指定工具版本、网络访问和多个仓库；每个新任务有独立工作区，环境保存状态不能替代版本控制。
- 结合本地规划的判断：可采用云端实现与测试、本机 Windows 验收的工作方式；需先准备 .NET 8、NuGet 访问及老师参考源码。当前仓库未包含项目目录外的 DailyPoetryA；本地绝对路径不能直接作为云端资料路径。
- 待确认：具体云端环境的 SDK、桌面显示能力、原生依赖及账户权限尚未实测；本次仅回答可行性，未创建环境或启动开发，未更改 v4 规划。

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

## 2026-10-21 Windows 本机验收来源

- 来源名称：课程指导书；文件：`raw/期末项目与报告要求.docx`；读取日期：2026-10-21。使用 officecli-docx / officecli view text 直接读取，要求为页面不少于参考、类/接口各不少于参考70%、纯源码压缩包≤10MB、八项架构思想、指定模板PDF。
- 对照来源：本机 `daily-poetry-a/` 实际源码、`Code/` 实际源码、`output/20261021-阅记-源码-v4.zip` 与 `output/20261020-阅记-项目报告-v1.pdf`。
- 历史 docs 和 memory-bank 仅提供检查线索；本轮 Windows 结果另存 docs/evidence/local-windows-20261021，不用 Linux 结果代替本机实测。
- 本轮初始 git HEAD：d6c3f8a。已有 docs/source-notes.md 未提交改动保留，不覆盖。没有联网查阅外部资料。

## 2026-10-21 报告和README修订来源

- 用户当前明确授权“修改报告和readme”，沿用既有模板和已确认身份资料，不新增选题或大纲决策。
- 原始指导书：raw/期末项目与报告要求.docx；核对日期2026-10-21；SHA256仍为352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d，原件未改。
- 内容来源：docs/report/report.tex及真实Linux历史截图；docs/local-audit-20261021.md；docs/evidence/local-windows-20261021中的两份93项TRX、71项架构探针、UI截图及只读SQLite核对结果。
- 配套源码来源：当前Code；源码v5逐文件匹配当前白名单，与实际测试v4相比仅README变化。新报告不假称v5又重跑过应用测试。
- 使用pdf技能与ReportLab/Poppler生成和检查21页正式PDF v2，最新可编辑来源report-v2.tex。未新增外部研究资料，未覆盖旧交付。

## 2026-10-21 Word可编辑版来源

- 用户要求“输出word即可，我进行微调”，沿用已确认最新报告结构及事实，不再调整业务内容或源码包。
- 正文来源docs/report/report-v2.tex，参照output/20261021-阅记-项目报告-v2.pdf；截图仍来自docs/evidence既有真实Linux/Windows证据。原始模板依据raw/期末项目与报告要求.docx，未改原件。
- 使用officecli-docx流程、python-docx生成原生文本/表格/代码，并用本机Microsoft Word 16.0独立隐藏实例更新TOC及PAGE字段、原生渲染21页检查；过程PDF/PNG/HTML仅存docs/report/word-v2。
- 正式交付output/20261021-阅记-项目报告-v2.docx，1587008字节；19张截图、7个可编辑表格、19个正文层级标题，正文/代码可编辑，2张结构图用可编辑表格表达，文档无保护。Word原生渲染及OOXML验证通过。用户微调后以该Word为继续编辑来源，不从tex覆盖用户改动。

## 2026-10-21 Word v3 架构与实体图修订来源

- 用户当前消息及截图：要求架构图、实体类图改用绘图，生成 v3。
- 原始指导书：raw/期末项目与报告要求.docx，只读，SHA256 352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d。
- 编辑底稿：output/20261021-阅记-项目报告-v2.docx，按当前已保存文件继续，不用旧生成脚本覆盖用户修改；基线 SHA256 faecd66dadee75f1fb605ff449d88fcc4a689c2e4a44d55c39db7e9572ff63d1。
- 架构依据：Code/ReadJournal/ServiceLocator.cs、ViewLocator.cs、App.axaml.cs，Library 的 Models/Services/ViewModels 及原报告架构说明。
- 四实体字段依据：Code/ReadJournal.Library/Models/Book.cs、ReadingRecord.cs、Quote.cs、ReadingPlan.cs；外键和级联依据 Services/DatabaseService.cs 的建表 SQL。Book 到每种子表均为 1 对 0..*，子表 BookId 指向 Book.Id；ReadingRecord.Pages 为计算属性，不列为数据库持久化字段。
- 未新增外部资料。过程与计划：docs/report-v3-diagrams-plan.md。


## 2026-10-22 完整实施规划编制与交付

- 用户本轮明确要求直接生成完整规划，包含接口、统一架构、分阶段功能、逐步验收，并在需要 API 时告知。此授权用于本轮规划交付，不把规划状态写成开发已完成。
- 输入：用户 v1 附件、前轮可行性评估、当前 2133786 的 Code 实际接口/模型/服务/导航/调用方及原始课程约束。补读各存储接口与实现、详情/概览/计划/设置 VM、文件选择器和 README；没有调用新的外部技术资料或 DeepSeek。
- 为保留原始附件，按实际原文件名原样复制至 `raw/1-阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md`，不覆盖已有资料；原附件和原有 raw 文件均不修改。
- 交付：`output/reader-plan-v2/` 下总体规划 v2、接口与数据契约 v1、分阶段实施与验收执行表 v1，全部 Markdown。编制记录见 `docs/20261022-完整实施规划编制记录-v1.md`。
- 新功能按 P0～P10 推进；真实 API 在 P9 前通知，P8 使用 Mock；本轮零真实调用、未实施代码、未重新运行应用测试。文档静态检查结果另存 docs/evidence/reader-planning-20261022/validation.json。

## 2026-10-22 阅读器 P1 实验来源与实测

- 当前用户“推送后继续p1”“continue”“你可以用linux，windows保留”：授权 P0 推送、继续 P1 并使用 Linux 验收，Windows 保留后续。
- 需求来源：output/reader-plan-v2 三份规划；原始 v1 与 raw 指导书保持只读。测试文本来自 P0 generate_samples.py，非私有书籍。
- 实测来源：docs/evidence/reader/P1 下 prototype-linux-fontfix.trx（14/14）、production-baseline.trx（96/96）、linux-fontfix-scale-{1,1.5,2}/native/probe.json 与真实窗口截图。实现提交 bd6eb50。
- 外部诊断来源名称 Avalonia 官方 11.0.10 X11 实现；链接 https://github.com/AvaloniaUI/Avalonia/blob/11.0.10/src/Avalonia.X11/X11Clipboard.cs 、同目录 X11Atoms.cs / X11Platform.cs；引用日期2026-10-22。准确 URL/blob SHA 见 P1/environment-and-results.json，未复制实现。只用于裸 Xvfb atom 缺失问题的根因验证。
- .NET/NuGet 与 Debian X11/Openbox/xclip 是实验运行依赖，版本/平台记录随证据保留。零真实 DeepSeek 调用，无生产数据库修改。

## 2026-10-22 P2 开始

来源为用户最新“推送，然后开始p2”、既有规划三件套、P0冻结fixtures和P1真实实验。P1远端4b336d3已核验。P2实施计划见reader-p2-plan.md；当前尚在实施，不提前宣布验收通过。

## 2026-10-22 P2 验收来源补充

当前Code提交9b24b07；P0冻结SQL/JSON未修改。最终证据为P2/p2-reviewed-workspace.trx与p2-reviewed-clean-source.trx（各124/124）、process-accepted/probe.json（字段/SQL约束/独立子进程）、ui-accepted/desktop-smoke.json与截图（schema2真实窗口迁移/重开）。145文件临时白名单源码核验摘要reviewed-package.json，未覆盖正式交付。中间失败和版本结果均保留。无新增外部研究资料或DeepSeek调用；使用cloud-environment-runtime核对本环境与NuGet依赖可用性。

## 2026-10-22 P3 开始

来源：用户“推送，开始p3”、output/reader-plan-v2三份规划、P2正式契约和验收、P0冻结SQL/JSON。P2远端8696045已核验。使用合成受管文本/资源与故障注入，不读取用户私有书籍或密钥内容；计划见reader-p3-plan.md。


## 2026-10-22 P3 最终验收来源

实现/证据17690bb；最终P3/p3-percent-validated.trx与p3-reviewed-clean-source.trx各169/169，reviewed-source-package.json记录159文件临时源码验证包。crash-probe-reviewed/probe.json记录四处真实Process.Kill与损坏日志保留；ui-accepted、ui-clean-accepted与ui-verification-final.json记录最终实际桌面及哈希核对。中间失败与早期百分比fixture均保留其历史含义，不作为最终版本依据。协议补充见reader-p3-protocol.md；未新增外部研究资料、未使用真实书籍/Key、未调用API，原始资料及正式交付未变。


## 2026-10-22 P4 开始

用户“推送，然后开始p4”；P3远端16e4155已核验。来源为output/reader-plan-v2三份规划、P3协议及验收、raw原规划和已冻结旧库资料；使用确定性合成TXT及编码/故障样本，未新增外部研究资料，未读取真实书籍/Key。计划见reader-p4-plan.md。

用户追加“continue，完成后提交至远端”，P4最终验收后推送feature/reader-native；仅推当前功能分支，不改main。环境重建后重新下载官方.NET8.0.424与允许的Debian镜像桌面包，并重做最终独立验证；首次桌面仅完成UTF8导入/去重，保留为中间证据。


## 2026-10-22 P4最终证据与授权

实现5358a4a；P4/p4-inbox-tombstone-reviewed.trx、p4-final-clean-source.trx各217/217；final-source-package.json记录169文件临时验证包，逐文件SHA一致。crash-probe-reviewed/probe.json记录10处实际杀进程、实际TXT初始/关联往返与损坏日志保留。ui-final/ui-missing/ui-clean-source、ui-verification-final.json记录最终桌面、Missing、独立构建及3书/11文件/5来源摘要核对；中间失败保留，不作为最终结论。用户新增P5/P6/P7顺序实施及逐阶段推送授权。无新的研究资料/真实书籍/Key/API，raw与正式交付不改。
