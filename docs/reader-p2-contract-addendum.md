# P2 契约落实与增量说明

原始依据：output/reader-plan-v2/接口与数据契约-v1（2026-10-22），本文件记录实施中必要的补充，不覆盖规划原件。契约代码位于 Code/ReadJournal.Library/Models/Reader 和 Services/Reader，Avalonia 实现只在 ReadJournal/Reader。

## 1. 当前实现与后续接口

P2 落地全部核心 DTO/跨层接口声明、新持久化行及 SQL 约束。当前实际注册/实现为数据协调器、迁移、统一状态计算器、实体校验器、旧 JSON 适配、电子书进度维护、只读文档目录、UI 原生布局服务。P3～P9 的备份协议、导入解析、会话、AI、平台与偏好接口只声明；按对应阶段提供真实实现后注册，不以占位实现冒充可用功能。ReaderView、导入入口和 API 均未开启。

P2 文档目录只接受合成的不可变纯文本，验证顺序/长度/字符簇，最多两本、每本 2000 万 UTF-16 单元。它支持实际片段读取、抽取、位置百分比/解析和打开身份核对，不是正式文件导入器。P4 才添加受管文件加载及 canonical manifest 摘要验证，P7 才增加图片/样式。布局引擎目前保留 P1 全文与边界数组的内存模型，几何最多8块；首行附加缩进/段距目前明确拒绝非零值，原文中的缩进保留。ReaderSurface 使用同一 TextLine 绘制/命中/选择计划；P5 才绑定正式 VM 与输入生命周期。

## 2. 懒分页的章内页身份

原拟 `PageRef(LayoutId, ChapterKey, int PageInChapter)` 要求任意位置在返回首屏前得到确切章内页号，这会迫使百万字单章从头计算，与 P1 的就近排版目标冲突。

实施为 `PageRef(LayoutId, ChapterKey, int? PageInChapter, int BlockStartOffset=0, int PageInBlock=1)`。稳定引用由 LayoutId+ChapterKey+块起始偏移+块内页号识别，章内页号在前缀已知时补充，未知为 null；已知章内/全局页号仍从1开始，不把 null 当0。不持久化块/页引用为正文位置。旧布局释放后不能继续使用其页引用。

为使布局只依赖正式文档接口，新增 `IReaderDocumentService.DescribeAsync(DocumentKey, ct)`，读取已加载文档的章节清单；不通过具体实现或隐藏当前书字段旁路取元数据。

## 3. 数据协调与 SQL 门禁

保留 IDatabaseService 全部旧签名，新加接受 IDataOperationLease 的 ReadAsync/WriteAsync，以及接受 IDataMaintenanceLease 的 ReadAsync/WriteAsync。旧签名自动取得正常操作租约后进入数据库串行门禁；显式租约重载验证归属/代际/释放状态，避免维护内部 SQL 再申请正常门禁导致自锁。调用者须先初始化，再取得租约，等待 SQL 完成才释放；SQL 委托内禁止异步调用其他存储。

维护先阻止新操作并排空已有短操作；活动 Reader 注册使维护立即返回“请先退出阅读器”，不等待。注册与维护检查在同一锁内。成功数据替换提交后才 CompleteReplacement；备份不允许改代际，失败/取消不改代际。普通操作/维护取消均释放等待门禁。

同目录 FileStream/FileShare.None 使用 OS 锁，保留 lock 文件不代表持有锁。Linux 独立子进程已测试占用拒绝、释放成功与崩溃后成功；Windows 尚未实测。新上下文任务显式捕获 DataEpoch；旧无上下文接口只保证操作期间与维护互斥，不能推断未来 Reader 任务已经带身份。

## 4. 状态与 schema 2 补充列

EbookProgress 新增标量 `CurrentPercent`（0～100），由后续会话存储在文档服务计算正文百分比后与位置同时保存。避免每个列表查询加载全文，不能从动态页码计算或相信任意 UI 传值。HasStarted 与 CompletedAt 独立控制状态；完成后回看不清除完成标记，重置保留已关闭会话的历史时长/次数。Manual 旧页码/重读/完成日期语义保持。

统一状态计算器进入 Book/Plan/Statistics 查询，旧 ReadingService.Calculate 仅接受 Manual 并委托同一计算器。BookProgress/PlanItem 的电子书文案使用百分比；统计先合计原始毫秒再换算分钟，不先逐条取整；真实页数仍只来自手动记录。

ResetProgress 未来由会话服务委托 P2 的 IEbookProgressMaintenance。调用前必须完成“关闭/核对会话提交→卸载布局→释放 ReaderRegistration”，然后单独取得维护权；不能持动作/普通操作租约升级锁。服务核对 epoch/BookUid、历史计划冲突后提交重置，再换代际。

AiGenerationBatch 增加 `InsertedQuoteIdsJson` 不可变提交回执。Quote/AiGenerationBatchItem 被级联删除后仍可重放原提交 ID；不能从仍存活的关联项推算原结果。P2只建立约束和删除回归，P8实现写入/撤销/幂等查询。

## 5. schema 1 JSON 与阶段安全边界

Book.Kind/BookUid 与 Quote.Revision 在现有 FormatVersion1 JSON 中不输出，保留旧格式原字段。BackupV1Adapter 显式升级为 BackupDataV2：每本 Manual、新唯一 BookUid、Quote.Revision=0；旧 JSON 不具有既有业务身份。v2 DTO 显式携带这些新字段，避免通用序列化误丢身份。

P2 纯手动书保留旧 JSON 导出/恢复并执行真实维护门禁，恢复成功更新代际；含电子书/定位/批次的数据集在旧 JSON 导出/恢复前明确拒绝，避免完整 ZIP 协议尚未实现时静默丢数据。P3 才统一切换完整 ZIP；该暂时保护不等于完整备份已经实现。备份校验与正常存储共用 ReadingService 和状态语义。JSON 自动备份在维护权保护下取得快照后于数据库门禁/事务外写文件，再进入替换事务，避免 SQL 内等待文件 IO。

迁移仅单事务增量添加列/表/索引/触发器，三处故障注入验证 DDL、回填和 user_version 一起回滚；不重建清空旧库。BookUid 增加 NOT NULL 列后逐行填充并建唯一索引，Kind 默认 Manual，Quote.Revision 默认0。DB触发器限制类型/身份变更及种类对应页码，外键由真实 SQL 声明。
