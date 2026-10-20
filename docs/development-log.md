# 开发执行记录

日期：2026-10-20（用户时区 Asia/Shanghai）。当前阶段：5。阶段 1～4 已按顺序通过 Linux 验收；阶段 5 的最终源码包、干净目录构建/测试/启动及实际 UI 已执行，报告封面身份和 Windows 实测仍待补齐。下文保留初始阻塞及后续解决的真实过程。

## 工作区与资料恢复

初始 `/workspace/FullStackWork` 仅有 Git 元数据，`git status --short --branch` 返回 `No commits yet on work`。递归搜索 `/workspace` 没有 plan.md、指导书、项目或参考源码。

读取配置远端后执行：

```sh
git ls-remote origin
git fetch origin main
git ls-tree -r --name-only FETCH_HEAD
git checkout -B work FETCH_HEAD
```

以上成功，恢复提交 `c8e3c71ca7847e5c26f1238f0ab6a954b7209ae1` 的原有资料，无初始工作文件被覆盖，未推送远端。`Code/` 只有 `.gitkeep`；仓库已有说明确认参考源码未随前次资料同步。

已完整读取规划和指导书，并核对报告正文标题及封面表格。指导书 SHA256 与旧记录一致：`352E94BC9126CC0EF9B5E9F0D293AD8B562BB07B7E17E85AACD7972F918D4D1D`。原始 raw 文件未修改。

## 已完成的阶段 1 工作

- `docs/plan.md` 仅一行差异：将推定已有命令/目标错误的表述替换为用户指定的检查表述，保留该行其余架构约束。
- `docs/stage-1-design.md`：课程要求追溯、页面流转、实体关系、职责分配、事务与恢复流程，以及参考源码核对闸门。设计数量不作为实际实现数量。
- 保留原 `docs/reference-baseline.md`、JSON 清单和 `docs/tools/Measure-ReferenceProject.ps1` 原样；检查 JSON 条目数量、类型键去重与哈希字段格式，内部一致。此项未比对源码字节、未重跑 Roslyn，不能称为参考源码统计通过。
- 来源、当前授权及缺失条件同步记录于 source-notes.md、questions.md 和 project-context.md。

## 参考源码阻塞现场

复现命令：

```sh
git ls-remote https://gitee.com/zhangyin-gitee/daily-poetry-a.git HEAD
```

退出码 128，实际错误：

```text
fatal: unable to access 'https://gitee.com/zhangyin-gitee/daily-poetry-a.git/': CONNECT tunnel failed, response 403
```

已核实 `/etc/codex/network-policy.json`：restricted，允许域名未列 gitee.com。没有绕过代理或修改策略文件。参考源码实际内容不可得，不能从既有过程文档推定命令可见性或导航错误。

最小继续条件：提供当前可读的原参考源码目录/归档，或者在环境网络配置中允许 gitee.com 后重新访问。优先核对已有提交 `947982998212fd50d1f53c7e4fbcf5da79bd9607` 及逐文件哈希；若版本不同，先记录差异和真实计数。

## 构建工具准备

原环境 `command -v dotnet` 未找到工具。已执行并成功：

```sh
curl --fail --show-error --location --max-time 60 https://dot.net/v1/dotnet-install.sh --output /workspace/scratch/dotnet-install.sh
bash /workspace/scratch/dotnet-install.sh --version 8.0.424 --install-dir /workspace/scratch/dotnet --no-path
/workspace/scratch/dotnet/dotnet --info
```

实测 SDK 8.0.424、MSBuild 17.11.48、运行时 8.0.30，系统 Debian 13 / linux-x64。官方 SDK 下载大小 216,718,168 字节；安装成功不等于已还原项目依赖。工具留在项目外，后续显式使用该 dotnet 路径或在单次命令中配置 PATH。

## 构建、测试与 UI 状态

