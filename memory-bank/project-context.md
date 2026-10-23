# 已确认的项目背景与约束

## 2026-10-22 阅读器扩展已授权，P0通过

- 用户要求开始执行并一阶段一阶段推进。后续入口为 docs/reader-execution.md，依据 output/reader-plan-v2 三份规划；已在feature/reader-native分支开展，未推送。
- P0实现/原始证据提交fef2575；四项验收通过（Linux）。工作区与干净源码各96项通过，旧schema1/JSON/SQL fixtures冻结，15确定性TXT/EPUB样本可重建，空库/旧库4次真实启动和正常关闭通过，默认目录未写入。证据见docs/evidence/reader/P0/acceptance.md。
- 当前P1未开始；Windows目标机配置及字体/DPI未取得，P1-07/P10必须另行实测。生产逻辑/schema和既有output交付未变，正式v1仍为原94项版本，不能把P0新增回归当成新版阅读器功能已完成。
- 后续P5/P6前补重置时先结束活动会话的编排；P8前补AI批次不可变回执。P9前通知真实API需求，聊天不接收Key。

## 2026-10-21 统一v1正式交付（当前最新）

- 用户要求更新报告、新源码包、清除旧交付并统一v1目录。已生成output/v1内项目报告v1.docx/PDF（23页）及源码v1.zip（119文件114395字节，SHA256 be5d57de1e6001122a2299b9269e1aca0873bd9a134a53a4e83afbf2a0229187）。正式文件与当前源码配对，包含返回入口修复。
- 以用户保存Word v3为底稿，保留微调/未改正文/两张SVG图，同步94项测试和71项本轮独立探针、包信息及返回截图。独立解包还原/构建/94项和空库启动通过；Word原生更新目录、PDF全部23页验证。未改变应用逻辑，仅README同步。
- 自动审批拒绝永久删除旧文件，故将原output内Word v2/v3、源码v5安全移至docs/report/release-v1/previous-deliveries。output仅保留v1目录，旧文件是归档未永久删除。完整结果见docs/release-v1-20261021.md、docs/report/release-v1/validation.json及cleanup.json。原始资料和业务数据不动，未推送Git。
- 后续以用户当前保存的output/v1 Word为唯一编辑底稿，不能从旧tex覆盖；此前v5/Word v3和“尚无PDF”均是历史。

## 2026-10-21 返回按钮修复（最新工作区代码）

- 左上角返回在模块首页原来仍可点击但无操作；已新增CanGoBack、栈变化通知与命令执行门槛，仅有上一页时显示按钮，保留菜单清栈和主VM持栈。修改MainViewModel/MainView及两份测试，无架构替换。
- 新增真实MainView绑定回归先红后绿，全套测试现为94通过；Windows隔离数据实际验证空编辑返回、详情返回、编辑→详情→书库及菜单首页隐藏入口。修复后应用已用默认用户数据重新启动，用户数据未改。过程见docs/back-navigation-20261021.md。
- 正式Word与v5源码ZIP未更新，v5不包含本次修复；后续提交打包需生成新版本，不能再声称旧v5与当前Code一致。此前93项和报告统计为当时版本历史结果。

更新日期：2026-10-21。

## 最新编辑交付（2026-10-21 Word用于用户微调）

- 用户要求“输出word即可，我进行微调”，已生成output/20261021-阅记-项目报告-v2.docx，沿用最新报告v2正文与证据。21页经本机Microsoft Word原生渲染检查、自动目录及页码更新，officecli结构验证通过；正文、代码、7表格及结构图可编辑，19截图独立保留，未加保护。
- 后续应以用户微调后的Word为准，不能从旧tex/生成脚本覆盖用户修改。本轮没有更改应用、测试、README、源码包或正式PDF；过程与验证见docs/report/word-v2/。用户自行删除的历史交付不恢复。

## 当前最新交付（2026-10-21 报告与README同步，优先于历史交付状态）

- 用户授权修改报告和README后，Code仅README变化，应用/测试/依赖未改。最新源码为output/20261021-阅记-源码-v5.zip：119文件113557字节，与v4相比仅README改变。
- 最新正式报告为output/20261021-阅记-项目报告-v2.pdf：21页，已纳入Windows本机93项测试、71项架构探针及真实截图、正确源码包信息；旧报告仍保留历史含义。此前报告和README未同步的问题已关闭。
- v5沿用相同代码的Windows工作区与v4独立解包两份93项结果，没有为README变化再跑一次测试。新包逐文件核验、PDF全部21页渲染及章节/字体/页界验证通过，用户需最终核对封面与提交配对。
- 最新记录docs/report-update-20261021.md，PDF可编辑来源docs/report/report-v2.tex，生成与验证方法docs/report/README.md。本轮未提交/推送。

