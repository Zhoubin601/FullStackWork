# P8 AI 本地流程与 Mock 验收

2026-10-23，Linux x64 / .NET SDK8.0.424。基准origin/main=cd3386b；实现分支feature/reader-ai-p8，用户明确要求完成后推送。依据reader-plan-v2/P8-01～10，协议见docs/reader-p8-protocol.md。

最终工作区Release **327/327通过，0失败、0跳过**（原272+新增55）；200文件白名单独立打包、解包、锁定还原、Release构建和测试同样327/327。final-release.trx / clean-release.trx与对应日志为本轮实测，summary.json记录计数，源码包319070字节，摘要在source-package.json，全部源文件摘要见source-files-sha256.json。临时验证包仅在/workspace/.readjournal-env，未作为正式output交付。

| 验收 | 结果及可复核证据 |
|---|---|
| P8-01 冻结范围 | AiWorkflowTests.SelectionRequestContainsOnlyFrozenTextAndAnalysisNeverWritesQuotes核对实际序列化segments只含所选原句，无范围外结尾或BookUid；CurrentPageAndGlobalPagesFreezeOrderedMappingAndWaitForIndexWithoutSending验证页映射；AiViewModelTests.ReflowKeepsSubmittedSnapshotWhileLocalReadingRemainsAvailable验证提交后翻页/字号28重排仍使用原请求，未提交页号清空。真实窗口24～25为全书1～2页、379字符的范围确认及Mock分析。 |
| P8-02 等待与取消 | 未就绪索引请求数0，取消/重排后零写入；PendingPageIndexCanBeCancelledByReflowAndSendsNothing。非配合Provider取消后仍回成功、事务内取消均零新增/整批回滚，见ConcurrentSecondRequestIsRejectedAndCancellationAfterResponseDoesNotSave及CancellationInsideTransactionRollsBackReceiptOriginAndAllQuotes。 |
| P8-03 分析/自动入库 | 分析/解释走只读结果；GenerationSavesOnlyAcceptedQuotesAndCountsRejectedAndDuplicates验证Quote/Origin的真实身份、Page=null、AI来源及原文回跳。真实窗口05～09：分析后0书摘/0批次，生成后1Quote/Origin/Receipt/Item，见desktop-verification.json。 |
| P8-04 原句与响应 | 伪造/范围外/重复无法消歧/字符簇截断拒绝，合法紧邻上下文消歧通过；真实导入large-image EPUB，剔除图片为多个span，跨图片拼接拒绝、图片后原句可存可回跳。AiHttpTests验证无效JSON、错误类型/模型坐标字段/重复字段、超长、截断、过多候选均拒绝。 |
| P8-05 原子与重试 | 部分合法只保存合法项，并统计拒绝/重复及原因；3处事务故障回滚全部表，重试本地保存不增加Provider计数。LostCommitAcknowledgementRetriesSameReceiptWithoutAnotherHttpCall覆盖已提交但回执丢失，再试读同BatchId原回执。 |
| P8-06 撤销与幂等 | 回执在删除书摘/级联明细后仍保持原ID；撤销后重放不重建。Revision、内容摘要、收藏任一变化保留，原已存在项保留，重复撤销幂等；2处撤销故障回滚。真实窗口11～12收藏项删除0保留1，20～21未改项删除1，原收藏保留且两份回执Status=1。 |
| P8-07 分类错误/阅读 | HttpFailuresAreClassifiedAndNeverEchoBodyOrAutomaticallyRetry覆盖401/403/429/500/503/302/400；OfflineDisconnectTimeoutAndCancelAreDistinctAndNeverRetry覆盖离线/断流/超时/取消，计数始终1，无错误正文/Key回显。VM网络等待期间翻页/重排有效、IsBusy=false，离开不等待非配合网络。 |
| P8-08 过期隔离 | 真会话关闭、完整ZIP恢复、重置代际、删除后复用同整数ID，旧成功响应均不写新数据；真实VM关闭并复用单例后旧结果不改新面板，见OldResponseCannotCommitAfterLeavingRestoringResettingOrReusingIntegerId和LeavingAndReusingSingletonDoesNotWaitForNetworkOrLetOldResultChangeNewPanel。 |
| P8-09 凭据 | SessionSecretStore仅会话内存；实际XAML输入框PasswordChar=●，保存/非法输入/离开均清空，可删除；测试Key运行时随机生成。完整ZIP逐条解压内容及数据目录检查无测试Key明文，恢复保留当前会话Key，新Store为空；错误/请求体不回显Key。BackupRoundTripPreservesReceiptAndUndoAndNeverContainsSessionCredentials、SettingsMaskAndClearInputDeleteCredentialsAndPersistOnlyNonSecrets、RealAdapterPostsOnlyMessagesWithBearerAndRejectsMissingConsentOrCredentialBeforeHttp。 |
| P8-10 限额/并发/费用 | 输入超额/第二并发在HTTP前拒绝，响应逐块262144字节上限，最多32候选，页范围≤200；超时不自动重试。提交确认显示范围/字符数/模型/输入上限及自动入库；真实模式明确可能计费、无可靠费用数字，不作伪估算。 |

## 真实桌面与边界

实际Xvfb/Openbox桌面使用隔离数据目录，全部正文为合成TXT；driver的真实输入/窗口截屏/正常关闭记录见desktop/actions.json，SQLite仅以mode=ro核对，无直接写库代替业务操作。真实分析、自动保存、AI来源、收藏变更保留、未改批次删除、原文高亮及页范围分析均有截图；更完整快照见desktop-verification.json。设置与会话密钥控件见desktop/26～27，重启后收藏AI书摘仍在见desktop/29；两次正常关闭exit0。设置保存补验见settings-final/03及其actions.json，实际ai-settings.json确认UseMock=true且未确认真实上传。

截图10是收藏异步处理中立即再点撤销被忙态拦截，没有冒充确认成功；截图14抓到尚在排版的一帧，15显示稳定高亮。保留过程截图，最终结论使用11/12/15/20/21等稳定结果。应用DBus/SESSION_MANAGER缺失及llvmpipe回退提示原样保留，没有业务绑定错误或崩溃，不据软件渲染推断其他平台性能。

首轮新增回归2项失败：测试断言漏计换行、保密扫描误读独占library.lock；修正为准确范围和排除锁文件后通过，原失败TRX/log保留。初始编译一处params数组的target-typed new写法已改为显式候选类型；未掩盖失败。独立复核同时将撤销内容摘要的时间字段改用Ticks，避免SQLite的Kind变化误判。最终55项新增及327项完整回归均在修正后的同一源码运行。

本轮真实内容API调用 **0**；Mock经过同一序列化/解析，不代表DeepSeek当前能力、模型质量、费率或真实联调成功。P8新增Windows实机尚未执行，P7的Windows150%结果不能代替。无真实商业EPUB样本。P9/P10未实施，正式Word/PDF/源码交付ZIP未更新；raw/output/参考/冻结fixtures共107文件逐字节与基准一致，见protected-files-sha256.json。
