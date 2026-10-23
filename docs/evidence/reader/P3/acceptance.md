# P3 完整备份与恢复验收

日期：2026-10-22。结论：**按用户授权的 Linux 平台，P3-01～08 全部通过，可以进入 P4；本轮停在 P3。** Windows 保留后续实测，未宣称通过。未启用真实导入或正式 Reader，未调用 API。

用户“推送，开始p3”已执行：P2 正常推送至 origin/feature/reader-native，并用 git ls-remote 核验远端 **8696045c6361e1670ca55e05e816526b7d81c0f3**。P3 实现、工具与证据本地提交 **17690bbf52b84f09150882332500fa4d4127654a**；验收和进度另行文档提交，P3 本轮未推送，main 未改。

## 来源与范围

依据 output/reader-plan-v2 的总体规划 v2、接口与数据契约 v1、分阶段实施与验收执行表 v1，以及 docs/reader-p2-contract-addendum.md。沿用用户“你可以用linux，windows保留”的平台决定。实际输入为当前 Code、P0 冻结旧库/JSON 和新增合成受管文件；没有真实书籍、用户 Key 或新增原始资料。raw、原规划/正式交付、DailyPoetryA 参考与冻结 fixtures 未改。

实现说明见 [reader-p3-protocol.md](../../../reader-p3-protocol.md)：ZIP v2 显式清单与业务快照、canonical v1、安全路径/资源预算、完整自动备份、持久恢复日志与 LastRestoreId 提交点、同一共享数据库连接的启动恢复、DataEpoch 与原单例页面清理。只替换 books/managed，收件原件及本机偏好保留。原 ServiceCollection/定位器/单例/三层导航继续使用。

## 逐项验收

| 用例 | 结论与证据 |
|---|---|
| P3-01 完整往返 | 通过。原四表及电子书文件、进度、会话、书摘来源、AI批次和明细全部快照一致；BookUid/Kind/Revision、含已删除ID的不可变回执 `[26,999]` 保留。合成原书、canonical、两章UTF-16正文、PNG共5文件逐个SHA-256一致。CompleteBackupTests.CompleteZipRoundTripPreservesAllBusinessRowsFilesAndLocalPreferences；最终桌面核验 ui-verification-final.json。 |
| P3-02 旧JSON | 通过。JSON v1恢复生成Manual/新BookUid/Revision=0，整体替换当前电子书及会话；替换前完整自动ZIP可读，旧业务摘要一致且含原电子书文件。LegacyJsonReplacesEbooksOnlyAfterReadableCompleteAutomaticBackup；真实DI的 ActualSingletonRestoreClearsCachedViewsDocumentsLayoutsAndOldBookIdentity。 |
| P3-03 无效输入 | 通过。24类恶意/不一致输入，包括缺文件、文件/data摘要、重复ID/UID/JSON属性、孤立关联、绝对/越界/Windows路径、链接、清单外内容、非法定位/回执/canonical/块/目录/字符簇/换行/图片尺寸/百分比；另4类ZIP/JSON预算。重新计算外层摘要的语义错误仍拒绝。全部在自动备份/SQL/managed变更前拒绝，数据/文件/epoch不变。UntrustedPackageIsRejectedBeforeAutomaticBackupOrDataChanges、ArchiveBudgetsRejectBeforeExpansionOrMutation、SymlinkInManagedContentOrDestinationIsRejected。 |
| P3-04 故障原子性 | 通过。完整自动备份、日志、目录安装、SQL四类故障保留旧SQL和文件；原子导出安装失败保留已有可读目标。隔离后/安装后取消均回退旧集且epoch不变。PreCommitFailuresRestoreWholeOldDataset、AtomicExportFailurePreservesExistingReadableTargetAndNoTemporaryFiles、CancellationBeforeSqlCommitRollsBackFilesAndKeepsEpoch。 |
| P3-05 真中断 | 通过。父进程等待实际服务checkpoint信号后 Process.Kill 子进程，重新打开数据库：旧目录隔离、目标安装、事务内三点均完整旧集，提交后完整新集；各5文件摘要一致、integrity=ok。见 crash-probe-reviewed/probe.json。已提交收尾失败返回成功及警告；cleanup-only清理失败后允许普通编辑，下次启动仍能安全清理，有独立回归。 |
| P3-06 排除本机材料 | 通过。ZIP只有manifest/data/显式受管文件，不含secure、日志、staging、布局缓存或本机主题。测试仅用虚构占位密钥文件，恢复后其内容、主题、收件原件及缓存均保留；没有读取真实Key。完整往返测试与 ui-verification-final.json.zipEntries。 |
| P3-07 上下文失效 | 通过。真实容器解析的文档/布局缓存释放，原单例详情、编辑草稿、列表、概览、书摘与返回栈清理；同整数ID的新BookUid不接收旧响应。旧列表延迟响应不回填，旧epoch写入被拦截。BackupUiIntegrationTests、ReaderBackupCoordinationTests及原代际测试；当前设置页保留并正常报告恢复成功。 |
| P3-08 信息不足停止 | 通过。实际安装后杀进程，再截断日志：初始化拒绝、IsInitialized=false、RecoveryRequired诊断提示保留恢复材料；当前5文件与trash中5文件均保留，所有恢复文件及数据库业务行未变。crash-probe-reviewed/probe.json.insufficientJournal；RecoveryFailureKeepsInitializationPageAndPreservesEvidence确认不导航主业务。 |

