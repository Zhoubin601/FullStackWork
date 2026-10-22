# P0 基线与隔离环境验收

日期：2026-10-22。对应实现与原始证据提交：`fef2575`（feature/reader-native）。远端起点：`0280730`，前轮复核单独提交：`3ab2db2`。本记录随阶段验收文档提交保存。

**阶段结论：P0-01～P0-04 通过；具备进入 P1 本地原生分页实验的前置条件。** Windows 机器配置和字体/DPI 当前不可访问，按规划 P0 步骤 6 挂起，不把 Linux 窗口验证计为 P1-07 或 P10 Windows 通过。本阶段不需要 API。

## 来源与范围

来源为 raw 中的原始电子书总体规划、课程要求 DOCX，以及 output/reader-plan-v2 的总体规划、接口契约和分阶段验收表；补充依据为 docs/20261022-阅读器v2计划执行复核-v1.md 与现有 Code。

用户已授权开始按阶段执行，本轮完成 P0。生产 UI/Library 源码、schema、原始资料、正式规划、既有 output 交付均未修改；新增内容为测试 fixtures、两项回归、生成/桌面验证工具、源码白名单和过程证据。旧 v1 正式交付未重新打包或覆盖。

## 环境

- Debian 13，Linux x64/glibc；CPU 为 AMD EPYC 9V74，详细 SDK/CPU/内存/字体记录见 environment-and-results.json。宿主机报告的资源不是已定版的性能预算。
- SDK 8.0.424、运行时 8.0.30，临时路径 /tmp/readjournal-review-sdk；设置 DOTNET_ROOT 和 PATH 后运行。
- 真实桌面使用独立 Xvfb 21.1.16/Openbox 3.6.1，1200×850×24。临时用户前缀解包系统依赖，未修改系统安装、生产源码或用户业务数据。
- NuGet 首次联网还原后的缓存被工作区和干净源码复验共用；不宣称首次构建无需网络。SDK/NuGet/桌面依赖版本均未更换应用固定依赖。

## 用例结果

### P0-01：干净源码还原、构建及基线

- 对应提交：fef2575。样本：本提交的 123 文件源码白名单，逐文件 SHA-256 见 code-files-sha256.json。
- 步骤：工作区 locked-mode restore → Release build → 全量测试；pack.py 在 /tmp 生成验证 ZIP，解压到全新目录后重复同一命令。
- 预期：当前源码和独立源码均可构建，原测试通过；新增 fixture 回归通过；不依赖 Code 外部资料。
- 实际：两份各 96 通过、0 失败、0 跳过，其中原有 94 项、P0 新增 2 项。无构建错误；旧 SQLitePCLRaw 的 Alpine RID 产生 NETSDK1206 警告，不把本次 x64 结果当成 Alpine 验收。
- 验证 ZIP：123 文件，119,020 字节；SHA-256 为 3b17e29e4c51974d46969112b93bce450f5f5084fd27b8ce53b44ba690075f68。包位于 /tmp，仅是验证产物；未进入 output。包内文件与最终 Code 白名单逐字节一致。
- 状态：通过。证据：restore.log、build.log、baseline.trx、test.log、clean-restore.log、clean-build.log、clean-baseline.trx、clean-test.log、source-package.json。缺陷：无未关闭代码问题。

### P0-02：隔离库真实启动、正常关闭与重开

- 对应提交：fef2575。环境：Linux X11/Xvfb/Openbox。使用 /tmp 下的独立 READJOURNAL_DATA_DIR。
- 步骤：空库和固定 schema 1 库分别启动真实 Release 应用、等待概览、截图，通过窗口管理器 Alt+F4 正常关闭，再次启动并关闭；核对数据库和默认用户目录。
- 预期：可进入主界面；正常退出；旧数据重开不变；默认目录不被写入。
- 实际：4 次正常退出码均为 0。空库四表为 0；旧库行数为 3/3/2/3，字段摘要在两轮间一致，integrity_check=ok、外键错误=0。实际查看截图：旧库概览显示累计 101 页、75 分钟、完成 1 本，近期 3 条记录与在读书籍均来自固定 fixture。
- 默认 /home/agent/.local/share/ReadJournal 运行前后均不存在，未创建或写入。
- 状态：通过（Linux 范围）。证据：ui-with-wm/desktop-smoke.json、4 张真实截图及运行日志、desktop-with-wm.log。
- 失败及处理记录：首次在无窗口管理器环境中直发关闭消息超时，已保留 desktop-smoke.log 和 ui/。该尝试不计通过。改用完整窗口管理器与正常快捷键后 4 次成功；不以强制结束进程作为正常关闭证据。没有据此改动生产关闭逻辑。
- 未覆盖：当前 Windows 环境、原生文件选择器、DPI 与新增阅读器性能。DBus/会话管理/软件 GL 回退诊断和 Openbox 缺菜单提示原样保留，不作为核心流程的通过证据或隐去。