## 当前最新状态（2026-10-21 Windows 本机验收，优先于历史平台边界）

- 当前源码基准d6c3f8a，本轮只检查和留证，未修改业务源码、正式PDF或ZIP，未提交/推送。
- 已实际在Windows x64本机还原、Release构建及93项测试全通过；v4源码包独立解包后同样93项通过，实际窗口启动与初始化正常。71项真实容器/反射/导航探针通过，数量及八项架构机制对应。
- 真实Windows界面完成新库录入、非法页码校验、书摘收藏、计划、统计、原生导出恢复、级联删除后恢复、深色主题及正常关闭重启。隔离数据库四表数据往返一致，完整性正常。旧记录的“Windows未实测”已在本轮解除。
- 最新源码包仍为output/20261021-阅记-源码-v4.zip（119文件113136字节），与Code逐字节一致。正式PDF仍保留旧68项测试、Windows未实测及旧源码包信息，需在正式提交前同步；README也保留旧平台说明。本轮未重做正式文件。
- 本轮结论与复现证据见docs/local-audit-20261021.md和docs/evidence/local-windows-20261021/；不能用本机一次测试宣称软件绝无缺陷。

## 当前状态（2026-10-21 P1修复，优先于下文）

- 用户要求处理P1，已直接修复：IQuoteStorage.ToggleFavoriteAsync在单事务内仅更新收藏位/时间；基类忙碌时拒绝离开，实际菜单/内容导航在注参前挡住忙页，覆盖直接Add/Edit。老师DI、单例、三层导航与主VM栈保持。
- 完整Release93项通过（原83+新增10），v4独立源码包锁定还原/构建/93项同样通过；架构探针71项通过。真实UI的慢IO收藏→菜单/新增被拦→松锁完成→原生选择器恢复→重启数据核对完成。当前这项P1已关闭，不沿用下文修复前“未关闭”表述。
- 最新源码`output/20261021-阅记-源码-v4.zip`（119文件113136字节）；详情`docs/p1-fix-20261021.md`。旧包、原始资料、参考及PDF保留；PDF仍为前轮v2/68项历史报告，本轮未重做。Windows未实测。修复完成时未推送，用户随后明确要求“提交至远端”，本轮全部复核和修复成果纳入本次提交，目标origin/main；核验方式见`docs/git-sync.md`及最终回复。

## 历史状态（0313917独立复核，P1修复前）

- 最新代码已从origin/main快进同步至`0313917`。本次任务为架构与功能检查，未修改应用源码、正式包或PDF，也未推送。
- 核心八项架构与严格参考机制对应；本次重新构建及83项现有测试通过，真实App架构探针71项通过。
- **有新发现的P1未关闭问题**：书摘切换收藏尚在等待数据库时可切设置恢复，旧Get→Save在恢复两侧执行会覆盖恢复后同ID书摘正文/感想/页码。实际VM+SQLite、单线程UI调度及慢IO门禁控序已复现，不是桌面现场复现。不要继续引用旧审查“无未修复缺陷”作为当前结论。
- 后续先读`docs/latest-recheck-20261020.md`及`docs/business-recheck-20261020.md`，架构逐项对应见`docs/architecture-recheck-20261020.md`。历史修复和交付记录仍保留其原阶段含义。

