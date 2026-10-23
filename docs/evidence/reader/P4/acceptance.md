# P4 受管TXT导入、扫描与删除验收

2026-10-22。结论：**P4-01～08按用户授权的Linux平台全部通过。** P3已正常推送并回读远端 `16e41558e8ceb4549274dd26cc6b0321e058350d`；P4实现/工具/证据提交 `5358a4a`，阶段记录另行提交后推送feature/reader-native。用户已追加授权继续P5、P6、P7，每阶段验收后独立推送。Windows保留未测，P4没有真实API调用。

## 来源与实现

依据output/reader-plan-v2三份规划、P2正式契约、P3完整备份协议和冻结旧库资料。输入为当前Code及确定性合成TXT/编码/故障样本；没有使用真实书籍或Key，raw、正式output、daily-poetry-a和冻结fixtures未改。补充约定见 [reader-p4-protocol.md](../../../reader-p4-protocol.md)，实施计划见 [reader-p4-plan.md](../../../reader-p4-plan.md)。

新增TxtBookParser、EbookLibraryService、BookFileOperations、BookDeletionService及必要DTO；共享连接启动恢复分派、原BookStorage删除入口和书库UI接入。保留原ServiceCollection→IServiceProvider→资源定位器、单例映射、三层导航和原Main栈；Library无Avalonia引用。未开启P5 Reader路由，书库“打开”仍进入原详情，电子书隐藏手动页码记录按钮。

## 逐项验收

| 用例 | 结果与证据 |
|---|---|
| P4-01 编码/换行 | 通过。UTF-8无BOM/含BOM与显式GB18030逐字保留，混合CRLF/CR/LF规范化；非法UTF-8、GB18030、UTF-16、二进制控制字符不静默替换。StrictEncodingAndMixedNewlinesPreserveSourceAndInitialProgress、InvalidInputNeverCreatesVisibleBookOrChangesSource；ui-final中03/05/06～08截图。 |
| P4-02 超长/稳定 | 通过。110万UTF-16单元无标题段落分块/续章，文本拼回完全一致；每块≤8192、每章≤262144，字符簇不切断。两个随机FileId书库同源/同标题canonical摘要一致，按章读取与末尾100%正确；字符簇/总量/块预算明确拒绝。LongUnheadedTextHasStableCanonicalAndNoLostOrSplitGraphemes、ParserBudgetsRejectBeforeInstallingFiles。 |
| P4-03 来源/去重 | 通过。外部与收件原件摘要不变；同字节返回Duplicate、同名不同字节各一本，来源变化暂缓且可重试。DuplicateBytesAndSameNameDifferentContentsKeepCorrectBooksAndSources、SourceChangingDuringCopyIsDeferredAndCanBeRetriedWithoutAlteringBytes；ui-verification-final.json记录5份来源摘要。 |
| P4-04 并发唯一性 | 通过。两项导入都到达文件安装完成/SQL前屏障再同时放行，真实SourceSha256唯一约束令一项Imported、一项Duplicate；只剩1个Book/File/受管目录且无日志或孤儿。SimultaneousIdenticalImportsUseSqlUniqueConstraintAndCleanLosingDirectory；检查真实EbookFile建表SQL唯一约束。 |
| P4-05 中断恢复 | 通过。复制、解析、安装、事务前/中/后6点实际Process.Kill；前5点重启完整旧集，提交后完整新书/初始进度/文件。另4个删除中断点验证提交事实；均integrity=ok、来源未变。crash-probe-reviewed/probe.json。7类提交前IO失败、提交前/中/后取消、cleanup后编辑再重启均有回归；不出现可见但无文件的Ready书。 |
| P4-06 扫描 | 通过。只扫描books本层，不递归managed/子目录或临时文件；观察变化文件暂缓，损坏/链接/断链不影响合法文件，反复扫描不加行。真实DI初始化在导航前完成扫描。补充本机InboxSeen记录：已删除收件书在刷新/重启后不重现，显式选择仍可重新导入；旧epoch扫描不能写入整体恢复后的数据集。RootOnlyScanSkipsChangingFilesAndOneBadFileDoesNotBlockOthers、SymbolicSourceAndBrokenInboxLinkAreRejectedWithoutBlockingValidInboxBook、P4RealContainerScansBeforeNavigationAndSharesFileAndParserServices、DeletedInboxBookStaysDeletedAfterScanAndRestartButExplicitImportStillWorks、InboxScanStartedBeforeRestoreCannotImportIntoReplacementDataset。 |
| P4-07 统一删除 | 通过。IBookStorage与IEbookLibraryService共用内部删除实现；确认含手动记录/会话/书摘/计划和实际文件字节。SQL级联清掉文件、进度、会话、来源、批次/明细，来源保留；事务前/中失败保持旧集，提交后移trash/清理失败保留日志警告，下次启动重试。ActualImportedTxtBackupDeleteRestorePreservesAllRowsFilesAndSource、DeleteFailuresKeepCommitFactAndRetriableCleanup、ActiveReaderOrWrongBookIdentityCannotDelete；ui-final/16-delete-confirmation.png。 |
| P4-08 实际往返/Missing | 通过。实际导入TXT分别以初始进度和完整关联数据备份→删除→恢复，全部业务摘要与文件清单一致；真实桌面3书/11文件初始未开始0%恢复一致。源/清单/章节缺失均标记Missing且拒绝读取/完整导出；缓存文档也不能绕过。Missing条目可显式删除后恢复最后完整备份；原件保留。crash-probe-reviewed/probe.json.roundtrip、MissingFilesAreMarkedAndCannotOpenOrExport、CachedManagedDocumentCannotReopenAfterChapterDisappears、MissingEntryCanBeExplicitlyRemovedThenRecoveredFromLastCompleteBackup、ui-missing/02-missing-library.png。 |

