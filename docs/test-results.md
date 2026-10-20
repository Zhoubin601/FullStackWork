# 实际自动化测试结果

环境：Debian 13 x64，.NET SDK 8.0.424，Release；来源：release.trx。40 例全部通过，另在最终归档解压目录重跑同一测试集，40 例通过。

| 实际用例 | 结果 | 耗时 |
|---|---|---|
| ReadJournal.UnitTest.ArchitectureTests.ActualContainerResolvesAllRegisteredSingletonsAndInjectsConstructors | Passed | 00:00:00.0014244 |
| ReadJournal.UnitTest.ArchitectureTests.InitializationFailureStaysAndPublicRetrySucceeds | Passed | 00:00:00.0891235 |
| ReadJournal.UnitTest.ArchitectureTests.ReflectionViewLocatorCachesAndSetsDataContext | Passed | 00:00:00.0303249 |
| ReadJournal.UnitTest.ArchitectureTests.SingletonDetailResetsOldBookBeforeLoadingAnother | Passed | 00:00:00.0049232 |
| ReadJournal.UnitTest.ArchitectureTests.ThreeLayersParametersRepeatedTargetAndBackStack | Passed | 00:00:00.0030741 |
| ReadJournal.UnitTest.DatabaseTests.ActualSqliteTransactionRollsBackOnConstraintFailure | Passed | 00:00:00.0652221 |
| ReadJournal.UnitTest.DatabaseTests.FailedDirectoryCanRetryAfterCauseRemoved | Passed | 00:00:00.0043216 |
| ReadJournal.UnitTest.DatabaseTests.InitializesVersionAndPersistsAcrossReopen | Passed | 00:00:00.0052093 |
| ReadJournal.UnitTest.EditingTests.CancelDiscardAndFailedSavePreserveDraftAndNavigation | Passed | 00:00:00.0464634 |
| ReadJournal.UnitTest.EditingTests.SaveThenLeavePersistsAndMarksClean | Passed | 00:00:00.0050783 |
| ReadJournal.UnitTest.ExtendedBusinessTests.AutoBackupFailurePreventsReplacementAndSqliteFailureRollsBack | Passed | 00:00:00.0038438 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "duplicate") | Passed | 00:00:00.0012225 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "future") | Passed | 00:00:00.0012058 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "json") | Passed | 00:00:00.0013188 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "missing") | Passed | 00:00:00.0014143 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "null") | Passed | 00:00:00.0026151 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "orphan") | Passed | 00:00:00.0024595 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "page") | Passed | 00:00:00.0012034 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "plans") | Passed | 00:00:00.0015104 |
| ReadJournal.UnitTest.ExtendedBusinessTests.InvalidBackupNeverMutatesOriginalData(kind: "version") | Passed | 00:00:00.0013182 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 0, total: 100, days: 0, state: "进行中", suggestion: 100) | Passed | 00:00:00.0000033 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 100, total: 100, days: -1, state: "已完成", suggestion: 0) | Passed | 00:00:00.0000093 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 20, total: 100, days: -1, state: "已逾期", suggestion: 0) | Passed | 00:00:00.0002098 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanDatesAndDenominators(current: 20, total: 100, days: 4, state: "进行中", suggestion: 16) | Passed | 00:00:00.0005375 |
| ReadJournal.UnitTest.ExtendedBusinessTests.PlanUniquenessCompletionAndDeletionDoNotAffectRecords | Passed | 00:00:00.0043482 |
| ReadJournal.UnitTest.ExtendedBusinessTests.StatisticsDemoAndBackupRoundTripPreserveIdsAndPreferences | Passed | 00:00:00.0463777 |
| ReadJournal.UnitTest.NavigationRaceTests.BackAndMenuCannotOpenTwoLeaveConfirmations | Passed | 00:00:00.0060672 |
| ReadJournal.UnitTest.NavigationRaceTests.OldSingletonLoadCannotPublishAfterNewParameter | Passed | 00:00:00.0212812 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 1, end: 1, pages: 1) | Passed | 00:00:00.0037785 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 1, end: 100, pages: 100) | Passed | 00:00:00.0018887 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 10, end: 20, pages: 11) | Passed | 00:00:00.0104561 |
| ReadJournal.UnitTest.ReadingBusinessTests.ClosedIntervalsAndRepeatedReading(start: 100, end: 100, pages: 1) | Passed | 00:00:00.0021007 |
| ReadJournal.UnitTest.ReadingBusinessTests.EditDeleteAndTotalPagesRecomputeCompletionAndProgress | Passed | 00:00:00.0049813 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 0, end: 10, minutes: 1, futureDays: 0) | Passed | 00:00:00.0008957 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 10, minutes: -1, futureDays: 0) | Passed | 00:00:00.0011585 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 10, minutes: 0, futureDays: 0) | Passed | 00:00:00.0009256 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 10, minutes: 1, futureDays: 1) | Passed | 00:00:00.0027893 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 1, end: 101, minutes: 1, futureDays: 0) | Passed | 00:00:00.0008564 |
| ReadJournal.UnitTest.ReadingBusinessTests.InvalidRecordRejectedWithoutPersistence(start: 20, end: 10, minutes: 1, futureDays: 0) | Passed | 00:00:00.0008419 |
| ReadJournal.UnitTest.ReadingBusinessTests.SearchPaginationQuotesFavoritesAndCascadeDelete | Passed | 00:00:00.0156445 |
