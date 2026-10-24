# 合并后全功能检查与修复记录

日期：2026-10-24。基线：d28f1d6，本地 main。本轮检查包括源码、自动化、生产容器探针、Windows 实际窗口及真实 DeepSeek 调用。

## 结论

发现并修复两处功能缺陷；修复后，在本轮明确覆盖的样本与环境内，没有未解决的功能失败。主要业务从界面操作到 SQLite、受管文件、关闭重启和完整备份恢复均已打通。测试不能证明所有可能输入与环境下绝对没有缺陷。

- 最终 Release：353 项，351 通过、0 失败、2 项 Windows 符号链接权限跳过，约 5 分 26 秒。
- 最终生产容器/视图/导航/性能探针：153 项通过，15 个 ViewModel、36 个服务接口。
- 旧库迁移与单实例、4 处备份恢复进程中断、10 处 TXT 导入/删除进程中断通过。
- EPUB：11 个自建支持/降级变体与 15 个异常拒绝变体通过，含真实导入、会话、书摘定位及完整备份往返。
- 原生排版实验：14/14 通过。
- 真实 DeepSeek：5 次调用成功，解读不入库、整段选区一条、去重、当前页提取、页范围解读、原文回跳和撤销通过。
- 实际完整备份恢复：10 张业务表内容逐项一致，12 个受管文件 SHA256 全部一致。

## 已修复缺陷

### 1. 本机缺少默认字体或保存字体时，排版操作被阻止

触发：干净 Windows 环境没有默认 Noto Sans CJK SC，或偏好来自安装了其他字体的电脑。正文可依靠后备字体显示，但字体输入仍是未安装名称；用户只改字号/纸张后点击应用，同样被 InstalledFont 拒绝。

修复：打开时在内存中解析为当前字体目录中的可用字体，保留字号、配色等设置，并提示临时替代；不在打开时写回偏好。用户点击应用后才保存，并清除旧提示。手动输入一个不存在的字体仍明确拒绝且不覆盖偏好。

源码：Code/ReadJournal.Library/ViewModels/ReaderViewModel.Fonts.cs、ReaderViewModel.cs。

回归：MissingDefaultOrSavedFontCanStillChangeTypographyWithoutOverwritingPreferencesOnOpen 两个用例。修复前均失败，修复后通过；实际窗口证实本机 209 种字体目录下从 Segoe UI 应用 28 字号成功、错误字体拒绝、默认恢复成功。

### 2. 首屏布局被视口变化替换后，正文可见但会话未激活

触发：打开阅读器时，字体预览、提示区域或窗口尺寸改变正文视口，新重排取消初次重排。旧打开逻辑将这种取消标成 OpenFailed；替代页面仍绘制出来，但首屏确认不能启动会话，翻页与 AI 入口不能正常使用。

修复：在首屏排版前进入等待呈现阶段，允许实际绘制确认启动会话；同一次打开被新视口重排替代时保留呈现/活动状态，不将内部替代取消当作打开失败。真正退出取消和真实打开异常继续按原流程处理，不提前创建阅读会话。

回归：ViewportChangeDuringFirstLayoutStillPresentsAndStartsExactlyOneSession 用可控首屏取消稳定复现，修复前为 OpenFailed，修复后可呈现且幂等保存一条会话。实际窗口再次验证首屏激活、翻页、复制保存、目录、重置再打开和重启续读，均通过。

源码：Code/ReadJournal.Library/ViewModels/ReaderViewModel.cs。新增永久回归均在 Code/ReadJournal.UnitTest/ReaderLifecycleTests.cs。

## 功能与链路矩阵

证据根目录：docs/evidence/integrated-audit-20261024/。下表的实际窗口均使用本轮独立库，不修改用户书库。

