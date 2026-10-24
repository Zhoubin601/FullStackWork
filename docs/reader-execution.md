# 阅读器扩展执行记录

依据：output/reader-plan-v2 三份规划，以及 docs/20261022-阅读器v2计划执行复核-v1.md。用户于 2026-10-22 授权开始，按阶段实施和验收。

功能分支：feature/reader-native。远端基线：0280730。前轮评估已单独保存为 3ab2db2；阶段代码和证据另行提交。用户随后授权“推送后继续p1”，P0 已推至 feature/reader-native（8465e49），P1 已在本轮按用户授权推至同名远端分支（4b336d3）；P2 已按用户“推送，开始p3”推送至远端8696045；P3已推送16e4155；P4验收完成，阶段提交后推送。用户已追加授权顺序完成并逐阶段推送P5、P6、P7。

| 阶段 | 状态 | 证据 |
|---|---|---|
| P0 基线与环境 | 四项通过（Linux）；实现提交 fef2575 | docs/evidence/reader/P0/acceptance.md |
| P1 原生分页实验 | 通过（用户授权 Linux 验收）；实现 bd6eb50 | docs/evidence/reader/P1/acceptance.md |
| P2 正式契约与迁移 | 七项通过（Linux）；实现 9b24b07 | docs/evidence/reader/P2/acceptance.md |
| P3 完整备份与恢复 | 八项通过（Linux）；实现 17690bb，已推送16e4155 | docs/evidence/reader/P3/acceptance.md |
| P4 受管TXT与扫描删除 | 八项通过（Linux）；实现5358a4a，验收后推送 | docs/evidence/reader/P4/acceptance.md |
| P5～P7 | 已完成并合入main；Linux阶段验收及Windows150%补验通过 | P5/P6/P7/acceptance.md、reader-windows-acceptance-20261023.md |
| P8 | P8-01～10已通过；按授权推送feature/reader-ai-p8 | docs/evidence/reader/P8/acceptance.md |
| P9 | 真实 DeepSeek 联调通过，已推送 9f6ae53 | docs/reader-p9-acceptance-20261023.md |
| P10 | 用户调整后的 Windows150% 发布验收完成，已推送 82070fa | docs/reader-p10-acceptance-20261023.md；reader-progress-20261023.md |

## P0 本轮步骤

1. 固定旧 schema 1 的数据库/JSON/SQL fixtures，记录字段和行数摘要，验证当前应用服务可读。
2. 提供确定性 TXT/EPUB 样本生成器；百万字文本在工作区外生成，不纳入源码包。
3. 锁定还原、Release 构建、现有基线及 fixture 回归，保存 TRX。
4. 用独立 READJOURNAL_DATA_DIR 完成真实窗口启动、正常关闭和重开，并核对默认目录未被写入。
5. 核对干净源码及白名单可重现性，记录 P0-01～04 结果，独立提交。

平台调整：用户明确“你可以用linux，windows保留”，本轮 P1 以 Linux 100/150/200% 实际渲染缩放、字体回退和焦点验证验收。Windows 字体/DPI/跨屏补验保留且未实测，不阻塞当前 P1/P2；不得据此宣布 Windows 支持通过。

P0 结果：工作区与干净源码各 96 项全通过；4 次真实启动/正常关闭、旧库重开字段不变、默认数据目录未写入。旧 schema/JSON fixtures 和确定性 15 文件样本就绪。详细用例、首次桌面失败与处理、平台边界均见 P0/acceptance.md。P0 历史结果保留；P1 当前结果见下节。

## 后续契约修订安排

- P5/P6 前补充阅读器 ResetProgress 的“关闭会话→释放注册→独占重置→新上下文”编排。
- P8 前补充 AI 批次不可变提交回执，不以可级联删除的明细承担重放结果。
- 本轮 P0 不修改业务 schema、分页或 AI 实现。

## P1 当前验收

独立实验实现统一原生排版/绘制/命中/选择、锚点恢复、限额缓存、可取消索引和冻结范围。最终实验 14/14 与生产 96/96 通过，三档实际 Linux 缩放均通过真实拖选/跨页/剪贴板/窗口大小与输入焦点验证。截图复核发现的缺失字体裁切已修复并补回归。八项判据、性能原始样本、失败历史和平台边界见 P1/acceptance.md。该段为 P1 完成记录；P2 当前结论见下节。P9 前仍须主动告知真实 API。

## P2 当前验收

