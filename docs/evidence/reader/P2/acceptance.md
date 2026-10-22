# P2 正式契约、兼容读模型与 schema 2 验收

日期：2026-10-22。结论：**按用户授权的 Linux 平台通过 P2-01～07，可以进入 P3；P3 尚未开始。** 导入、正式 Reader 和真实 API 入口未开启。Windows 保留为后续兼容性实测，未宣称通过。

P1 已按用户“推送，然后开始p2”正常推送至 origin/feature/reader-native，核验真实 SHA **4b336d372c40526a8c4f482ba2c40c5c3da262fa**。P2 实现/工具/证据本地提交 **9b24b0731eac3818ee6bab02b70d2bc32b87e279**，阶段结论另行文档提交；本轮不再推送 P2，也未修改 main。

## 来源、范围与契约调整

依据 output/reader-plan-v2 的总体规划 v2、接口与数据契约 v1、分阶段实施与验收执行表 v1；实际输入为当前 Code、P0 冻结 schema1 SQL/JSON 和 P1 的原生排版实验。沿用用户“你可以用linux，windows保留”的平台决定，无新的原始资料或真实书籍输入。raw、原规划正式文件、DailyPoetryA 参考及现有正式 v1 交付均未修改。

实现落点：Library/Models/Reader（DTO/持久化行）、Library/Services/Reader（契约/只读文本目录/状态/重置）、根 Services（协调器/迁移/v1适配与验证）、UI/Reader（P1 引擎迁移、正式布局服务和渲染控件）。真实 ServiceCollection→BuildServiceProvider→IServiceProvider、单例映射、资源定位器、三层导航和原主 VM 栈保留。

实施补充必须连同本验收阅读：[reader-p2-contract-addendum.md](../../../reader-p2-contract-addendum.md)。核心为：

- PageRef 以 LayoutId+章节+块起始偏移+块内页号定位，章内页号可为 null，避免任意锚点首屏必须从章首排版；不将页引用当持久化正文位置。
- 新增 DescribeAsync 获取正式章节元数据，租约 SQL 读写重载避免维护期间重入正常门禁。
- EbookProgress.CurrentPercent 是与正文位置同时保存的标量快照，不能从动态页码推算；后续会话写入时由正文服务计算。
- AiGenerationBatch.InsertedQuoteIdsJson 及不可变回执触发器，使删除 Quote/BatchItem 后仍保留原提交事实；P8 才实现完整批次写入/撤销。
- Format1 JSON 保持原字段；显式 v1→v2 适配生成新 Manual/BookUid、Revision=0。含电子书/定位数据的旧 JSON 导出/恢复先拒绝，P3 才实现完整 ZIP。

## 逐项验收

| 用例 | 结论 | 可复核证据 |
|---|---|---|
| P2-01 旧库迁移无损 | 通过。冻结 schema1 三本书、三条记录、两条书摘、三个计划的原字段/稀疏主键/日期/关联逐字段一致；user_version=2、integrity=ok、FK错误0；新UID重开不变。 | ReaderBaselineFixtureTests；process-accepted/probe.json；ui-accepted/desktop-smoke.json |
| P2-02 失败回滚、高版本 | 通过。在 identity/tables/version 三处抛出IO异常，新增列/表/索引、UID回填及版本全部回滚到原schema1，重试成功；schema99拒绝且旧字段不变。 | ReaderMigrationTests.MigrationFailureRollsBackEveryDdlAndFieldThenRetries（三条）；FutureSchemaRefusedWithoutMutation |
| P2-03 UID/Kind/Revision | 通过。旧Book均独立非空UID、Kind=Manual，旧Quote.Revision=0；真实唯一索引/类型与身份触发器；编辑及收藏递增当前版本。 | process-accepted/probe.json.books/quoteRevisions/schemaObjects；ReaderMigrationTests |
| P2-04 双种类规则 | 通过。Ebook允许TotalPages=0、Manual拒绝0；Ebook手动页码记录及物理页码书摘拒绝；Kind/BookUid不可转换，业务与SQL均验证。旧书籍编辑页保存保留UID/Kind。 | ReaderStateTests.EbookValidationAndImmutableIdentityProtectBothStorageAndSql、RealBookEditorPreservesExistingIdentityAcrossDetachedSave；SQL约束回归 |
| P2-05 统一状态和计划 | 通过。未开始/已开始但0%/到100%未标完成/完成后回看分别验证；列表、筛选、计划、统计一致；不显示0/0，电子书计划以百分比计算。重置检查历史多计划/身份/活动Reader，成功后保留会话活动且失效旧epoch。原始毫秒合计后换算分钟。 | ReaderStateTests；ReadingStateCalculator/Query；QueriesPlansAndResetUseSameCompletionStateAndKeepHistory；StatisticsSumMillisecondsBeforeRoundingAndDoNotInventEbookPages |
| P2-06 真实DI/分层与布局 | 通过。当前已实现服务从真实容器重复解析为同一实例，具体/接口布局与文档/协调器映射一致；Library程序集无Avalonia引用；正式契约的跨章覆盖、字符簇命中、冻结选区、近末尾首屏与旧布局释放通过。 | ReaderLayoutContractTests、ReaderDocumentContractTests及原ArchitectureTests |
| P2-07 进程/维护/epoch | 通过。第二真实进程初始化/写入被锁拒绝、零写入；正常释放及崩溃遗留锁文件后均可取得锁并提交SQL。维护排空旧操作、阻止新注册/操作；活动Reader立即拒绝维护；取消释放门禁；恢复后等待的旧写入拒绝；自动备份失败数据/epoch不变。 | process-accepted/probe.json；ReaderCoordinatorTests；ReaderBackupCoordinationTests |

