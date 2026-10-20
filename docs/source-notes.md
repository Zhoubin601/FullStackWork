# 资料来源与核对记录

记录日期：2026-10-20。本文记录事实来源，不表示项目已经实现或验收。

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