- 项目：个人离线阅读与书摘管理；Avalonia/.NET8、MVVM、SQLite。Code放源码和测试，过程资料在docs，raw保持只读。
- 用户要求严格采用DailyPoetryA真实调用结构：ServiceCollection注册→BuildServiceProvider→IServiceProvider→定位器属性解析；服务/VM单例；ServiceLocator.Current从应用资源取得；三层导航和常量、MainViewModel菜单和返回栈、统一SetParameter、反射和按类型缓存ViewLocator。不得用壳VM、宿主或页面工厂替代。
- 参考基线为63类、10接口、9视图；课程最低45类、7接口、9视图。当前修复后应用66类、14接口、10业务页。
- 本轮用户已授权修复全部已发现缺陷并提供报告身份；身份已填入正式报告，不在此长期背景中重复保存。编辑加载/保存输入保护、首次成功读取门禁、新建ID回填、重试、计划/备份一致性已修复。
- 最新架构/功能复核：概览VM内查询已迁入IReadingRecordStorage；删除固定目标并防止旧完成事件弹走新页面，Loaded等待旧操作后加载最新参数；设置页数据操作期间阻止切页/关闭。核心DI、三层导航及主VM栈保持原结构。
- 最新Release正式测试83项全通过；源码v3全新解包锁定依赖还原、构建与同一83项通过。本轮旧版基线68项、删除和Loaded中间版76项仅为历史阶段证据。Windows桌面未实测，课程指导书未指定OS。
- 最终DLL独立架构探针71项通过；本轮真实Linux窗口完成新库业务闭环及最终版重启、编辑/删除/恢复，删除后四表全0，恢复后各1，绑定诊断0，证据见最新UI审查。
- 当前源码交付：output/20261020-阅记-源码-v3.zip（118文件109424字节）。output/20261020-阅记-项目报告-v1.pdf（19页）仍是前轮报告，未为本轮审查重做；最新结论读docs/architecture-flow-review-20261020.md。旧版ZIP和报告草稿保留。
- 继续时先读docs/architecture-flow-review-20261020.md和docs/architecture-strict-audit-20261020.md；此前失败证据不代表修复后的状态。本轮开始及推送前fetch确认HEAD与origin/main均20d9c67；用户随后明确要求“推送远端”，本轮成果纳入本次提交，目标origin/main；授权及核验方式见docs/git-sync.md。

## 2026-10-21 Word v3 已确认交付

- 用户要求架构图、实体类图使用实际绘图，不要用表格拼图；此偏好适用于后续报告图形修订。
- 当前最新Word为output/20261021-阅记-项目报告-v3.docx，基于用户已保存的v2继续编辑，保留其微调。架构图与实体关系图为SVG矢量图并附PNG兼容图，其他正文、5数据表、19原始截图保留。22页经本机Word原生更新目录/页码与逐页渲染验证，原始资料及v2未变；构建、验证及渲染在docs/report/word-v3/。
- 后续仍以用户当前保存的Word为准，不要从旧tex脚本重建覆盖微调。v3仅报告绘图与排版修订，源码包仍v5。


## 2026-10-22 电子书扩展规划交付

- 用户要求生成完整的原生阅读器与 AI 助手规划，明确接口、统一架构、阶段分布、逐步验收；需要使用真实 API 时主动告知。
- 完整规划在 `output/reader-plan-v2/`，包含总体规划 v2、接口与数据契约 v1、分阶段实施与验收执行表 v1。旧 v1 正式项目交付保留。
- 本轮只编制规划，未启动功能开发、未调用 DeepSeek；后续执行从 P0 环境/基线和 P1 原生分页实验开始。真实 API 安排在 P9 前通知，不在聊天收集 API Key，应用设置页安全配置。

## 当前阅读器扩展状态（2026-10-22，优先于历史未实施状态）

- 分支 feature/reader-native，P0 已推送远端 8465e495496702054474b1f03c45ddf96c90856a，main 未改。
- P1 独立原生分页实验通过，代码及证据本地提交 bd6eb50；14项实验与96项原生产测试通过，Linux实际1/1.5/2缩放与真实输入/剪贴板/窗口/字体检查通过，详见docs/reader-execution.md和docs/evidence/reader/P1/acceptance.md。P1 尚未推送。
- 用户明确“你可以用linux，windows保留”：当前阶段使用Linux验收，Windows检查工具保留且未运行，不阻塞当前P1/P2；不可宣称Windows兼容已通过。
- P2 尚未开始，下一步正式契约/兼容读模型/schema2；P1不接入生产Code/SQLite/AI。保留老师DI/单例/三层导航，raw和既有正式交付不改。后续ResetProgress会话释放及AI不可变提交回执两项契约需补；真实API至P9前主动通知。

## 当前阅读器扩展状态：P2已通过（2026-10-22，优先于上条）

