# 2026-11-01 双分支最新状态复核

## 授权与来源

用户要求检查两条分支最新状态，无问题则合并，并明确要求合并后推送。根据 docs/20261024-branch-merge-v1.md 已确认的双分支范围，检查 feature/ui-ux-polish-v2 与 feature/reader-fonts-live-ai。

来源名称：当前仓库源码、Git 历史与远端引用、既有合并记录。
链接或文件名：Code/、docs/20261024-branch-merge-v1.md、docs/20261024-integrated-audit-v1.md、origin 各分支引用。
引用日期：2026-11-01。

## 初步核对

- 工作区起始干净；fetch origin --prune 后 main 与 origin/main 均为 0159d2c。
- UI 分支最新 ca79d15；字体/真实 AI 分支最新 c759bda；均没有主线之外的新提交。
- main...UI 为 5/0，main...字体/AI 为 7/0。
- 历史合并 d28f1d6 的双亲为 ca79d15 与 c759bda；后续 ba08e19 已修复字体缺失和首屏会话激活问题。
- 本次不需要制造重复合并提交；核查当前整合结果，通过后正常推送 main。

## 执行计划

1. 保存 raw/ 与 output/ 文件摘要和分支快照。
2. 复核合并核心代码、冲突标记，完成锁定依赖还原、Release 构建与完整自动化回归。
3. 记录实际结果、边界及文件保护检查，提交本轮过程记录。
4. 再次抓取引用核对分支未变化，正常推送 main 并回读远端提交号。

## 验证结果

- 锁定依赖还原通过：dotnet restore ReadJournal.sln --locked-mode。
- Release 构建通过，0 错误、3 个 NETSDK1206 警告，均为旧 SQLite 原生依赖的 alpine-x64 RID 提示；与 Windows 目标无新增冲突，未宣称验证 Alpine。
- 字体目录注册、搜索预览、手动应用和缺失字体临时后备逻辑保留；首屏重排先进入 Presenting，视口替代首轮排版后仍可激活会话，对应永久回归保留。
- 生产 AI 测试替身仅存在于测试工程，上传同意和会话凭据门禁仍在真实请求前校验；整段选区回归显式注入测试 Handler，不依赖生产模拟入口。
- 阅读器折叠面板、AI 统一范围、字体控件及设置页真实 AI 入口的合并内容保留，未发现阻塞合并的问题。
- Code 内未发现冲突标记，16 个 XAML 文件可解析，git diff --check 通过。
- raw/ 两文件与 output/ 三文件共 5 项保护摘要一致。
- 再次 fetch 核对三个目标引用均未变化；两次 git merge --ff-only 均返回 Already up to date，所有远端分支也均已被 main 包含。

完整 Release 自动化回归已结束，进程退出码 0：共 353 项，351 通过、0 失败、2 跳过，运行约 5 分 53 秒。跳过项为 TxtImportTests.SymbolicSourceAndBrokenInboxLinkAreRejectedWithoutBlockingValidInboxBook 与 CompleteBackupTests.SymlinkInManagedContentOrDestinationIsRejected，均因 Windows 账号缺少符号链接创建权限（ERROR_PRIVILEGE_NOT_HELD）。本轮实际结果与既有已记录基线一致，不将跳过计入通过。

证据：docs/evidence/branch-review-20261101/，包括 restore.log、build.log、tests.log、release.trx、分支引用、两次无新增合并日志及文件保护检查。

## 提交与推送

未改应用源码、原始资料、参考源码或正式交付；本轮新增检查记录与证据，并更新 docs/source-notes.md、docs/analysis-notes.md、docs/questions.md。按用户授权提交这些过程记录后正常推送 main，不强推、不删除分支、不处理既有 stash。具体提交号及推送成功与否以最终 Git 输出和远端回读为准。

## 验证边界

本轮不请求真实付费 AI，不重新进行物理窗口、其他 DPI 或跨屏人工验收，不重新生成正式报告或源码包。符号链接能力场景以当前 Windows 账号权限下的实际跳过结果为准，不作通过声明。