P1 已推送核验远端4b336d3。P2实现及证据本地提交9b24b07，七项通过；最终工作区与145文件白名单独立源码各124/124，真实旧库迁移/重开及空库启动共4次正常关闭；独立子进程占用时零写入、释放/崩溃后可恢复锁。统一状态/计划/统计/重置、三处迁移失败回滚、身份/代际和真实DI/布局契约均有回归。阶段结论见P2/acceptance.md，必要契约补充见docs/reader-p2-contract-addendum.md。

生产schema已到2，旧JSON纯手动路径保留；包含电子书/定位数据时先拒绝旧JSON，以免完整备份尚未实现而丢数据。P3完整ZIP/日志/恢复尚未开始，导入/正式Reader入口继续关闭。Windows保留未测，当前P2未再推送，raw/正式交付未变。


## P3 当前验收（优先于上述历史状态）

用户“推送，开始p3”已执行：P2远端feature/reader-native=8696045c6361e1670ca55e05e816526b7d81c0f3。P3完整ZIP/旧JSON输入、完整自动备份、staging/trash/日志和启动恢复、单例缓存与代际失效已实现，本地代码/证据17690bb，八项通过。最终工作区与159文件白名单独立源码各169/169（原124+新增45）；四处真实杀进程重启分别确认完整旧集/新集，损坏日志阻止业务并保留两套文件及数据库业务行。

最终真实桌面验证ZIP导出/确认恢复/重开，独立解包应用再次启动，3次正常关闭、默认目录未写入；文件与业务哈希及完整自动备份均一致。详细失败历史、资源限额和边界见P3/acceptance.md及docs/reader-p3-protocol.md。本轮P3未推送，P4未开始；Windows保留未测，API零调用，raw/正式交付/参考/冻结fixtures未变。


## P4 当前验收（优先于历史状态）

P3已按用户要求推送16e41558e8ceb4549274dd26cc6b0321e058350d。P4代码/工具/证据5358a4a，八项Linux通过：严格TXT/分块、受管导入/唯一去重、扫描/Missing/孤立隔离、统一删除、导入/删除日志和同连接启动恢复、实际TXT完整备份往返。最终工作区与169文件独立源码各217/217（原169+48），10处真实进程中断通过；最终桌面导入/编码/去重/删除/恢复/重开、Missing及独立构建启动共4次正常关闭，3书/11文件/5来源哈希一致。

阶段验收见P4/acceptance.md，契约补充见docs/reader-p4-protocol.md；InboxSeen为本机收件处理记录，防止保留来源的已删除书自动重现。P4验收文档提交后正常推送并回读。用户最新授权继续P5、P6、P7，每phase验收并推送后再继续；P5尚未开始，Windows保留未测，无API，raw/正式交付/参考/冻结fixtures不改。


## 2026-10-22 P5阶段结果（优先于历史状态）

P4已推送并回读69b783abab2dfa02984121fa878da2fd6c74d692。P5实现正式原生Reader/首屏开始会话/SessionId幂等事务关闭/CloseFailed重试继续、所有运行期异步离开门禁、正文锚点重排/偏好/可取消索引。验收见docs/evidence/reader/P5/acceptance.md；接口补充见docs/reader-p5-protocol.md。Linux工作区与独立白名单源码完整回归，真实桌面两次正常关闭、偏移442重启一致、字号28保留；20次切书布局每次清零。P5完成提交后按授权正常推送并回读，再开始P6，随后P7，每阶段均验收/推送。Windows仍保留未测，无API；raw/output/参考/冻结fixtures保持。


## 2026-10-23 P6当前结果（优先于历史状态）

P5已推送af7ebb60bb65498628cd3a9631a82b92de7701c2。P6原子定位书摘/按范围去重/保留原文与精确回跳、统一历史/毫秒统计图、完成与原子重置已实现。工作区完整243项通过；独立181文件白名单源码重验，真实桌面M1闭环10张业务表/4个受管文件与来源SHA一致、续读偏移294，续接桌面两次正常关闭。详见docs/evidence/reader/P6/acceptance.md与reader-p6-protocol.md。验收提交后正常推送并核对真实远端，然后按用户授权继续P7；Windows保留未测，无API，raw/output/参考/冻结fixtures不变。


## 2026-10-23 P7完成验收（优先于历史状态）

