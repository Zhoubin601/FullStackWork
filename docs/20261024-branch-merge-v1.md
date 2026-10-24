# 2026-10-24 分支合并记录

## 授权与范围

用户要求“拉取最新更新，合并分支”，随后确认将 UI 优化、字体与真实 AI 两个分支均合入 main。

- 仓库：origin = https://github.com/Zhoubin601/FullStackWork.git。
- main 远端基线：8da5ba6（保留整段选区书摘、简化阅读器面板）。
- UI 优化：feature/ui-ux-polish-v2，ca79d15。
- 字体与真实 AI：origin/feature/reader-fonts-live-ai，c759bda。
- 本次先完成本地合并与验证；不把既有未跟踪资料提交到合并中。

## 来源与保护

- 依据：用户本轮确认、实际 git fetch 结果、上述三个提交的源码与提交历史。
- raw/ 仅记录哈希，不改写；不需要外部调研资料。
- 合并前有 119 个未跟踪文件，哈希与 2 个 raw/ 文件哈希记录在 docs/evidence/merge-20261024/before.json。

## 执行计划

1. 检查工作区，拉取远端引用，记录原文件摘要。
2. 将本地 main 快进到 origin/main，再合入 UI 优化。
3. 合入字体与真实 AI，逐项解决冲突，保留整段选区、面板折叠、主题样式、字体搜索/预览及真实 AI 行为。
4. 验证冲突标记、构建、完整单元测试和原文件摘要；记录实际结果后提交。

## 冲突处理

本地 main 从 70d46a8 快进到远端 8da5ba6，再快进到 UI 分支 ca79d15，随后合并 c759bda。

10 个冲突文件已逐项处理：

- ReaderView.axaml：以新版 UI 布局为基础，将已安装字体搜索、候选预览、字号候选、恢复默认字体、实时预览放入原有可折叠排版面板；保留目录、AI 独立折叠及范围显隐。
- SettingsView.axaml：保留紧凑双列卡片、主题和反馈样式，移除 Mock 控件，合入真实 AI 上传说明和模型提示。
- ReaderViewModel.Ai.cs：保留统一 AI 范围与整段选区流程，采用真实服务提示。
- CHANGELOG.md、README.md：合并功能说明，不将旧分支验收写成本轮结果。
- analysis-notes.md、questions.md、reader-execution.md、source-notes.md、project-context.md：保留两边记录；执行表去除重复 P9～P10 行。

使用 ui-ux-pro-max 技能复核界面整合；本次沿用已有字体搜索实现与语义主题资源，未重新设计全站。

## 验证过程

- 合并前的基线测试耗时较长，已主动中止，未取得完整结果；该次运行不作通过依据。
- 合并后完整 Release 回归已启动，日志与 TRX 保存在 docs/evidence/merge-20261024/。
- 历史分支验收不视为本轮验证，最终结果见下文。

首轮完整回归：350 项，347 通过、1 失败、2 跳过。失败项为旧整段选区 HTTP 测试仍通过无 Handler 的 DeepSeekClient 使用已移除的生产模拟入口，触发 UploadNotAcknowledged。已为该测试显式设置测试上传同意、临时虚构凭据，注入字体分支迁入测试工程的 AiMockHandler；生产 DeepSeekClient 行为未放宽。修正后的单项回归 1/1 通过，日志为 selection-http-fixed.log。

完整最终回归结束：350 项，348 通过、0 失败、2 跳过，耗时约 5 分 14 秒。两项跳过均为 Windows 当前账号无符号链接创建权限（ERROR_PRIVILEGE_NOT_HELD）：TxtImportTests.SymbolicSourceAndBrokenInboxLinkAreRejectedWithoutBlockingValidInboxBook 与 CompleteBackupTests.SymlinkInManagedContentOrDestinationIsRejected。日志为 tests-final.log 与 merged-release-final.trx。

文件保护复核：合并前 119 个未跟踪文件与 2 个 raw/ 文件摘要全部一致；原有未跟踪文件未加入暂存。raw/ 与 output/ 无合并改动，两个冲突界面 XML 可解析，git diff --check 与暂存区格式检查通过。对应结果见 file-preservation.json、static-verification.json。

## 交付与边界

- 当前 main 将包含 ca79d15 与 c759bda 两个分支的完整历史；本轮修正与证据随合并提交保存。
- 再次 fetch 核对远端三个分支提交均未变化；本次仅作本地合并，不推送远端，不删除分支，不操作既有 stash。
- 原始依据为 Git 源码与用户确认，无外部资料；raw/ 与旧正式交付保持不变，未重新打包 ZIP 或导出报告。
- 本轮完成 Windows Release 编译与自动化回归，未重新进行真实窗口人工验收或真实 DeepSeek API 调用；这两项及符号链接权限场景仍需另行复核。
- 本轮不存在未解决的合并冲突或失败测试。实际提交号以 Git 历史及最终回复为准。
