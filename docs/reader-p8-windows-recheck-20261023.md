# P8 Windows 复核记录

日期：2026-10-23。来源分支 origin/feature/reader-ai-p8=4baef83；工作分支 codex/reader-p9。使用本机 Windows/.NET SDK8.0.424、窗口实际144DPI（150%）。

## 自动化

锁定依赖还原及Release构建成功。首次327项：324通过，1失败，2跳过。失败为 BackupRoundTripPreservesReceiptAndUndoAndNeverContainsSessionCredentials 在Windows读取仍被SQLite独占的数据库文件；修正为业务完整性检查完成后关闭数据库，再扫描包含数据库在内的全部持久文件，不排除数据库以规避检查。修正后该用例1/1通过。

2项符号链接测试因Windows账户权限跳过，保留该限制，不写成通过。首次日志/TRX和修复后TRX均保留于 docs/evidence/reader/windows-p8-20261023。后续包含P9准备改动的独立白名单源码全量结果另行追加。

## 实际窗口

独立 READJOURNAL_DATA_DIR，仅使用86字符自建短文和Mock，未调用真实API。

- 书库扫描导入，进入原生阅读器，范围确认显示当前页86字符。
- 分析后Quote/QuoteOrigin/AiGenerationBatch均0；实际结果显示不入库。
- 生成后Quote、QuoteOrigin、AiGenerationBatch、AiGenerationBatchItem各1。
- 撤销后Quote/Origin/Item各0，批次保留且Status=1。
- 设置页可见配置与会话密钥入口；正常退出0，重开后自动会话1、已撤销批次仍在、无书摘重建，再正常退出0。
- 每次只读核对数据库integrity_check=ok，foreign_key_check无错误；截图01～06已目视复核阅读器与生成结果。

证据在 windows-p8-20261023/desktop，包含UI动作、截图和只读数据快照。脚本首轮误将收件目录设为books/inbox（应用只扫描books本层），以及重启断言误用ReadingSession表名（实际EbookReadingSession）；更正驱动后完成同一真实流程，两者均未改生产行为。失败脚本保留。

## 范围和后续

本次复核P8本地/Mock业务，未据此宣布P9真实联调或M3通过。官方核验发现旧模型不在当前列表，这是P9准备时修正的真实模式配置缺口。100%/200%及跨屏、真实商业EPUB未追加覆盖。raw及既有output全部文件SHA256保持不变，见protected-verification.json。本轮不生成新正式报告或源码交付，不合并main、不推送。

## 全量复核结果

200文件白名单独立打包、解包、锁定还原及Release全量回归完成：333总计，331通过、0失败、2符号链接权限跳过（原P8的327项+6项P9准备回归）。证据P9-preparation/clean-release.trx、clean-test.log及source-sha256.json。此处全量结果在第三次真实调用后的提示词/细分错误修订之前；后续修订的回归结果另记，不混淆源码版本。P8原始生产实现未发现本地闭环阻塞，测试扫描修正已包含于独立回归。