- 按用户“推送，然后开始p2”，P1已推至origin/feature/reader-native，远端SHA4b336d372c40526a8c4f482ba2c40c5c3da262fa；main未改。
- P2实现/证据本地9b24b07，七项通过；生产schema2，最终工作区和145文件白名单独立源码各124/124（原96+新增28），4次真实Linux启动/正常关闭、旧字段保持、三处迁移回滚、独立进程写入互斥/崩溃锁释放及代际通过。详见docs/evidence/reader/P2/acceptance.md。P2尚未推送。
- 正式契约补充见docs/reader-p2-contract-addendum.md：懒分页块引用+可空章内页号、DescribeAsync、显式租约SQL重载、CurrentPercent标量、AI不可变回执。原DI/单例/导航保留。
- P3尚未开始；完整ZIP/日志恢复通过前不启用导入，Reader正式入口继续关闭。P2文本目录/布局为受限纯文本基础；受管文件/P5会话与输入/P7图片/P8批次按阶段落实。旧JSON纯手动可用，含电子书/定位数据时明确拒绝旧格式以免丢失。
- Linux当前验收，Windows保留未测；raw/正式交付/参考/冻结fixtures不变，P9前主动通知真实API。


## 当前阅读器扩展状态：P3已通过（2026-10-22，优先于上条）

- 用户“推送，开始p3”已执行：P2推至origin/feature/reader-native，真实远端8696045c6361e1670ca55e05e816526b7d81c0f3；main未改。
- P3实现/工具/证据本地17690bbf52b84f09150882332500fa4d4127654a，八项Linux验收通过；工作区和159文件独立源码各169/169（原124+45新增）。四点真实杀进程恢复完整旧集/新集，日志损坏阻止业务并保留两套文件及数据库业务行；最终桌面ZIP导出/恢复/重开与独立源码应用启动、哈希均通过。详见docs/evidence/reader/P3/acceptance.md；本轮P3未推送。
- 完整备份ZIP v2、旧JSON仅输入、完整自动备份、books/managed替换、日志/LastRestoreId及启动同连接恢复已实现。canonical v1、安全路径/限额/位置百分比、cleanup-only语义及DI清理约定见docs/reader-p3-protocol.md。保留收件原件/主题/secure，未知或损坏日志不能静默删除。
- 下一阶段P4受管TXT导入/扫描，尚未开始；需复用文件层及扩展操作恢复分派，导入/正式Reader入口仍关闭。P7完整图像解码、P5会话采集、P8批次写入按阶段做；P9前主动通知真实API，当前零调用。
- 用户仍按Linux逐阶段验收，Windows保留未测；raw/正式交付/参考/冻结fixtures未变。后续不要引用P2旧JSON限制或P3中间168项fixture当作当前实现结论。


## 当前阅读器扩展状态：P4已通过，获授权连续完成P5～P7（优先于上条）

- P3已推送origin/feature/reader-native=16e41558e8ceb4549274dd26cc6b0321e058350d；P4实现/证据5358a4a9620dd8b8708e657c7f44e0e3bfe53844，八项Linux通过，验收文档另行提交后在本轮推送并回读。继续时先核对真实git状态。
- P4工作区/169文件独立源码各217/217（原169+48）；10处真进程中断，实际TXT完整初始/关联往返及最终桌面/Missing/独立构建4次正常关闭通过。见docs/evidence/reader/P4/acceptance.md、reader-p4-protocol.md。TXT严格UTF8/BOM/显式GB18030、字符簇安全分块，受管导入/扫描/Missing/孤立隔离、统一删除已接入；Reader路由尚未开放。
- InboxSeen在AppMetadata中保存本机收件上下文，不随业务备份替换，避免保留来源的已删除书自动重现；显式文件选择器仍可再导入。Missing不能生成完整自动备份，需补回文件或显式删除损坏条目再恢复旧完整备份。删除先SQL再移trash，已确认无引用的IO清理失败留日志重试；信息不足停止业务。
- 用户最新明确要求：P4完成推送后继续完成P5、P6、P7，每phase独立验收、提交、推送后再继续。无需反复询问推送或平台授权；不得停在P4。P5尚未开始，P8以后尚未授权，真实API到P9前主动通知。
- Linux当前验收，Windows保留；raw/output/参考/冻结fixtures不改。SDK与桌面工具现放/workspace/.readjournal-env以便环境续接，临时源码验证包不作为正式交付；最终证据的源及工具SHA已记录。


## 2026-10-22 P5阶段结果（优先于历史状态）