P6已推送并回读34f07e46c7ca56b159ea1e689604836fc1d1992a。P7锁定VersOne.Epub3.3.6/Unlicense与AngleSharp1.8.5/MIT，实现安全有界EPUB适配、spine/嵌套目录/稳定键、原生基本样式/图片页/亲和锚点/纯文本抽取，复用P5/P6会话与书摘闭环。工作区272项通过；独立186文件白名单源码再验。11个确定性支持/降级变体逐本完成会话/书摘/完整ZIP删除恢复回跳，15类异常均拒绝且来源不变；真实Linux窗口两次正常关闭、大图2400×1800缩放、偏移669/b-17图片续读、28字号书摘高亮通过。

验收：docs/evidence/reader/P7/acceptance.md；契约边界：docs/reader-p7-protocol.md；逐本兼容表：docs/evidence/reader/P7/compatibility.json。真实书籍资料未提供（覆盖0），图片实测PNG，其他图像格式未逐类样本验收，Windows保留未测，不推断市场兼容率；无真实内容API。原始资料/正式交付/参考/冻结fixtures保持。最终提交后正常推送feature/reader-native并回读；P4/P5/P6此前均已按阶段推送，用户本轮授权到P7止，P8以后未开始。


## 2026-10-23 Windows 补验（优先于上述保留未测状态）

按用户授权验收feature/reader-native并通过后合并main。本机Windows11 x64、实际150%DPI：生产及独立186文件源码均270通过/0失败/2符号链接权限跳过；原生实验14/14，真实拖选/Shift跨页复制/焦点/重排通过；旧库迁移/锁、4处恢复/10处导入删除进程中断通过；11个EPUB变体往返及实际TXT/EPUB完整UI闭环通过。首次时区样本错误、权限表达及开发探针锁文件已修正。完整验收说明docs/reader-windows-acceptance-20261023.md，证据docs/evidence/reader/windows-20261023。当前已测范围允许合并；100%/200%与跨屏/真实商业EPUB仍未测，P8以后未实施，旧正式交付保持。

## 2026-10-23 P8当前验收（优先于历史状态）

P8从已合并main=cd3386b实施，分支feature/reader-ai-p8。默认Mock完整走HTTP序列化/结构解析，当前页/选区/页范围快照、原句核验、受保护批次存储、回执幂等、失败只重试本地保存、撤销、会话内密钥和Reader取消/过期隔离均已接入。验收P8-01～10通过，工作区与200文件独立源码各327/327，55项P8新测试；真实Linux窗口有分析0写入、自动保存、保留收藏/删除未改项、原文高亮及379字符页范围分析证据。详见P8/acceptance.md及reader-p8-protocol.md。

本轮按用户要求完成后推送feature/reader-ai-p8，推送结果按真实Git回读；不自动合并main。真实内容API调用0，P9/P10未实施，P8新增Windows实机未测，现有output保持。


## 2026-10-23 P8复核与P9核心联调（当前）

远端P8=4baef83已拉取，当前本地codex/reader-p9。Windows补P8分析/生成/撤销/重开，修复测试扫描独占SQLite问题；P9官方模型与配置、真实分析/解释、修订后提取2条/定位/撤销、取消/超限/凭据删除通过。实际TCP失败时本地阅读与正常会话通过（进程独立失败代理，非拔网线）。最终334项332通过/0失败/2权限跳过，200文件独立源码一致。首轮真实提取全拒绝与修订历史保留，不复用成通过证据。验收reader-p9-acceptance-20261023.md；Key已删、Mock恢复、应用正常退出，未合main/未推送。P10未开始，raw/output未变。


## 2026-10-23 P10当前交付

P9已推送origin/codex/reader-p9=9f6ae53；P10在codex/reader-p10完成。用户跳过其他缩放补测，按实际150%范围验收；工作区与独立源码各334项332通过0失败2权限跳过，152架构/性能探针、14原生实验、旧库迁移/14处实际中断、新库/迁移库实际UI完整闭环通过。最终202文件源码ZIP已新增至output/reader-release-v1；正式大小和SHA见reader-p10-acceptance-20261023.md。生产功能代码不改；本轮只更新发布文档/索引/白名单及工具。旧Word/PDF/ZIP和raw保持，不合main、不自动推送P10。

## 2026-10-24 字体与AI清理

最新实现、Linux339项与窗口验证以及待补Windows/真实API条件见docs/font-ai-acceptance-20261024.md。本轮功能分支feature/reader-fonts-live-ai，不将旧版Windows验收冒充当前变更通过。
