# 当前项目指导书符合性与 Windows 本机测试

> 后续处理：用户已授权“修改报告和readme”，本文件列出的报告/README版本滞后问题已关闭。新交付为21页PDF v2及源码v5（仅README改变）；详见 `docs/report-update-20261021.md`。以下保持本次检查时的历史事实和证据。

日期：2026-10-21。检查基准：Git `d6c3f8a` 的当前 `Code/`，本轮不修改应用源码或已有正式交付。来源为原始指导书、实际参考项目和当前实现；旧 Linux 结果只用于定位检查范围。

## 结论

**当前代码的功能规模、八项架构机制和最新源码包满足本次可核实的指导书要求，Windows 本机实测通过；整套正式交付尚有报告版本不一致问题，因此不能宣称“完全对应、零差异或绝对完美”。**

- 工作区 Release 构建成功，0 错误，完整测试 **93 通过、0 失败、0 跳过**。
- 最新源码 ZIP 独立解包、锁定还原、Release 构建后，同一 **93 项全部通过**；独立解包应用也在新数据目录启动、初始化和关闭成功。
- 实际 App、真实 Microsoft DI 容器和反射 ViewLocator 的 **71 项运行时架构断言通过**，另有静态注册、路由和 VM 职责扫描通过。这 71 项不是额外 71 个业务单元测试。
- Windows 真实窗口完成书籍→记录→书摘→计划→收藏→统计→导出→删除→恢复→重启，以及主题保存与重启验证。SQLite 只读核对与界面一致。
- **需在正式提交前更新 PDF 的测试环境、结果、截图和源码包说明**；代码中的 README 也仍保留“Windows 桌面尚未实测”的历史文字。

所有新日志、TRX、截图、清单和只读数据库核对结果位于 [本轮证据目录](evidence/local-windows-20261021/)。

## 原始要求逐项核对

直接使用 `officecli view raw/期末项目与报告要求.docx text` 和 DOCX XML 提取要求。原始文件 SHA256：`352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d`，前后未变。

| 指导书要求 | 本轮实际证据 | 判断 |
|---|---|---|
| 功能有实际意义且逻辑自洽 | 个人离线书籍、阅读记录、书摘、计划、统计与备份；不依赖无意义的本地注册登录；核心流程实际运行 | 符合已核实范围 |
| 页面不少于参考项目 | 参考 9 个视图；应用 14 个视图文件。剔除主窗口、主容器、初始化和阅读记录模态窗，仍有 10 个业务页面 | 达标 |
| 类不少于参考 70% | 参考 63 类，最低 ceil(63×70%)=45；应用 66 类 | 达标 |
| 接口不少于参考 70% | 参考 10 接口，最低 7；应用 14 接口 | 达标 |
| 源码 ZIP 无编译结果且≤10MB | v4 ZIP 为 119 文件、113136 字节，CRC 正常，无 bin/obj/DLL/EXE/运行数据库；与白名单和当前 Code 逐字节一致 | 达标 |
| 八项技术与架构要求 | 下表逐项对照真实参考源码，另有本机运行时探针 | 对应 |
| 使用给定报告模板并交 PDF | 现有 19 页 PDF 的封面、背景、1.1～1.2、2.1～2.5、3.1～3.3、4.1～4.3、总结均存在；缩略图检查未见明显破版 | 格式满足，内容版本需同步 |

数量使用同一 Roslyn C# 语法树工具重跑，仅统计 UI/Library 手写代码，排除测试、bin/obj 和生成代码；包含默认访问级别及静态类，分部类去重。参考 55 个 C# 文件与 9 个视图的 64 项哈希全部与既有基线一致。`application-inventory.json` 内通用工具输出的 Minimum 字段是“当前被统计对象自身的70%”，课程门槛应取 `reference-inventory.json` 的 45/7，不误用应用的 47/10。

## 八项架构与老师示例的对应