## 最终构建、独立源码与桌面

- 工作区最终 `p3-percent-validated.trx`：**169/169，失败0、跳过0**；P2原124项适配ZIP并保留，新增45项。build-final.log Release成功，增量构建0警告/0错误。
- 显式白名单159文件临时包，199883字节，SHA-256 `3dd1b1c732836b92a738d6f3be4b5d2a2d771f24e380485bb9b20993529e05a1`。独立全新解包、锁定依赖还原与Release测试 **169/169**，见 reviewed-source-package.json、reviewed-clean-restore.log、reviewed-clean-test.log、p3-reviewed-clean-source.trx；全部包内文件逐个与最终源码摘要一致。仅用于验证，未新增正式源码交付。
- 还原使用现有NuGet缓存；不宣称首次还原离线可用。独立首次构建出现固定SQLite包的旧Alpine RID提示 NETSDK1206，未忽略失败或宣称Alpine通过。
- 最终桌面在独立 READJOURNAL_DATA_DIR 和真实Xvfb/Openbox窗口操作原生GTK ZIP选择器：导出→选择目标ZIP→整体替换确认→恢复成功→正常关闭重开→书库A。见 ui-accepted/03～12截图及actions.json。确认页明确电子书/会话整体替换、旧JSON语义、完整自动备份和本机主题/Key保留。
- 使用独立解包构建的应用再次启动同一隔离书库，真实概览和书库显示A。见 ui-clean-accepted/01-clean-start.png、02-clean-library-stable.png。两组最终验收共3次正常关闭、exit=0，默认数据目录原本不存在且未被创建。最终应用日志绑定诊断0；缺少会话DBus/Session Manager及llvmpipe OpenGL回退提示保留，实际软件渲染窗口已目视检查。
- 桌面完成后独立读取业务和文件：恢复摘要 `40db29e3d0899bd141a2075859e6dfdb00186991348446d9715e6c571a0925c4` 等于输入A；完整自动备份等于原B；桌面导出的业务data与5文件清单又与自动备份B一致。恢复后2书、1电子会话、原批次回执及5文件完整，integrity=ok。见 ui-verification-final.json。

复现入口：按 Code/README.md 还原/测试；`dotnet run --project docs/tools/ReaderP3Probe -c Release -- <全新证据目录>`执行四个真实中断及损坏日志保留检查。UI工具参数见 docs/tools/reader/p3_desktop_driver.py；只能使用独立合成数据目录。

## 失败历史与修正

中间证据全部保留，最终以带percent-validated/reviewed及ui-accepted/ui-clean-accepted的上述记录为准：

1. 初期124项13失败、154项14失败：旧测试仍按JSON路径/故障注入外观断言；另发现旧DateTime.Kind后缀与SQLite ticks语义导致恢复后摘要不一致。更新完整ZIP测试及实际文件层注入，旧日期规范化为无Kind ticks后154/154。
2. 实际单例缓存清理新增测试157项中1失败：ReadingRecordEditViewModel.SetParameter(null)访问空记录。改为显式空记录重置，157/157；不是把警告当成功绕过，最终恢复断言LastWarning为空。
3. 深度回归167项中1失败：测试图片50001×1未超过25MP，却预期超限；修正为50000001×1，另补媒体头/清单尺寸一致性后168/168。
4. 补充正文位置与可计量百分比一致性，修正合成会话为精确比例，新增恶意百分比回归，最终169/169。此前168项源码、crash-probe-final和ui目录属于中间版本；用修正后的全新合成数据、源码包及crash-probe-reviewed/ui-accepted重做最终验收。
5. ui目录中早期GTK焦点/输入时序未命中的截图、ui-final非交互stdin提前关闭、ui-clean-source中立即导航后的黑屏抓图均保留为工具诊断；后者初始概览正常，分开命令等待后在ui-clean-accepted重新目视核对书库成功，没有据此修改生产代码。旧窗口树文本仅规范末尾空行以通过diff检查。

## 阶段边界

本阶段验证应用进程中断，未进行断电、磁盘损坏、限额顶峰压力或Windows文件系统实测。图片只核对受限媒体头/尺寸/哈希，完整解码与排版仍属P7；TXT导入/扫描与恢复分派扩展属P4，会话采集属P5，AI批次写入/撤销属P8。canonical文件契约已明确，可供后续阶段使用，不等于真实导入已经完成。

P3没有未关闭的验收失败。下一阶段为P4，须继续逐阶段验收；P9真实API前主动通知。汇总见environment-and-results.json，源码及工具摘要见code-files-sha256.json、tools-sha256.json。