| 检查 | 实际执行与结果 |
|---|---|
| 指导书 XML/封面表格提取和原文件 SHA256 | 已执行，正文可读，哈希一致 |
| 既有基线 JSON 内部一致性 | 已执行：63 类条目、10 接口条目、55 源文件条目、9 视图条目，类型键无重复；仅限清单内部 |
| 参考源码获取、架构逐文件核对和统计重跑 | 获取失败，其余未执行 |
| .NET SDK 安装和 --info | 已执行，成功 |
| dotnet restore/build/test | 未执行；未取得参考源码，阶段 1 未通过，三工程尚未建立 |
| 实际应用启动、UI 流程和截图 | 未执行；无应用且未进入对应阶段，不产生示意截图冒充运行 |
| 源码包、干净目录验证、PDF 报告 | 未执行；无正式 output 交付物 |

## 后续执行点

1. 取得参考实际源码，核对 ServiceLocator、App、ViewLocator、三层导航和常量、MainViewModel、ViewModelBase、工程依赖，并核验旧清单和统计脚本结果。发现实际问题再记录文件/行号和修正依据。
2. 阶段 1 验收通过后才进入三工程骨架，按规划实现并真实测试容器、构造注入、单例、导航参数/返回及数据库初始化和失败重试。
3. 后续阶段仍按 plan.md 原顺序推进。当前云平台不能充作 Windows 桌面验收结果；报告及交付只能引用实际运行证据。

## 后续恢复执行：阶段 1、2 验收

2026-10-20 用户已将参考源码推送到根目录。fetch 后通过 `git merge --ff-only FETCH_HEAD` 同步提交 0dc2d02，保留原有未提交工作。此前参考不可用阻塞已解除。架构及真实类型重统计的通过证据见 reference-architecture-audit.md 和 reference-baseline-cloud.json。

阶段 2 已建立 Code/ReadJournal.sln 及 UI、Library、UnitTest 三工程，固定参考依赖版本；实现全部定位/导航核心机制、SQLite 版本初始化、共享连接串行事务和可重试初始化。概览、书库、详情承担实际数据加载职责，不为数量预建空页面。

实际命令：

```sh
dotnet build Code/ReadJournal.sln --no-restore --nologo
dotnet test Code/ReadJournal.sln --nologo --logger 'trx;LogFileName=stage2.trx'
DISPLAY=:99 READJOURNAL_DATA_DIR=<隔离测试目录> dotnet Code/ReadJournal/bin/Debug/net8.0/ReadJournal.dll
```

最终构建 0 警告、0 错误，7 测试全部通过。涵盖真实容器类型、属性与服务单例、自动构造注入、三层导航、参数先注入、重复目标不覆盖参数、返回栈及菜单重置、反射缓存/上下文、模拟初始化失败重试、真实 SQLite 重启持久化、外键错误触发事务回滚、目录创建失败重试。

已修复实际失败：MainView 的内部 xmlns 声明被 Avalonia 编译器拒绝，移到根节点；PRAGMA busy_timeout 返回结果行，改用 ExecuteScalar<int>。失败记录不视为通过；修复后完整重跑测试。

图形环境使用提取到工作区外的 Xvfb Debian 包（apt 缓存不可用，改从允许的官方 Debian 源下载包），不是 Avalonia Headless 截图。已实际启动桌面窗口：首次启动、再次启动、文件阻挡数据目录造成初始化失败，移除阻挡后通过鼠标点击“重试初始化”成功创建数据库并显示概览。一次重试点击坐标不正确，未创建数据库，调整为截图中的真实按钮位置后完整重跑成功。成功截图为应用运行时直接截屏，不包含手绘或生成内容。

证据：docs/evidence/stage2-build.log、stage2-test.log、stage2-first-launch.png、stage2-second-launch.png、stage2-initialization-error.png、stage2-retry-success.png；TRX 在测试工程 TestResults 中，不入源码包。当前阶段 2 按 Linux 实测通过，Windows 实测未执行。按规划进入阶段 3。

## 阶段 3 验收

实现书籍增改查删、书库搜索/状态筛选/分页、详情进度和记录、阅读记录弹窗增改删、书摘搜索/编辑/收藏/删除。Library 增加 IBookStorage、IReadingRecordStorage、IQuoteStorage、IReadingService 及实现，UI 增加 IAlertService 平台实现；统一草稿基类处理保存、放弃、取消和保存失败。依旧真实容器全部单例，新增路由遵循参数注入 → 切换 → Loaded；同实例先拦截，返回显式刷新并保留列表搜索条件。弹窗不计业务页面。

