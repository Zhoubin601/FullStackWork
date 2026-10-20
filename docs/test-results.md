# 实际自动化测试结果

## 最新架构与功能贯通复核（2026-10-20）

最终 Release **83通过、0失败、0跳过**。原有68项加本轮15项；源码v3独立解包、locked-mode还原、Release构建和同一83项全部通过。解包还原使用本轮已填充的共享NuGet缓存，未复制bin/obj，没有另建空NuGet缓存。

本轮实际来源：`docs/evidence/architecture-flow-20261020/final.trx`、`clean.trx`。独立运行时架构探针另有71项全部通过，覆盖完整容器和视图/导航映射。修复前失败证据单独保留，汇总见`docs/architecture-flow-review-20261020.md`。

以下是本轮15项新增永久回归的实际结果；后续旧68项表保留历史日志和耗时，不冒称为本轮耗时。

| 实际新增用例 | 结果 | 耗时 |
|---|---|---|
| ReadJournal.UnitTest.BusyLoadRegressionTests.ReturningToBusyListWaitsAndRefreshesLatestQueryEvenAfterFailure(oldOperationFails: False) | Passed | 00:00:00.1957399 |
| ReadJournal.UnitTest.BusyLoadRegressionTests.ReturningToBusyListWaitsAndRefreshesLatestQueryEvenAfterFailure(oldOperationFails: True) | Passed | 00:00:00.0424900 |
| ReadJournal.UnitTest.DeleteNavigationRaceTests.CommittedDeletionCannotPopReopenedDetailUsingSameSingleton | Passed | 00:00:00.0119316 |
| ReadJournal.UnitTest.DeleteNavigationRaceTests.CompletionOfOldDeletionCannotDiscardDraftOnNewPage | Passed | 00:00:00.0185828 |
| ReadJournal.UnitTest.DeleteNavigationRaceTests.ConfirmedDeletionOnCurrentDetailStillReturnsToLibrary | Passed | 00:00:00.0038885 |
| ReadJournal.UnitTest.DeleteNavigationRaceTests.PendingConfirmationCannotDeleteAfterDetailParameterChanges | Passed | 00:00:00.0108730 |
| ReadJournal.UnitTest.DeleteNavigationRaceTests.PendingImpactReadCannotDeleteBookOpenedByLaterNavigation | Passed | 00:00:00.0057179 |
| ReadJournal.UnitTest.DeleteNavigationRaceTests.ReopenedDetailLoadsNewBookAfterOlderOperationCompletes | Passed | 00:00:00.0344447 |
| ReadJournal.UnitTest.OverviewArchitectureTests.OverviewLoadsThroughBusinessInterfacesWithoutDatabaseDependency | Passed | 00:00:00.0129821 |
| ReadJournal.UnitTest.OverviewArchitectureTests.RecentSummariesJoinBooksSortDatesAndIdsAndApplyLimitInActualSqlite | Passed | 00:00:00.0208462 |
| ReadJournal.UnitTest.OverviewArchitectureTests.RecentSummariesReturnEmptyForActualEmptySqlite | Passed | 00:00:00.0078511 |
| ReadJournal.UnitTest.SettingsNavigationTests.FailedRestoreReleasesNavigationAndCloseProtection | Passed | 00:00:00.0221720 |
| ReadJournal.UnitTest.SettingsNavigationTests.PendingRestorePreventsMenuNavigation | Passed | 00:00:00.0076742 |
| ReadJournal.UnitTest.SettingsNavigationTests.PendingRestorePreventsWindowClose | Passed | 00:00:00.0075971 |
| ReadJournal.UnitTest.SettingsNavigationTests.SuccessfulRestoreReleasesNavigationAndCloseProtection | Passed | 00:00:00.0042293 |

## 前轮68项历史结果

2026-10-20 修复后复验：Debian 13 x64，.NET SDK 8.0.424，Release。**68通过、0失败、0跳过**；包括原有40项、编辑回归20项、计划一致性回归8项。新版ZIP独立解包后使用全新NuGet目录还原、构建并运行同一68项，全部通过。

来源：`docs/evidence/fix-20261020/fix-release.trx` 和 `fix-clean.trx`。此前审计的5项历史失败复现保留在audit目录；已将正确行为纳入永久回归，后两项改为验证重复关联被提前拒绝、数据保持完整及备份往返。

