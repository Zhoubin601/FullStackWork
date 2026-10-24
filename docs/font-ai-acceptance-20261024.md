# 字体选择与真实 AI 清理：本轮验收记录

日期：2026-10-24。基线：82070fa，来自 origin/main。用户要求丰富字体选择、剔除 AI 占位、重新验证全部功能，并于完成后推送远端。

**当前结论：功能实现和 Linux 可执行验证已完成；Windows 真机重验及本轮真实 DeepSeek 联调仍待补，不能宣布全部验收通过。**

## 实现

- 系统字体目录通过独立 IReaderFontCatalog 注入，保持原 ServiceCollection、单例、ServiceLocator、三层导航和反射 ViewLocator。搜索本机全部字体，推荐已安装中文字体优先，本环境331种；候选项和中英文预览均使用真实字体。
- 常用字号支持选择与手动输入，恢复默认字体/22字号；未安装字体不能写入偏好，输入中的候选名称不改变正文。应用字体会重新分页并保持正文锚点，偏好跨阅读会话保存。Windows中文后备链增加 Microsoft YaHei。
- 生产移除 UseMock 配置、设置开关、模拟 HTTP Handler 和模拟结果分支。旧配置多余 UseMock 字段忽略，保存后清理；凭据缺失或未同意上传会明确报错。AiMockHandler仅保留在测试工程，生产程序集无该类型。真实分析、解释、生成/去重/撤销/取消与本地保存重试逻辑保留。
- 本轮没有保存用户密钥，没有向代理拒绝的目标发送凭据。旧正式源码ZIP、Word/PDF及raw均未修改。

## 实测结果

| 范围 | 本轮结果 | 证据 |
|---|---|---|
| 全量Release | 工作区339通过、0失败、0跳过；独立206文件源码解包/锁定还原/构建/同一339项通过 | final-test.log、clean-final-test.log、tests/*.trx |
| 新增回归 | 字体切换重排/锚点/偏好续读；未安装字体拒绝且布局不变；搜索过程预览可渲染；旧Mock配置不能模拟返回；测试Handler独立序列化解析 | ReaderLifecycleTests、AiHttpTests，原334项现339项 |
| 架构与性能 | 最终生产探针153项通过，首屏口径约480.4ms，翻页P95约12.0ms，重排P95约149.8ms；释放检查通过 | production-quiet/probe.json；Skia离屏采样，不是物理显示验收 |
| 迁移与恢复 | 旧schema1字段保持/迁移/跨进程锁通过；4处恢复和10处导入删除真实进程中断恢复通过 | ReaderP2Probe-fixed.log、p3-process/probe.json、p4-process/probe.json |
| EPUB | 11个支持/降级变体及异常拒绝、完整备份往返通过 | p7-compatibility/compatibility.json、ReaderP7Probe.log |
| 网络故障 | 真实HttpClient通过进程专属拒绝连接代理得到Offline，本地正文可读并正常提交会话；无真实Provider调用 | offline-network.log |
| 应用窗口：旧业务 | 实际新增手动书、非法页数拒绝、手动记录/书摘收藏/计划、合并统计、级联删除后恢复、浅深主题、正常关闭和重开 | desktop/04～13、27、37、40～45及actions.jsonl |
| 应用窗口：阅读器 | TXT收件扫描/续读/目录/拖选保存书摘/回跳高亮/标记读完/全屏；331字体搜索、预览、衬线28字号应用、缺失字体拒绝、默认恢复；EPUB原生文件选择器导入/图片续读，独立构建大图2400×1800实际显示与续读 | desktop/14～36、46～51；最终独立构建clean-desktop-app.log中无Binding错误 |
| 应用窗口：备份 | 实际完整ZIP导出→删除→恢复；10张业务表一致，全部受管文件SHA一致；数据库完整性ok、无外键错误 | desktop/restore-comparison.json、restored-files-comparison.json |
| 应用窗口：AI | 设置无Mock入口；当前页上传范围确认；无密钥时提示且无模拟内容、无新增书摘 | desktop/11、29、30 |

所有窗口均为Debian13 x64 + X11/Xvfb + Openbox虚拟桌面上的真实Avalonia应用，不是Windows物理真机。本轮没有取得物理DPI/跨屏数据。已有Windows/P9成功记录属于旧版本，不替代这次修改后的重验。

## 未完成的验收条件

1. 云环境HTTP白名单未放行 api.deepseek.com；两次无凭据连通检查均在CONNECT时收到403，未抵达Provider。用户已提供测试凭据与不限次数授权，但本轮实际Provider调用0，密钥未写入文件/源码/日志。需通过云环境配置放行域名后，才能继续真实分析/选区解释/范围分析/生成/去重/回跳/撤销等联调。不得绕过代理策略。
2. 当前没有连接Windows执行环境。本轮Windows字体列表、输入、全屏、排版与完整业务流程需要在真实Windows上补验；历史150%结果不计作本轮通过。
3. 真实商业EPUB与其他物理缩放覆盖沿用既有边界。

本轮源码候选包仅用于独立验证，位于工作区外，不作为验收完成的正式output交付。206文件、326325字节，最终摘要见source-final.json（初轮候选摘要保留）；全部与Code逐文件一致。待补验状态随实现推送至功能分支feature/reader-fonts-live-ai，不将其标为全面验收的正式发布。

## 测试工具失误与修正

- P2首轮误写fixture文件名schema1.sql.json，探针FileNotFound，未完成验收；改为实际schema1-sql.json后重跑通过，原失败日志保留。
- 首次真实窗口发现字体动态绑定无法将字符串转换为FontFamily，导致预览/候选行不可见；新增显式转换并补动态绑定回归，最终窗口已显示候选与预览且无Binding错误。原失败截图和日志保留。
- AutoComplete弹出候选时，点击外部首先收起弹层。首轮截图18仅为候选，尚未保存排版；后续通过Enter/Tab确认并实际点击应用，22截图及reader-preferences确认为Noto Serif CJK SC/28/43.4。
- 桌面驱动首轮把两个按键名称作为一个序列传给xdotool而失败，修正拆分后继续；全屏退出恢复原始窗口大小后重新最大化，未将空坐标点击视为功能通过。46截图是启动未就绪时的概览，后续47～51才是独立应用的真实EPUB/字体验收。
- 首轮桌面快照只捕获7张表，后续以备份data.json与恢复数据库补比对QuoteOrigin/AiGenerationBatch/AiGenerationBatchItem，最终10表结果保存在restore-comparison.json，未声称AI批次实机已产生数据。

- 最终繁忙环境性能样本production-final重排P95为1150.6ms，超过500ms目标；结束其他构建/测试后单独运行同一二进制，production-quiet测得149.8ms，153项检查及全部性能目标通过。两组原始样本都保留，不将单次安静环境结果外推为所有负载下的保证。编译服务关闭命令等待未退出，针对该关闭命令结束等待后直接运行已构建探针，未杀应用掩盖正常退出失败。