| 指导书机制 | 当前实现位置与调用方式 | 本轮判断 |
|---|---|---|
| 1. MVVM | Library/Models 为数据实体；Library/ViewModels 管交互；UI/Views 为 XAML 和薄 code-behind。MainWindow 仅把平台关闭事件交给 VM 的离开检查 | 对应 |
| 2. 统一参数入口 | `ViewModelBase.cs:27` 定义 virtual SetParameter；详情和编辑 VM override；菜单/内容导航在切换前注参 | 对应 |
| 3. 服务接口与构造注入 | Library/Services 定义 14 个接口；VM 构造函数接收存储、业务、导航、文件和提示接口；实际容器自动构造依赖 | 对应 |
| 4. 真实 ServiceLocator/IoC | `ServiceLocator.cs:23` 起 ServiceCollection 注册，`:53` BuildServiceProvider；私有 IServiceProvider 持有容器，`:55` 起 getter 用 GetService。Current 经应用资源取得定位器。29 项 AddSingleton 注册涵盖 14 VM、14 接口及 DataPaths | 严格对应真实容器机制，无手工实例字典替代 |
| 5. 三层导航与主 VM 栈 | Root/Menu/Content 三接口、三常量类、三实现；执行时通过 Current 定位，不在构造函数反向注入主 VM。Root 改窗口 VM 的 Content；Menu 调 SetMenuAndContent；Content 调 PushContent。`MainViewModel.cs:13/32/37/42` 持有栈和返回操作 | 对应；2 根、6 菜单、4 内容目标全部验证 |
| 6. ViewLocator | `ViewLocator.cs:12` 命名约定，`:13` Type.GetType，`:15` Activator.CreateInstance 和按类型缓存，`:16` DataContext；App.axaml 注册数据模板 | 对应；13 个非窗口视图实际构造、缓存、绑定通过 |
| 7. IO 服务化与异步 | 文件/数据库/偏好/提示在可注入服务中；业务 VM 无直接文件操作或数据库查询命中；数据库服务通过 Task.Run 执行 IO，VM 用异步命令 await | 对应；IDatabaseService 仅在初始化相关 VM 中用于启动协调 |
| 8. Library/UI 分层 | Library 工程不引用 Avalonia 或 UI，包含实体、接口、业务服务及 VM；UI 工程引用 Library，负责视图、定位器、导航实现、平台对话框和文件选择 | 对应 |

与参考源码存在明确的正确性扩展：菜单/内容导航允许空参数也走 SetParameter 清理单例旧状态；同实例不重复压栈；忙页阻止导航；编辑页增加草稿、加载版本与离开确认；阅读记录采用模态编辑窗。它们保留了原来的容器、单例、三层导航和主 VM 栈，没有引入 Shell、额外导航宿主或页面工厂替代。指导书本身要求思想对应，并不要求代码完全相同；本轮也按既有更严格的“具体机制一致”约束核对。

Library 的存储接口仍有 SQLiteConnection 委托，说明它没有彻底屏蔽数据库技术；当前 VM 不执行数据库查询。这不违反指导书，但不能由“跨 UI 复用”进一步夸大成“整个业务库完全独立于数据库实现”。

## 本机运行结果

环境：Windows x64，系统版本 `10.0.26300`；.NET SDK `8.0.424`、运行时 `8.0.30`；Avalonia `11.0.10`。详见 `environment.log`。没有联网查阅外部资料；依赖还原使用本机 NuGet 配置及缓存，未隔离全部依赖缓存，不宣称首次还原能够离线完成。

在项目根目录执行：

```powershell
dotnet restore Code/ReadJournal.sln --locked-mode
dotnet build Code/ReadJournal.sln -c Release --no-restore
dotnet test Code/ReadJournal.sln -c Release --no-build --no-restore --logger "trx;LogFileName=windows-full.trx" --results-directory docs/evidence/local-windows-20261021
```

工作区与独立源码包均构建成功、各自 93 项通过。测试涵盖阅读规则、存储/事务、编辑及加载保护、计划一致性、删除/导航竞态、恢复并发、设置导航和架构。构建各有 3 条 NETSDK1206：固定 SQLite 依赖含旧 Alpine RID；本机 win-x64 实际启动及数据库读写正常，不能据此推断 Alpine 已兼容。

真实窗口通过 Windows UI Automation 的 Invoke/Value 模式操作实际控件，配合鼠标键盘和进程窗口截图；未注入替身 ViewModel 或直接通过 SQL 写入业务数据。