实际执行 `dotnet build Code/ReadJournal.sln --no-restore --nologo`（存储模块）以及 `dotnet test Code/ReadJournal.sln --nologo --logger 'trx;LogFileName=stage3.trx'`，22 测试全部通过。新增实际 SQLite 业务边界、重读、修改/删除重算、分页搜索、收藏与外键级联测试；Moq 验证保存失败保留草稿、取消/放弃/保存离开；真实容器导航测试 A 详情返回后进入 B 时旧数据立即清空，再正确加载 B。

真实 X11 UI 用鼠标键盘录入 UI Reading A、100 页书籍，第 10～20 页、15 分钟的阅读记录，以及第 20 页收藏书摘；从 UI 流程写入 SQLite 后只读核实值一致。实际弹出未保存离开确认，取消保留草稿，放弃返回列表，原书摘未被覆盖。重启应用再打开详情，进度 20/100、累计 11 页、15 分钟及关联书摘均可见。截图 stage3-* 位于 docs/evidence，日志 stage3-test.log、stage3-storage-build.log、stage3-storage-test.log、stage3-ui.log。没有用直接写库代替本轮 UI 录入。

已补充业务保护：编辑页 Loaded 重入不覆盖脏草稿；记录弹窗关闭后刷新详情以覆盖保存并离开的路径；运行文化设置 zh-CN。当前阶段 3 Linux 验收通过，Windows 实测尚未执行。进入阶段 4。

## 阶段 4 验收

实现阅读计划新增/编辑/删除、三种派生状态、含当天分母建议页数；全量统计与近七日趋势；概览在读书籍/近期记录/待完成计划；主题独立偏好文件；主动空库演示载入；系统文件选择器 JSON 导出与恢复。

四类实体备份字段采用 JsonRequired，拒绝缺失集合/字段、格式错误、非法版本、重复 ID、孤立关联、非法页码/日期及重复未完成计划。恢复在同一串行边界中捕获当前快照，先原子写出自动备份，再事务替换。InsertOrReplace 保留显式主键，已测非连续 ID；主题不在业务备份中。失败模拟覆盖自动备份写出失败和 SQLite trigger 引起的替换失败，原数据保持完整。

命令：`dotnet build Code/ReadJournal.sln --no-restore --nologo`；`dotnet test Code/ReadJournal.sln --nologo --logger 'trx;LogFileName=stage4.trx'`。最终 40 测试全部通过。真实容器额外核对 Library/Services 内全部 14 个接口均有实际单例实现。新增延迟任务测试证明旧 A 请求完成后不会发布到已转入 B 的单例详情；导航三种离开途径共用防重入保护，不产生两份确认窗口。这些为阅记新增业务保护，不是参考原有机制。

实际 X11 UI：通过确认载入空库虚构数据，概览显示在读/记录/计划；计划列表显示进行中、完成、逾期；统计显示 191 页、210 分钟、完成 1 本及七日分布；切换深色并保存；系统保存选择器导出 3 本书/4 条记录/2 书摘/3 计划 JSON；系统打开选择器选择同一实际文件、确认恢复，原库自动备份存在且主题保留。计划编辑实际选择完成书，保存拒绝；切到未完成书显示建议每日 180 页，重复未完成计划保存拒绝。截图 stage4-* 位于 evidence。

UI 驱动曾在 GTK 原生打开选择器输入路径时被自动补全干扰，得到不存在路径，应用正确呈现文件找不到并未替换。改为双击真实文件列表条目后恢复成功；最终成功证据已重新截屏，不能将先前失败当成功。此为驱动交互问题，不绕过文件服务或用写库代替恢复。

阶段 4 Linux 验收通过；Windows 验收仍未执行。实际初次语法树统计为 65 类、14 接口、14 视图文件（10 业务、初始化/主窗口/主容器/记录弹窗各 1），符合规划数量目标；阶段 5 将重生成最终类型清单与文件哈希。


## 阶段 5 已执行部分与实际剩余