| 实际用例 | 结果 | 耗时 |
|---|---|---|
| ReadJournal.UnitTest.ArchitectureTests.ActualContainerResolvesAllRegisteredSingletonsAndInjectsConstructors | Passed | 00:00:00.0042006 |
| ReadJournal.UnitTest.ArchitectureTests.InitializationFailureStaysAndPublicRetrySucceeds | Passed | 00:00:00.3818069 |
| ReadJournal.UnitTest.ArchitectureTests.ReflectionViewLocatorCachesAndSetsDataContext | Passed | 00:00:00.1025177 |
| ReadJournal.UnitTest.ArchitectureTests.SingletonDetailResetsOldBookBeforeLoadingAnother | Passed | 00:00:00.1250014 |
| ReadJournal.UnitTest.ArchitectureTests.ThreeLayersParametersRepeatedTargetAndBackStack | Passed | 00:00:00.0186840 |
| ReadJournal.UnitTest.DatabaseTests.ActualSqliteTransactionRollsBackOnConstraintFailure | Passed | 00:00:00.0656746 |
| ReadJournal.UnitTest.DatabaseTests.FailedDirectoryCanRetryAfterCauseRemoved | Passed | 00:00:00.0109982 |
| ReadJournal.UnitTest.DatabaseTests.InitializesVersionAndPersistsAcrossReopen | Passed | 00:00:00.0546888 |
| ReadJournal.UnitTest.EditingRegressionTests.BookLoadMergesUntouchedFieldsWithoutOverwritingTypedDraft(typeBeforeLoaded: False) | Passed | 00:00:00.0044979 |
| ReadJournal.UnitTest.EditingRegressionTests.BookLoadMergesUntouchedFieldsWithoutOverwritingTypedDraft(typeBeforeLoaded: True) | Passed | 00:00:00.0018804 |
| ReadJournal.UnitTest.EditingRegressionTests.FailedInitialReadBlocksSaveAndRetryHydratesUntouchedFields(kind: "book") | Passed | 00:00:00.0047806 |
| ReadJournal.UnitTest.EditingRegressionTests.FailedInitialReadBlocksSaveAndRetryHydratesUntouchedFields(kind: "plan") | Passed | 00:00:00.0018314 |
| ReadJournal.UnitTest.EditingRegressionTests.FailedInitialReadBlocksSaveAndRetryHydratesUntouchedFields(kind: "quote") | Passed | 00:00:00.0012811 |
| ReadJournal.UnitTest.EditingRegressionTests.PlanLoadPreservesChangedDateAndLoadsBookProgressForSuggestions(typeBeforeLoaded: False) | Passed | 00:00:00.0043891 |
| ReadJournal.UnitTest.EditingRegressionTests.PlanLoadPreservesChangedDateAndLoadsBookProgressForSuggestions(typeBeforeLoaded: True) | Passed | 00:00:00.0355755 |
| ReadJournal.UnitTest.EditingRegressionTests.QuoteLoadPreservesTypedTextAndSelectionWhileStillLoadingChoices(chooseAnotherBook: False, typeBeforeLoaded: False) | Passed | 00:00:00.0101283 |
| ReadJournal.UnitTest.EditingRegressionTests.QuoteLoadPreservesTypedTextAndSelectionWhileStillLoadingChoices(chooseAnotherBook: False, typeBeforeLoaded: True) | Passed | 00:00:00.0020311 |
| ReadJournal.UnitTest.EditingRegressionTests.QuoteLoadPreservesTypedTextAndSelectionWhileStillLoadingChoices(chooseAnotherBook: True, typeBeforeLoaded: False) | Passed | 00:00:00.0033393 |
| ReadJournal.UnitTest.EditingRegressionTests.QuoteLoadPreservesTypedTextAndSelectionWhileStillLoadingChoices(chooseAnotherBook: True, typeBeforeLoaded: True) | Passed | 00:00:00.0024611 |
| ReadJournal.UnitTest.EditingRegressionTests.RetryingQuoteChoiceLoadPreservesAnAlreadyDirtyDraft | Passed | 00:00:00.0022617 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveAndLeaveRefusesToDiscardInputChangedDuringStorageAwait(kind: "book") | Passed | 00:00:00.0029370 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveAndLeaveRefusesToDiscardInputChangedDuringStorageAwait(kind: "plan") | Passed | 00:00:00.0073158 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveAndLeaveRefusesToDiscardInputChangedDuringStorageAwait(kind: "quote") | Passed | 00:00:00.0034276 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveAndLeaveRefusesToDiscardInputChangedDuringStorageAwait(kind: "record") | Passed | 00:00:00.0137693 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveKeepsNewerDraftOnPageAndNextSaveUpdatesTheCreatedEntity(kind: "book") | Passed | 00:00:00.0014023 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveKeepsNewerDraftOnPageAndNextSaveUpdatesTheCreatedEntity(kind: "plan") | Passed | 00:00:00.0017719 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveKeepsNewerDraftOnPageAndNextSaveUpdatesTheCreatedEntity(kind: "quote") | Passed | 00:00:00.0049303 |
| ReadJournal.UnitTest.EditingRegressionTests.SaveKeepsNewerDraftOnPageAndNextSaveUpdatesTheCreatedEntity(kind: "record") | Passed | 00:00:00.0019943 |
| ReadJournal.UnitTest.EditingTests.CancelDiscardAndFailedSavePreserveDraftAndNavigation | Passed | 00:00:00.1842485 |
| ReadJournal.UnitTest.EditingTests.SaveThenLeavePersistsAndMarksClean | Passed | 00:00:00.0105951 |
| ReadJournal.UnitTest.ExtendedBusinessTests.AutoBackupFailurePreventsReplacementAndSqliteFailureRollsBack | Passed | 00:00:00.0109345 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "duplicate") | Passed | 00:00:00.0201906 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "future") | Passed | 00:00:00.0185141 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "json") | Passed | 00:00:00.0155592 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "missing") | Passed | 00:00:00.0188330 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "null") | Passed | 00:00:00.0404529 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "orphan") | Passed | 00:00:00.0289411 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "page") | Passed | 00:00:00.0157138 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "plans") | Passed | 00:00:00.0302347 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "version") | Passed | 00:00:00.0094363 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 0, total: 100, days: 0, state: "进行中", suggestion: 100) | Passed | 00:00:00.0000136 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 100, total: 100, days: -1, state: "已完成", suggestion: 0) | Passed | 00:00:00.0000268 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 20, total: 100, days: -1, state: "已逾期", suggestion: 0) | Passed | 00:00:00.0003716 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 20, total: 100, days: 4, state: "进行中", suggestion: 16) | Passed | 00:00:00.0004409 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanUniquenessCompletionAndDeletionDoNotAffectRecords | Passed | 00:00:00.0150321 |
| ReadJournal.UnitTest.ExtendedBusinessTests.StatisticsDemoAndBackupRoundTripPreserveIdsAndPreferences | Passed | 00:00:00.0357653 |
| ReadJournal.UnitTest.NavigationRaceTests.BackAndMenuCannotOpenTwoLeaveConfirmations | Passed | 00:00:00.0423462 |
| ReadJournal.UnitTest.NavigationRaceTests.OldSingletonLoadCannotPublishAfterNewParameter | Passed | 00:00:00.0415013 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.BackupExportedAfterLegalUiOperationsMustPassOwnValidation | Passed | 00:00:00.0120330 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.ConcurrentPlanSavesLeaveOnePlanAndRestorableBackup | Passed | 00:00:00.0105294 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.LegacyCompletedPlansAllowSafeChangesAndRetainHistoryThroughBackup | Passed | 00:00:00.0141272 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.LegacyCompletedPlansRejectInvalidatingChangesWithoutPartialWrites(operation: "delete-record") | Passed | 00:00:00.0360793 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.LegacyCompletedPlansRejectInvalidatingChangesWithoutPartialWrites(operation: "edit-record") | Passed | 00:00:00.0128161 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.LegacyCompletedPlansRejectInvalidatingChangesWithoutPartialWrites(operation: "increase-pages") | Passed | 00:00:00.1017385 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.LegacyCompletedPlansRejectInvalidatingChangesWithoutPartialWrites(operation: "move-record") | Passed | 00:00:00.0290651 |
| ReadJournal.UnitTest.PlanConsistencyRegressionTests.RemovingCompletionRecordMustNotProduceTwoUnfinishedPlans | Passed | 00:00:00.0482004 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 1, end: 1, pages: 1) | Passed | 00:00:00.0024700 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 1, end: 100, pages: 100) | Passed | 00:00:00.0031374 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 10, end: 20, pages: 11) | Passed | 00:00:00.0042259 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 100, end: 100, pages: 1) | Passed | 00:00:00.0023882 |
| ReadJournal.UnitTest.ReadingBusinessTests.EditDeleteAndTotalPagesRecomputeCompletionAndProgress | Passed | 00:00:00.0087074 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 0, end: 10, minutes: 1, futureDays: 0) | Passed | 00:00:00.0020245 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 10, minutes: -1, futureDays: 0) | Passed | 00:00:00.0025785 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 10, minutes: 0, futureDays: 0) | Passed | 00:00:00.0021373 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 10, minutes: 1, futureDays: 1) | Passed | 00:00:00.0039381 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 101, minutes: 1, futureDays: 0) | Passed | 00:00:00.0020829 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 20, end: 10, minutes: 1, futureDays: 0) | Passed | 00:00:00.0019640 |
| ReadJournal.UnitTest.ReadingBusinessTests.SearchPaginationQuotesFavoritesAndCascadeDelete | Passed | 00:00:00.0089623 |