| 流程 | 实际结果及证据 |
|---|---|
| 首次启动 | 新建隔离数据目录后显示空概览；`01-first-start.png` |
| 书籍与校验 | 空编辑保存提示总页数必须为正整数；实际新建 120 页的虚构测试书；`ui-03-book-validation.json`、`ui-04-book-created.json` |
| 阅读记录 | 0 起始页被拒绝；10～20 页/25 分钟保存成功，界面累计 11 页、进度 20/120；`ui-07-record-invalid.json`、`08-record-saved.png` |
| 书摘、计划 | 实际新建第15页摘录及目标当天的计划，显示剩余100页/建议每日100页；`10-detail-complete.png` |
| 收藏与查询 | 清除测试辅助过程遗留的搜索条件后，切换收藏显示星号，数据库 IsFavorite=1；`11-favorite.png` |
| 计划、统计、概览 | 页面显示与记录一致的 11 页、25 分钟、0 本完成、剩余100页；`12-plans.png`、`13-statistics.png`、`14-overview.png` |
| 原生导出/恢复 | 实际 Windows 文件对话框产生合法 JSON，恢复成功并生成自动备份；`ui-export.json`、`17-restored.png` |
| 删除与恢复闭环 | 实际确认删除书籍，四表各0；再次恢复后四表各1，自动备份确为删除后四表全空；`ui-deletion-counts.json`、`ui-22-restored-after-delete.json`、`validation.json` |
| 主题与重启 | 鼠标键盘切深色、保存、正常关闭后重启，深色与业务数据保留；`24-theme-applied.png`、`25-final-overview.png`、preferences.json |
| 数据一致性 | 重启后四表记录与恢复前快照完全一致，收藏保留；PRAGMA integrity_check=ok，foreign_key_check为空；`ui-final-data.json`、`validation.json` |
| 独立源码包启动 | 全新解包应用用另一个空数据目录创建版本1数据库，显示空概览并正常关闭；`26-clean-package.png`、`ui-26-clean-package.json` |

三个工作区启动日志及独立包启动日志均未输出绑定诊断。截图按实际窗口抓取，不将旧 Linux 图片当作本机证据。测试进程已正常关闭。

测试辅助过程曾遇到名称不匹配、Windows PowerShell 脚本 UTF-8 BOM、窗口前台获取、残留搜索条件及 ComboBox 不支持 ExpandCollapsePattern；调整辅助操作后继续通过。上述不作为应用功能失败，也不把中途没有成功执行的步骤算作通过。原生文件选择通过实际对话框完成，本轮不声称所有选择器步骤都已写成可无人值守重放的自动化脚本。

## 正式交付的未完成项

**[P2] 现有报告的验证与包版本滞后。** `output/20261020-阅记-项目报告-v1.pdf` 物理第1、15、16、19页仍记载 Windows 未验收、68项测试；第15页源码包描述为114文件/102816字节。最新v4为119文件/113136字节，本轮实测为93项及Windows通过。PDF的66类/14接口等规模数字本轮核对正确，无需误改。保留旧历史结果没有错，但用这份PDF直接描述最新交付会产生版本不一致。

建议形成下一版报告，保留原 Linux 测试历史，增加本轮 Windows 环境、93项测试与截图、备份恢复数据证据，并准确对应v4或后续重新打包的源码版本。README中的 Windows 未实测说明也需随文档修订更新；如修改 Code/README.md，应重新生成新版本ZIP，不能继续称v4与修改后源码逐字节一致。

本轮完成的是检查，不修改业务代码，不重做或覆盖正式PDF/ZIP，不提交或推送。用户需复核上述报告更新范围及最终拟提交的源码/报告配对。未执行大数据压力、Alpine、其他CPU/系统版本或任意用户行为的穷尽测试；本机一次验收不能证明软件绝无缺陷。

原始资料和参考源码保持不变；新增过程文件均在docs，长期进度更新到memory-bank。自动审批以策略限制拒绝了本轮临时解包/构建目录的递归清理，未提供更具体原因，因此保留 `docs/evidence/local-windows-20261021/clean-source/` 及 `architecture/bin/obj/`。它们是测试辅助材料，不进入源码ZIP或output；原ZIP、PDF、日志、TRX、截图及测试数据核对证据均保留。