迁移“中断”指应用层故障注入导致真实 SQLite ROLLBACK，不冒称已验证断电/文件系统损坏恢复；完整日志与成套文件恢复仍属于 P3。

## 构建、测试、干净源码与真实桌面

环境：Linux x64、.NET SDK 8.0.424 / runtime 8.0.30、Avalonia 11.0.10、SQLitePCLRaw 固定依赖。锁定还原与 Release 编译成功；现有依赖旧 Alpine RID 的 NETSDK1206 警告保留，当前 Linux x64 运行通过，不声明 Alpine 支持。

- 最终工作区：**124/124，失败0、跳过0**，p2-reviewed-workspace.trx / reviewed-workspace-test.log。
- 最终显式白名单源码：**145 文件**，临时核验ZIP 163435字节，SHA256 `66396288a2d47fe2e4559b5d215b582cf71b65f8ade7f1ee854b01a9d4267d3a`；全新目录锁定还原及 **124/124**，p2-reviewed-clean-source.trx / reviewed-clean-test.log。原96项保留并适配schema2断言/多实例测试隔离，新增28项。两次124不合并成248个用例。
- 临时源码包仅用于白名单核验，在/tmp，未作为新正式交付写入output。新源文件均已补入 Code/delivery-files.txt，归档无bin/obj/运行库/测试结果。
- 最终真实 X11/Openbox 桌面：空库首次/重开、冻结旧库迁移/重开，共**4次启动与正常Alt+F4关闭**，退出码均0；迁移前后旧字段哈希一致，重开完整字段哈希含UID一致；默认用户目录前后均不存在。ui-accepted 下截图/日志/JSON保留。
- 旧库重开截图已复核原概览数据（101页、75分钟、完成1本、在读20/120页）与未开放新入口；最终 schema1-2.png 与此前已复核的同场景截图逐字节相同。原生环境的DBus/session-manager缺失与llvmpipe降级日志保留，未见业务初始化或绑定失败。

中间失败记录未删除：build-01 是契约提取多包含方法片段的编译错误；build-02 是 Avalonia 调度API误用，均已修正。tests-01 原96项中1项因旧Headless测试的资源容器共享目录被单实例锁拒绝，修正测试目录隔离后通过。其余中间TRX仅代表当时实现，最终以 reviewed 两份为准。

## 重现

从仓库根目录（临时目标均须新目录）：

```sh
dotnet restore Code/ReadJournal.sln --locked-mode
dotnet test Code/ReadJournal.sln -c Release --no-restore
python3 Code/pack.py /tmp/p2-new-source.zip
# 解包到全新目录，在该目录锁定还原和运行同一测试。
dotnet restore docs/tools/ReaderP2Probe/ReaderP2Probe.csproj --locked-mode
dotnet run --project docs/tools/ReaderP2Probe/ReaderP2Probe.csproj -c Release --no-restore -- \
  Code/ReadJournal.UnitTest/Fixtures/Reader/schema1-sql.json \
  Code/ReadJournal.UnitTest/Fixtures/Reader/schema1.json /tmp/p2-new-process-evidence
```

真实桌面用 docs/tools/reader/smoke_desktop.py，沿用 P0 的 Xvfb/Openbox/字体环境，新增 `--expected-schema 2`（默认1保留P0复现）。本轮参数：dotnet=/tmp/readjournal-review-sdk/dotnet，xvfb/wm=/tmp/readjournal-p0-x11/root/usr/bin/{Xvfb,openbox}，app=Code/ReadJournal/bin/Release/net8.0/ReadJournal.dll，fixture=docs/evidence/reader/P0/fixtures/readjournal.sqlite3；工作目录/tmp/readjournal-p2-desktop-accepted，证据目录本节 ui-accepted。rootless库/配置路径设置沿用 P1/acceptance.md。

## 阶段边界与后续

P2 文档服务是最多两本纯文本的内存目录，只用于合成fixture；尚未实现受管文件加载、canonical manifest核验或EPUB图片/样式。UI引擎保留P1的最多8块几何缓存、全文/边界数组和块尾留白；额外首行缩进/段距非零时明确拒绝，不假称已实现。正式 ReaderSurface 尚未绑定Reader VM和输入生命周期，这在P5完成；P2只迁入统一布局/绘制实现与正式契约。

P3～P9 服务接口在P2声明，按各阶段实现后注册，不以空实现冒充可用。P3下一步完整ZIP与旧JSON整体替换、文件操作日志及崩溃恢复；通过前不开放电子书导入。Reset关闭会话/释放注册的上层编排、AI批次存储/撤销仍由P5/P8落实。P9真实API前仍须主动通知，当前零调用。

Code 与 docs 之外的 raw/output/参考及冻结fixtures无差异；工具生成的运行数据库目录被.gitignore排除。校验表见 code-files-sha256.json / tools-sha256.json；摘要见 environment-and-results.json。当前不新增Word/PDF/正式源码包。