- 完成 Code 内构建脚本 build.sh / build.ps1、显式 delivery-files.txt 和 pack.py。包根目录直接为解决方案与三工程，临时目录复制后压缩，前后核对清单与文件字节哈希，110 文件、93,138 字节，远低于 10,000,000 字节。归档不含生成结果。
- 最终包 `output/20261020-阅记-源码-v1.zip` SHA256：ae11c13d96614e8b127a0db2e5735fcbbf1a002bc9710e2064068f57c411dee4。最终包逐文件字节与当前 Code 白名单再次比对一致。
- 全新临时解压目录使用独立空 NUGET_PACKAGES、NUGET_HTTP_CACHE_PATH，实际 `dotnet restore --locked-mode --no-cache`、Release build、test 均退出 0，40 项全部通过；解压后应用在独立数据目录实际首次和再次启动成功，SQLite user_version=1。相关 final-clean-* 日志/截图保留。还原使用网络；编译结果没有回填归档。
- 首次干净验证用错了 .NET 8 不支持的 --no-http-cache 开关，MSB1001；检查 restore --help 后改用 --no-cache 成功，之后对最终归档完整重跑。不把错误命令当通过。
- 固定 SQLite 依赖在全新 Release 构建产生 3 条 NETSDK1206（旧 Alpine RID）提示，未升级或压制；Linux x64 实际可运行，Alpine 未验。图形环境缺少会话管理器、GLX llvmpipe 回退软件渲染仍可完成操作，原始日志如实保留。
- 打开真实绑定日志后发现事件行为在 DataContext 尚未附着时绑定空上下文、固定 Fluent ProgressBar 动画绑定父模板对象。先尝试空值回退仍有诊断；最终事件命令显式绑定同一资源定位器的单例，静态 ProgressBar 模板依据 Avalonia 11.0.10 PART_Indicator 契约实现，未升级框架或替换导航/定位器。修复后重跑 Release 测试、重生成归档并重新干净验证；最终十业务页面全部实际遍历截图，绑定诊断 0。
- 最终手写类型65、接口14、C#文件76、视图14，其中独立业务10。Roslyn JSON/Markdown 清单及逐文件哈希已重生成，参考基线和脚本保留。
- 为满足实际测试截图要求，在真实 xfce4-terminal 窗口执行 Release test，实际输出40通过/0失败/0跳过，直接截屏 final-test-terminal.png；没有把绘制的文字图作为测试截图。
- 补充 UI 重算验证：从已验证的演示数据库复制隔离测试夹具，随后只用实际窗口修改/删除记录及编辑总页数，不直接写库。结束页60→240时累计371页/210分钟/完成2本，关联计划完成；删该记录后161页/175分钟/完成1本、计划回在读；总页数240→30后完成数回2本，累计仍161页/175分钟。截图 consistency-* 及只读SQL核对已保留，绑定诊断0。
- 报告按模板生成完整章节、图表、十页面实际截图及测试证据。环境未提供 AGENTS.md 指定 minimax-pdf 技能，采用已有 XeLaTeX；最初缺 CTeX/xeCJK，从允许的 Debian 官方包只读提取兼容2024版本到项目外；修复字体缺失、数学模式中文缺字和长标识越界。最终 PDF 草稿18页，1,028,672字节，全部页渲染检查，缺字0、越界0、模板章节齐全；抽查细节见 docs/report/validation.json。草稿SHA256 d55f087491528b6b93c8fb41d318f7a279032fbf1f2168a8e60190e2979ace0c。
- **尚未通过完整阶段5**：姓名/学号/班级没有提供（已异步询问），Windows桌面未执行。报告保留 docs/report/20261020-阅记-报告草稿-v1.pdf，不放入正式 output 伪称定稿；源码包可交付。继续时仅需补身份与Windows实际记录，必要修复并复验后定稿PDF，不重做前四阶段。

## 2026-10-20 用户授权修复与报告定稿

用户提供报告身份并授权修复所有已发现缺陷。编辑保护/重试、计划与备份状态一致性及关联安全问题均已修复。原40项加编辑20项、计划8项，共68项全部通过；新版ZIP在全新NuGet目录还原、构建、同一测试集及首次/再次启动成功。真实窗口验证失败后保存阻止和重新加载、重复计划关联拒绝、3/4/2/3实体备份恢复及重启。最终应用66类/14接口/10业务页，源码v2为114文件102816字节；正式报告19页，封面完整，全部页渲染无缺字或越界。旧交付物及原始资料保留。完整证据与SHA256见 repair-results-20261020.md。Windows未实测，未冒称通过。
