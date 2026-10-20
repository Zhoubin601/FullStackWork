# 本轮独立诊断用例

`AuditRegressionTests.cs` 是 2026-10-20 审查时增加的 5 项缺陷复现，不属于正式项目原有 40 项测试，也未加入 Code 或交付 ZIP。

在当前版本上，5 项正确行为断言均失败，证明审查发现的问题；请勿将这些失败描述为验收通过。加载/保存时序使用 TaskCompletionSource 控制；计划及备份状态使用真实临时 SQLite。

复现方法：将正式源码 ZIP 解压至项目外的临时目录，先按交付说明还原依赖并运行原有 40 项测试；再仅将本目录的 C# 文件复制到临时解包目录的 `ReadJournal.UnitTest/`，在解包根目录执行：

```sh
dotnet test ReadJournal.UnitTest/ReadJournal.UnitTest.csproj -c Release --no-restore --filter 'FullyQualifiedName~AuditRegressionTests' --logger 'trx;LogFileName=audit-regressions.trx'
```

本轮使用 .NET SDK 8.0.424 / Debian 13，最终结果为 0 通过、5 失败、0 跳过。证据：`docs/evidence/audit-20261020/regressions-final.log` 和 `audit-regressions-final.trx`；总体结论见 `docs/project-audit-20261020.md`。修复后应让用例通过，并复验正常流程。

## 修复后状态

用户授权后，缺陷已修复并分别纳入 Code/ReadJournal.UnitTest/EditingRegressionTests.cs（20项）和 PlanConsistencyRegressionTests.cs（8项）。正式总测试集68项全部通过。此处5项保留为历史复现；后2项当前应在重复计划关联步骤提前拒绝，应运行永久回归验证拒绝及数据完整性，不把旧路径假设继续成立当作修复要求。详见 docs/repair-results-20261020.md。
