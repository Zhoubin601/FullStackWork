# 项目报告与交付记录索引

## 1. 当前正式交付入口（`output/提交材料/`）

根据课程原始要求 [`raw/期末项目与报告要求.docx`](../../raw/期末项目与报告要求.docx)，期末作业最终必须提交 **PDF 格式项目报告** 与 **项目源码压缩包（ZIP，不超过 10MB，不含编译结果）**。

当前唯一正式交付目录为 [`output/提交材料/`](../../output/提交材料/)：

| 文件角色 | 文件路径 | 页数 / 文件数 | 字节大小 | SHA256 | 提交属性说明 |
| --- | --- | --- | ---: | --- | --- |
| **当前正式项目报告（PDF）** | [`output/提交材料/阅记-项目报告.pdf`](../../output/提交材料/阅记-项目报告.pdf) | **27 页**（含 19 个章节标题与目录项、19 个超链接、19 个 PDF 书签、14 表、10 图、7 段代码） | `2,199,879` 字节（约 2.10 MB） | `0605288c6a50ab1e5ff19fb276624b06154e954426c96e1d3a751de6d2ff6841` | **唯一正式报告提交版本**（课程指导书明确要求以 PDF 格式提交报告） |
| **当前正式源码压缩包（ZIP）** | [`output/提交材料/阅记-源码.zip`](../../output/提交材料/阅记-源码.zip) | **208 个源码与资源文件** | `342,012` 字节（约 334.00 KB，`< 10MB`） | `118095ab068e02276fd341832fdb60b9fad254142b7c3e500617df2e6dba8432` | **唯一正式源码提交版本**（与 [`Code/delivery-files.txt`](../../Code/delivery-files.txt) 及 [`Code/`](../../Code/) 逐字节一致） |
| **报告可编辑源文件（DOCX）** | [`output/提交材料/阅记-项目报告.docx`](../../output/提交材料/阅记-项目报告.docx) | **27 页** | `2,447,721` 字节（约 2.33 MB） | `7563abfa1941cedd8c336ad84eff4a04ccca97ae66027f4dbd690c25bfbca20c` | **仅作为可编辑源文件保留**，供后续编辑或重新导出 PDF 使用，不替代正式 PDF 提交件 |

---

## 2. 报告规模、源码对应与测试核验摘要

- **项目正式名称**：阅记原生电子书阅读器与 AI 阅读助手（ReadJournal）
- **代码与视图规模**：生产代码（`ReadJournal` + `ReadJournal.Library`）共 **137 个类、41 个接口、134 个 C# 源文件、15 个 AXAML 视图文件（含 11 个业务视图）**；课程参考项目 `DailyPoetryA` 为 63 个类、10 个接口、9 个视图（70% 门槛为 45 个类、7 个接口、9 个视图）。
- **数据库结构**：SQLite `user_version = 2`，共 **10 张业务表**（`Book`、`ReadingRecord`、`Quote`、`ReadingPlan`、`EbookFile`、`EbookProgress`、`EbookReadingSession`、`QuoteOrigin`、`AiGenerationBatch`、`AiGenerationBatchItem`）及 `AppMetadata` 元数据表。
- **构建与测试结果（2026-11-01）**：工作区 `Code/` 与 [`output/提交材料/阅记-源码.zip`](../../output/提交材料/阅记-源码.zip) 独立解包目录均通过 `dotnet restore --locked-mode`、`dotnet build -c Release --no-restore` 与 `dotnet test -c Release --no-build --no-restore`，共 **353 项自动化测试：351 项通过、0 项失败、2 项 Windows 符号链接测试因缺少符号链接权限跳过（不计入通过数）**。

---

## 3. 历史版本与过程记录存放位置（非正式提交入口）

`docs/report/` 及其子目录下的所有文件均为**历史过程记录、排版脚本与阶段性核验归档**，**不属于最终正式提交内容**，不得作为正式提交件使用：

- [`docs/final-submission-audit.md`](../final-submission-audit.md)：最终交付审查与仓库清理完整报告
- [`docs/delivery-checklist.md`](../delivery-checklist.md)：最终交付核对清单
- [`docs/20261101-report-repair-results-v2.md`](../20261101-report-repair-results-v2.md) 与 [`docs/report/repair-20261101-v2/`](repair-20261101-v2/)：2026-11-01 报告版式修复、SVG 图件重绘与校验脚本归档
- [`docs/20261101-v1-report-audit.md`](../20261101-v1-report-audit.md) 与 [`docs/report/audit-20261101-v1/`](audit-20261101-v1/)：2026-11-01 历史版本独立复检记录
- [`docs/report/update-20261101/`](update-20261101/)：2026-11-01 封面模板对齐与 Word 原生导出脚本（`render-native.ps1`）
- [`docs/submission-readiness-20261025.md`](../submission-readiness-20261025.md)、[`docs/report/readiness-20261025/`](readiness-20261025/)、[`docs/report/submission-editorial-20261025/`](submission-editorial-20261025/)、[`docs/report/submission-20261025/`](submission-20261025/)：2026-10-25 阶段性整理与归档材料（含历史快照备份）
- [`docs/cleanup-results-20261026.md`](../cleanup-results-20261026.md)：2026-10-26 历史过期文件清理记录
- [`docs/report/release-v1/`](release-v1/)、[`docs/report/release-v2-20261025/`](release-v2-20261025/)、[`docs/report/revision-v2/`](revision-v2/)、[`docs/report/word-v2/`](word-v2/)、[`docs/report/word-v3/`](word-v3/)：早期阶段性脚本与历史验证记录归档