| 功能 | 本轮检查结果 | 证据 |
|---|---|---|
| 手动书籍与校验 | 空书名不入库；新增资料、非法页码拒绝、合法记录入库；编辑/删除与版本保护由最终完整回归覆盖 | desktop/01-manual-complete.json；full-verified.trx |
| 手动书摘/收藏 | 入库、取消收藏/重新收藏持久化；手动书摘不显示无效原文回跳 | desktop/actions.jsonl；01-manual-detail.png |
| 阅读计划/统计/概览 | 手动书与电子书计划入库；完成状态与时长显示；概览近期记录；计划一致性和编辑回归通过 | 03-plans.png、04-overview.png、09-mixed-statistics.png；full-verified.trx |
| 搜索、筛选、分页与编辑保护 | 实际窗口搜索定位多本书；自动化覆盖数据查询、分页、草稿与异步导航竞态 | actions.jsonl；完整回归 |
| TXT 文件选择器与去重 | 同一文件导入两次只创建一册 | desktop/02-txt-open.json；actions.jsonl |
| 收件区扫描 | 应用运行时放入新合成 TXT，扫描入库；再次扫描不重复 | desktop/13-reset-and-scan.json |
| 电子书阅读 | 首屏激活、翻页、目录跳转、正常返回提交会话；字体、字号、未安装字体拒绝、默认恢复 | 06～09 截图；03-native-txt-quote.json、04-native-txt-closed.json |
| 原生选区与定位 | 物理拖选、Ctrl+C/Ctrl+S、保存去重、书摘原文回跳复制一致 | actions.jsonl；03-native-txt-quote.json；15-epub-quote-return.png |
| 折叠、全屏、重排 | 三面板收起和恢复、按钮及键盘全屏切换、正文仍可操作；自动化覆盖锚点和有效选区保留 | 06-all-panels-collapsed.png、07-reader-panels-restored.png；完整回归 |
| 完成与重置 | 标记完成后回看不取消完成；重置清除完成标记并回到首章偏移 0，保留历史与书摘且重新激活 | desktop/13-reset-and-scan.json；ui-09-reset-scan.py |
| EPUB 原生图片与续读 | 原生选择器导入、文字选区、2400×1800 图片显示；关闭重启后以非文本块锚点续读；书摘回跳一致 | 13b-epub-image-actual.png、14-epub-image-resumed.png、08b-epub-image-closed.json |
| EPUB 支持与拒绝 | 11 个支持/降级样本、15 个异常样本，来源不变，完整事务往返 | p7-compatibility/compatibility.json |
| AI 当前页解读 | 实际服务返回，结果显示、不新增书摘 | real-page-analysis.json |
| AI 选区整段书摘 | 所选 9 字原文精确保留为一条；整体解读非空；多段与 Unicode 原文保留另由永久回归覆盖 | real-whole-selection.json；AiSelectionWorkflowTests |
| AI 去重、回跳、撤销 | 相同选区再次调用不重复；原文回跳复制逐字一致；撤销回到原有 2 条书摘 | real-selection-deduplication.json、real-quote-loop.json |
| AI 当前页提取/页范围 | 原句均在自建原文中逐字匹配；当前页新增可撤销；全书第 1～1 页实际解读成功 | real-page-extraction.json、real-page-range-analysis.json |
| AI 错误/取消/本地重试 | 真正 TCP 拒绝连接后仍能阅读并正常保存会话；HTTP 超时、断流、错误、过期响应和保存重试由完整回归覆盖 | P9Network-probe.log；full-verified.trx |
| 凭据删除 | 删除后再次解读在缺失密钥处拒绝，阅读与返回保存不受影响；退出后无持久化凭据 | desktop/07-ai-credential-deleted-local-readable.json；credential-cleanup.json |
| 主题 | 实际切换深色/浅色，持久化且备份恢复后本机主题保留 | 16-settings-dark.png、17-restored-dark.png、18-restored-reader-dark.png |
| 演示数据保护 | 非空库加载演示数据明确拒绝、原数据保持；空库演示由完整回归覆盖 | actions.jsonl；完整回归 |
| 完整备份与删除恢复 | 实际导出 ZIP、删除 EPUB 及关联数据、恢复、重启、再次阅读和回跳；10 张业务表与 12 文件摘要一致 | backup-loop.json；09～12 数据快照 |
| 迁移/锁/故障恢复 | 旧 schema1 字段保持并迁移；锁定实例不能写入；真实进程中断后恢复与坏日志保留 | P2/P3/P4-probe.log；对应 process/probe.json |
| 生产架构与资源释放 | 真实 DI/别名/VM 单例/视图缓存/三层导航，20 次切书后布局/文档/图片引用归零 | production-complete/probe.json |

## 性能与真实请求

最终生产探针的预热载入加离屏首绘约 465ms，翻页 P95 约 6.68ms，重排 P95 约 129.87ms。它们是 Skia 离屏采样，不能替代物理显示缩放验收；百万字符冷导入和预热首绘也不是同一指标。

5 次真实调用合计供应商报告输入 845 token、输出 652 token，费用以供应商账单为准。仅上传本轮自建短文/选区，单次正文 9～86 字符，无错误后的自动重发。真实密钥经无回显交互进入隐藏输入框，保存后框清空，结束删除并正常退出。精确扫描本轮证据、运行数据、源码及压缩包解压成员：723 文件、40624 个归档成员，UTF-8/UTF-16LE 密钥匹配 0，无法读取文件 0。该扫描不等于检查整台机器。

## 过程中的驱动与环境问题

- 首次并行构建探针时，运行中的实际应用锁住 DLL，关闭测试应用后按顺序构建并继续，通过；属于本轮执行顺序问题。
- UI 驱动按旧按钮名称、JSON 字段大小写/结构、Toggle/ComboBox 支持的模式定位时出现失败，均核对实际 XAML/UIAutomation 树/序列化结构后修正。原生图片按旧字号的翻页次数定位偏差，补充实际图片截图和 NonTextBlockKey 校验后通过。没有为适配驱动而放宽生产逻辑。
- 新回归初版断言失败时被夹具清理异常遮挡，已用 finally 正常关闭会话后取得准确失败证据。
- 中途的一次完整回归在发现第二个真实缺陷后主动中止，full-final.log 不作为最终结果；最终依据为 full-verified.log 和 full-verified.trx。

## 文件与待复核边界

- 修改 2 个生产源码和 1 个永久测试文件；新增本记录、执行计划和独立证据，更新来源/分析/问题与项目进度记录。
- raw/ 两文件及原有 119 个未跟踪文件做 SHA256 比对；不改写原始资料、不删除用户文件，不把既有未跟踪资料纳入提交。output/ 与旧报告、正式源码包不变。
- 验收结束时修复保留在本地；用户随后明确授权提交并推送远端，本次同步记录见 docs/git-sync.md，实际提交与远端回读结果见 Git 历史和本轮回复。旧 ZIP 尚未包含这些修复。
- 2 项符号链接测试因 Windows 当前账号缺少创建权限而跳过，不算通过。其他显示缩放/跨屏、真实商业 EPUB、复杂 CSS/DRM/固定版式等未扩大验收范围。
- 本轮没有需要用户补齐才可完成的本地功能检查；仍需复核以上权限/环境边界及未来正式打包版本。

## 来源

原始功能资料：raw/1-阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md。当前实现与约束：Code/README.md、当前 Code 源码、docs/20261024-branch-merge-v1.md、既有 P2～P10 协议。课程 DOCX 本轮不重新处理，沿用 docs/source-notes.md 已整理要求。无新增外部调研资料。