P4已推送并回读69b783abab2dfa02984121fa878da2fd6c74d692。P5实现正式原生Reader/首屏开始会话/SessionId幂等事务关闭/CloseFailed重试继续、所有运行期异步离开门禁、正文锚点重排/偏好/可取消索引。验收见docs/evidence/reader/P5/acceptance.md；接口补充见docs/reader-p5-protocol.md。Linux工作区与独立白名单源码完整回归，真实桌面两次正常关闭、偏移442重启一致、字号28保留；20次切书布局每次清零。P5完成提交后按授权正常推送并回读，再开始P6，随后P7，每阶段均验收/推送。Windows仍保留未测，无API；raw/output/参考/冻结fixtures保持。


## 2026-10-23 P6当前结果（优先于历史状态）

P5已推送af7ebb60bb65498628cd3a9631a82b92de7701c2。P6原子定位书摘/按范围去重/保留原文与精确回跳、统一历史/毫秒统计图、完成与原子重置已实现。工作区完整243项通过；独立181文件白名单源码重验，真实桌面M1闭环10张业务表/4个受管文件与来源SHA一致、续读偏移294，续接桌面两次正常关闭。详见docs/evidence/reader/P6/acceptance.md与reader-p6-protocol.md。验收提交后正常推送并核对真实远端，然后按用户授权继续P7；Windows保留未测，无API，raw/output/参考/冻结fixtures不变。


## 2026-10-23 P7完成验收（优先于历史状态）

P6已推送并回读34f07e46c7ca56b159ea1e689604836fc1d1992a。P7锁定VersOne.Epub3.3.6/Unlicense与AngleSharp1.8.5/MIT，实现安全有界EPUB适配、spine/嵌套目录/稳定键、原生基本样式/图片页/亲和锚点/纯文本抽取，复用P5/P6会话与书摘闭环。工作区272项通过；独立186文件白名单源码再验。11个确定性支持/降级变体逐本完成会话/书摘/完整ZIP删除恢复回跳，15类异常均拒绝且来源不变；真实Linux窗口两次正常关闭、大图2400×1800缩放、偏移669/b-17图片续读、28字号书摘高亮通过。

验收：docs/evidence/reader/P7/acceptance.md；契约边界：docs/reader-p7-protocol.md；逐本兼容表：docs/evidence/reader/P7/compatibility.json。真实书籍资料未提供（覆盖0），图片实测PNG，其他图像格式未逐类样本验收，Windows保留未测，不推断市场兼容率；无真实内容API。原始资料/正式交付/参考/冻结fixtures保持。最终提交后正常推送feature/reader-native并回读；P4/P5/P6此前均已按阶段推送，用户本轮授权到P7止，P8以后未开始。


## 2026-10-23 Windows 阅读器验收

feature/reader-native基准e50612b的P0～P7在Windows11 x64/.NET8.0.424/实际150%DPI补验通过，用户授权通过后并入main。生产工作区/186文件独立源码各270通过、0失败、2个符号链接权限跳过；原生实验14通过及真实鼠标/Shift跨页/焦点/重排通过。原有手动业务、TXT/EPUB续读与定位书摘、完整ZIP UI删除恢复及真实进程故障恢复有证据。说明docs/reader-windows-acceptance-20261023.md，原始证据docs/evidence/reader/windows-20261023。仍未覆盖Windows100%/200%与跨屏、真实商业EPUB；P8～P10未实施。现有正式报告和源码ZIP没有更新，不将旧交付认作当前扩展源码。

## 2026-10-23 P8已完成（优先于历史状态）

本轮用户明确“继续p8，完成后推送远端”。从main=cd3386b建立feature/reader-ai-p8；P8范围快照/默认Mock/DeepSeek HTTP适配/精确原句核验/原子自动入库/去重/不可变回执/本地保存重试/安全撤销、AI来源、设置与隐藏会话密钥、取消及过期响应隔离均完成。P8-01～10通过，Linux工作区与200文件独立源码Release各327通过、0失败、0跳过（原272+新增55）。真实Linux窗口验证分析不写库、生成/收藏保留/未改项撤销/原文高亮及全书1～2页379字符Mock分析。证据docs/evidence/reader/P8/acceptance.md，契约docs/reader-p8-protocol.md。

凭据仅进程会话内存，退出需重输；默认Mock无真实内容API调用。P8新增Windows实机保留，P7的Windows150%结果不代替P8；P9/P10未开始，真实API当前接口/费率及受控调用后续另行说明，不在聊天收Key。raw/output/参考/冻结fixtures保持，旧正式报告和ZIP不代表P8源码。本轮完成后按授权推送feature/reader-ai-p8，继续前核对真实远端SHA；未经指令不自动合并main。