## 最终验证与产物

- Release工作区 `p4-inbox-tombstone-reviewed.trx`、白名单独立解包 `p4-final-clean-source.trx`：**各217/217，失败0、跳过0**，保留P3原169项并新增48项。build-final.log增量构建0警告/0错误；干净首次构建的固定SQLite旧Alpine RID提示NETSDK1206保留，不宣称Alpine验证通过。
- 显式169文件临时验证包229536字节，SHA-256 `097b575eb14550580a54e0644d3afa3cec883ffcd8e774c2ff295bc98f674fbd`；独立锁定还原/Release测试通过，包内每文件与最终源码SHA一致。final-source-package.json、final-clean-restore.log、final-clean-tests.log、code-files-sha256.json；没有新增正式源码交付或报告。
- 10处真实进程中断及损坏导入日志验证：后者阻止主业务、保留4个已安装未提交文件及全部数据库/恢复文件字节。实际TXT初始ZIP与关联ZIP是本阶段新增合成fixtures，见crash-probe-reviewed。源码工具在docs/tools/ReaderP4Probe，可用全新证据目录复现。
- 最终ui-final用真实Xvfb/Openbox、原生GTK选择器操作UTF8/重复/无效输入/显式GB18030、扫描、ZIP导出、删除确认、恢复确认及正常关闭重开。16确认说明1916字节受管文件及来源保留，17显示删除后2书，24恢复成功，26重开3书。电子详情没有手动页码入口。未以业务探针或伪UI替代这些动作。
- 正常关闭应用后，仅移走私有受管副本并保留原始来源/精确副本，ui-missing真实启动显示Missing；再正常关闭并补回原字节。独立源码构建应用ui-clean-source重新启动成功。3组最终验收共**4次正常关闭，exit=0**，默认目录原本不存在且未创建；最终日志绑定诊断0。
- ui-verification-final.json：完整业务摘要 `b990ce5ad0110bc1e78a0f762a60b9b3326bd6eab1e383890ed890823d3ae757` 与桌面导出ZIP一致，3书/11受管文件、未开始0%初始进度完整；自动备份包含删除后2书，5份原件摘要未变，integrity=ok。补回Missing前后最终业务摘要一致。

首次重建环境的依赖还原通过配置代理获取NuGet包；后续独立还原复用包缓存，不宣称首次还原离线。临时SDK/桌面数据在用户续接时曾丢失，最终证据已于重建环境重做。系统会话DBus/SessionManager缺失、llvmpipe回退和GTK关闭时的WIDGET_REALIZED_FOR_EVENT诊断保留；最终操作和正常关闭均实际通过。中间重绘抓图保留，结论使用稳定截图及数据核对，未编辑截图内容。

## 失败历史与边界

早期169项回归全过；新增导入测试202项有15失败，诊断定位为初始DocumentKey/Position错误地直接序列化，改回P2版本化写入器后202/202。211项中1失败暴露删除文件移动IO失败错误地封锁业务，改为仅对已确认无引用文件保留待清理日志/警告，212/212。随后补充来源变化/缓存Missing为214项，增加删除收件防重现、Missing显式删除后恢复及旧扫描epoch拒绝，最终217项全部通过；更早带final字样的214项记录仅为中间版本。

ui-01因执行环境重建中止；ui-restarted的Escape没有关闭显式确认，主窗关闭被忙碌门禁拒绝，工具15秒等待超时并终止自己的测试进程，该轮没有正常关闭回执；最终ui-final重新执行并点击明确确认按钮完成。中间截图包含部分重绘帧，测试驱动增加真实X11曝光请求辅助抓图。首次.NET欢迎横幅日志仅将CRLF规范为LF以通过差异检查，结果文本未改。

本阶段不等于断电、磁盘损坏、资源上限峰值或Windows实测。缺文件库无法生成完整自动备份，因此整体恢复先要求补回文件或显式删除损坏条目，不能静默跳过文件。InboxSeen是本机收件观察上下文，不随业务备份替换。受管正文最多两文档/每文档两章缓存，P3完整验证仍可能临时读取受限全文；没有宣称已达到P5首屏指标。EPUB、图像解码与图片排版留到P7，真实API仍到P9前主动通知。

当前没有未关闭的P4验收失败。P4推送并回读后进入已授权的P5；P5、P6、P7继续各阶段独立验收和推送，不能跳过前置判据。