### P0-03：固定旧库/JSON fixtures

- 对应提交：fef2575。样本：2024 年固定日期的纯合成资料，Book/Record/Quote/Plan 行数分别为 3/3/2/3。
- 步骤：由原 schema 1 的真实 DatabaseService 建库，经现有 BackupService 恢复并导出；冻结数据库、完整字段摘要及 SQL statements；独立回归再从冻结 SQL 建库，不调用未来初始化器生成旧 schema。
- 预期：当前服务可读取，主键/文本/时间/页码/收藏/关联全部不变；特殊旧状态可被后续迁移复验。
- 实际：schema=1、format=1、integrity=ok、外键错误=0；全部序列化业务字段往返一致。覆盖稀疏 ID、重叠页段、可空书摘页、emoji/组合文本、收藏状态、未开始/在读/已完成及同书两个历史完成计划。删除关键完成记录会被现有一致性规则整次拒绝，字段保持不变。
- 状态：通过。证据：fixtures/readjournal.sqlite3、fixtures/exported.json、fixtures/manifest.json、fixture-generation.log、baseline.trx 中两项 ReaderBaselineFixtureTests。
- Code 中仅保存小型 schema1.json 和 schema1-sql.json，SQL 保持旧结构；后续迁移不得用修改黄金数据来绕过失败。缺陷：无。

### P0-04：可重复样本与打包边界

- 对应提交：fef2575。生成器版本 1，固定 seed=20261022。
- 步骤：在两个新 /tmp 目录分别生成全部样本，比较 manifest 和 15 文件原始字节；独立核对字符数、编码/换行、EPUB 容器/XML及源码包清单。
- 预期：相同参数字节一致；百万字 TXT、超长段落、UTF-8 BOM/plain、GB18030、LF/CRLF/CR、Unicode 边界以及自建 EPUB 均就绪；大书不入源码 ZIP。
- 实际：15 文件逐字节相同；million.txt 为恰好 1,000,000 Unicode code points；含 5 个自建小 EPUB 变体。源码包不含运行数据库、EPUB 或百万字样本，保留可重建生成器和小型 JSON fixtures。
- 状态：通过。证据：samples-manifest.json、sample-validation.json、source-package.json。EPUB 本轮只做容器/XML检查，没有冒称已通过解析或渲染；相关验收留 P7。缺陷：无。

## 修改与产物位置

- Code/ReadJournal.UnitTest/ReaderBaselineFixtureTests.cs 与 Fixtures/Reader：两项回归及冻结旧格式数据。
- Code/ReadJournal.UnitTest/ReadJournal.UnitTest.csproj：复制测试 JSON；Code/delivery-files.txt：新增 4 个测试/fixture 路径。
- docs/tools/ReaderBaseline：原 schema 1 建库/字段校验工具及锁文件；docs/tools/reader：确定性样本和真实桌面验证工具及使用说明。
- docs/evidence/reader/P0：全部实际测试、窗口、fixtures、环境、摘要和验收记录。
- docs/reader-execution.md 维护扩展阶段状态；docs/plan.md、source-notes.md、analysis-notes.md、questions.md、git-sync.md 与 memory-bank/project-context.md 同步当前进度和后续入口。

## 下一阶段与待补外部条件

下一阶段为 P1 原生分页/选择实验；P0 不实现阅读器。P1 需验证漏字/重复、选择映射、字体/视口重排、渐进分页、取消及性能；不能把本阶段通过外推为新增阅读器通过。

Windows 目标机器配置/字体/DPI 资料未提供，P1-07 和 P10 的实机要求仍挂起。当前不需要用户提交 Key；P9 真实联调前再通知。P0 无阻塞缺陷，当前正式规划无需用户重新审批。

外部依赖来源（访问日期 2026-10-22）：GitHub 的本项目 origin；微软 SDK builds.dotnet.microsoft.com；NuGet 锁定包；Debian deb.debian.org 上的 xvfb/openbox/libobrender/libobt/imlib2。具体临时包版本见环境与工具说明，未据新外部技术资料改写产品方向。
