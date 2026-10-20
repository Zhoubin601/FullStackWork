# 指导书合规复核（2026-10-20）

> 本文件为修复前的历史审查。用户随后已授权修复，当前状态和复验结果见 [缺陷修复记录](repair-results-20261020.md)。


本次仅检查，未修改应用源码、原始资料或正式交付物。原始依据为 `raw/期末项目与报告要求.docx`，以 ZipFile/XML 重新提取正文和表格；同时直接检查当前源码、原生参考副本、源码 ZIP 及实际 PDF。下述“通过”仅指相应条款，不能推导全部功能均已端到端验证。

## 结论

项目的数量门槛、源码包约束和八项架构要求均有当前实物证据。**正式报告交付仍未完成**：output 中无 PDF；18 页 PDF 位于 docs/report，仍是带身份占位符的草稿。当前不能认定整份作业已全部符合交付要求。

Windows 桌面未实测是当前项目规划的验收缺口，但原始指导书没有指定操作系统，不应把 Windows 必验冒充指导书硬性条款。提交截止日期在原始资料中未提供，也不是本次发现的程序缺陷。

## 要求、证据与状态

| 指导书要求 | 当前实物证据 | 判断 |
|---|---|---|
| 功能具有实际意义、逻辑自洽 | 本地阅读管理；Book、ReadingRecord、Quote、ReadingPlan 四类实体；阅读记录派生进度及统计，无离线工具注册登录的矛盾。`Code/ReadJournal.Library/Services/ReadingService.cs`、`StatisticsService.cs` | 设计符合；实际流程运行由本轮功能审计另验 |
| 页面不少于 DailyPoetryA | 参考 Views 9 个 axaml；本项目 14 个 axaml，其中 10 个业务页，另含初始化、主窗口、主容器及记录弹窗 | 数量通过 |
| 类不少于参考 70% | 本轮重新运行 Roslyn TypeInventory：参考 63 类，最低 ceil(63×0.7)=45；本项目 65 类 | 通过 |
| 接口不少于参考 70% | 本轮相同口径重新扫描：参考 10 接口，最低 7；本项目 14 接口 | 通过 |
| 源码压缩包，不含编译结果，不超过 10MB | `output/20261020-阅记-源码-v1.zip` 实查 93,138 字节、110 文件；未发现 bin/obj/TestResults/.git、dll/exe/pdb/so/dylib；包内每个文件与当前 Code 对应文件 SHA256 相同 | 通过 |
| MVVM 视图与视图模型分离 | `ReadJournal.Library/Models` 为数据实体/投影；交互在 ViewModels；Views code-behind 多为 InitializeComponent。主窗口关闭事件只调用 VM 的 CanCloseAsync。`Code/ReadJournal/Views/MainWindow.axaml.cs:6` | 通过 |
| 统一导航参数注入入口 | `Code/ReadJournal.Library/ViewModels/ViewModelBase.cs:19` 定义虚方法 SetParameter；`Code/ReadJournal/Services/ContentNavigationService.cs:18`、`MenuNavigationService.cs:18` 在切换前调用，详情与编辑页按需覆写 | 通过 |
| 接口抽象、构造依赖注入 | `Code/ReadJournal/ServiceLocator.cs:25` 起注册接口映射；例如 `Code/ReadJournal.Library/ViewModels/BookDetailViewModel.cs:24` 构造接收存储、提示、导航接口 | 通过 |
| 轻量 ServiceLocator，以 IoC 集中组装 | `Code/ReadJournal/ServiceLocator.cs:23` 创建 ServiceCollection，`:53` BuildServiceProvider，`:10` 保存 IServiceProvider，`:55` 起属性经 GetService 解析；`:12` Current 从应用资源定位 | 通过 |
| 三层服务化导航、统一常量、主 VM 管栈 | 三个导航接口及对应实现齐全；常量在 Library/Services 的导航接口文件中；`Code/ReadJournal.Library/ViewModels/MainViewModel.cs:13` ContentStack，`:32` PushContent，`:42` GoBack | 通过 |
| ViewLocator 自动渲染机制 | `Code/ReadJournal/ViewLocator.cs:5` IDataTemplate；`:12` 命名映射，`:13` Type.GetType，`:15` 创建/按类型缓存视图，`:16` 设置 DataContext | 通过 |
| IO 副作用服务化及异步协作 | VM 无直接 File/Directory/HttpClient 调用；数据库 IO 在 `Code/ReadJournal.Library/Services/DatabaseService.cs:12`、`:46` 经 gate/Task.Run；文件和提示均经接口服务；`Code/ReadJournal.Library/ViewModels/SettingsViewModel.cs:18` 起使用异步命令 | 通过 |
| Library/UI 工程分层 | Library csproj 只引用 MVVM 与 sqlite，UI csproj 引用 Library 并承担 Avalonia、定位器和平台服务；Library VM 不引用 Avalonia | 通过 |
| 按报告模板编写 | 实际 PDF 文本可提取，18 页；封面、背景意义、1.1/1.2、2.1–2.5、3.1–3.3、4.1–4.3、总结展望均存在；14 张截图均嵌入 PDF，源截图无缺失 | 正文结构通过；封面未完成 |
| 报告包含项目名称、姓名、学号、班级 | `docs/report/report.tex:22` 姓名/学号/班级仍为“资料未提供”；PDF 第一页一致 | 未完成 |
| 必须以 PDF 提交报告 | 草稿实存 `docs/report/20261020-阅记-报告草稿-v1.pdf`；output 仅 ZIP，无正式 PDF；tex:25 明确草稿状态 | 格式已有，正式交付未完成 |

## 数量复核方法与包检查

本轮使用 SDK 8.0.424（`/tmp/readjournal-audit/dotnet`），重新构建 `docs/tools/TypeInventory` 到 `/tmp/requirements-typeinventory`，直接执行生成 DLL，对当前两个源码根目录分别扫描；工具原文不改。只统计 UI/Library 手写 class/interface，排除 bin/obj/生成代码/测试；partial 按工程及完整类型名去重，包括默认访问级别。输出保留在当前执行机 `/tmp/requirements-reference-count.json` 与 `/tmp/requirements-app-count.json`。

结果：参考 55 个 C# 文件、63 类、10 接口、9 视图；本项目 76 个 C# 文件、65 类、14 接口、14 视图。复核了已有两个 JSON 清单的所有源文件与视图哈希，均与当前文件一致。注意工具对本项目输出的 MinimumClasses/MinimumInterfaces 是本项目自身的 70%，不是作业门槛；作业门槛始终取参考项目的 45 类、7 接口。

源码 ZIP SHA256 为 `ae11c13d96614e8b127a0db2e5735fcbbf1a002bc9710e2064068f57c411dee4`。ZIP 路径约束、编译产物排除与包内/当前源码逐文件一致性均重新通过 Python zipfile/hashlib 检查，未仅沿用历史检查结论。

## 尚需完成或由用户复核

1. 用户提供并复核姓名、学号、班级，然后完成报告封面与正式 PDF 交付。
2. 按既有项目规划补 Windows 桌面实际验收；指导书本身未限定此平台。
3. 端到端功能是否全部打通，需结合本轮重新构建、自动化测试和实际 UI 流程结果判断；本文件不以架构与文件存在代替运行证据。
