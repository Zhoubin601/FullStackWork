# P3 完整备份与恢复协议实现说明

依据 reader-plan-v2 与 P2 契约补充；本文件记录已实现契约的落点和阶段边界，不修改原规划。

## 数据与文件格式

ZIP v2 只包含 manifest.json、data.json 及显式登记的 books/managed/<FileId>/ 内容。磁盘的 books 根是收件区，恢复只替换 managed 子目录，绝不移动收件原件。ZIP 保留同一相对路径，避免 DTO 与磁盘路径的隐式转换。manifest 含格式2/schema2、创建时间、data.json SHA-256、每个文件长度和SHA-256。AppMetadata.LastRestoreId 是恢复提交点，由新恢复生成，不作为历史业务数据重放。

data.json 使用 BackupDataV2：原四表与所有电子书/定位/批次表，BookUid/Kind/Quote.Revision 在独立wrapper明确输出。保留 AI 不可变提交回执，包括已删除明细的原ID。旧日期按 SQLite 的无Kind ticks语义规范化，不能仅因旧 JSON 时区后缀不同误判字段丢失。新会话时间仍使用UTC ticks+原偏移+本地日期。

CanonicalManifest v1 固定章节顺序、UTF-16LE无BOM文本/摘要、块和样式范围、图片声明、目录目标和可计量正文单元数。目录目标不含自身 DocumentKey，以免 canonical 摘要自引用。CanonicalSha256 由该固定模型序列化派生，源文件/清单本身还有独立文件摘要；ParserVersion/FileId/SourceSha256 共同组成正文身份。P4将使用此文件契约加载受管TXT，P7才提供完整图片排版/解码。

检查顺序：受限容器/路径→JSON完整性/重复属性→实体关系/种类/回执→文件长度/哈希→canonical版本/块/字符簇/原句映射/位置与百分比一致性→实际文件集合。图片先受限读取PNG/GIF/JPEG/WebP头，核对媒体和尺寸/25MP预算；不在 Library 依赖 Avalonia 解码，P7渲染器仍须验证实际解码预算。

进度及会话起止百分比均由对应章节位置的可计量正文单元数复核（不计图片占位符，允许1e-6浮点误差），不接受位置合法但百分比与正文不符的包。

当前集中限额：压缩包512MiB、总展开1GiB、单条目300MiB、JSON16MiB、条目20000、单源文件100MiB、单文档2000万UTF-16单元、字符簇1024单元、图片25MP。可注入 BackupLimits 测试边界；不是无上限解压，也不宣称已压力测试到上限。

只接受普通文件条目，拒绝目录/链接/大小写冲突/清单外文件、绝对路径、反斜杠、冒号、点段、系统保留名及重解析点。只在私有校验临时目录受控展开，不调用通用ExtractToDirectory。无效输入不触碰当前SQL/managed/自动备份。受管目录若有未登记文件，完整备份明确拒绝，避免整体替换时静默丢弃；P4对账器后续负责这些孤立资源。

## 导出与恢复提交点

正常导出取得Backup维护权，只读快照及文件IO不在SQL事务内混做；同目录临时ZIP写完Flush(true)，重新打开完整校验成功后原子替换目标。失败保留已有可读目标，不留下伪完成损坏包。Linux额外对重命名父目录fsync；Windows仍待实测。

恢复输入先校验为不可变临时副本；再取得Restore维护权、拒绝活动Reader、处理旧日志，导出并验证当前完整自动备份。自动备份位于 backups/auto-backup-<RestoreId>.zip，绝不只备份旧JSON。

1. staging/<RestoreId>/books/managed 准备并复核目标。
2. operations/restore-<RestoreId>.json 持久记录RestoreId、阶段、旧managed是否存在、自动备份相对路径、新旧实体摘要及文件清单；外层存payload摘要。
3. 旧managed隔离到trash/<RestoreId>/books/managed，再安装目标managed。日志先于第一次目录变更，后续阶段更新原子写入。收件区、secure、本机主题及缓存目录不进入此根移动。
4. 一次SQLite事务按依赖逆序删除、正序插入目标实体，更新LastRestoreId，核对全部业务快照/外键/完整性。SQL失败回滚。
5. 提交成功才更新DataEpoch，释放文档/布局缓存并清空单例旧内容/返回栈；取消已发生提交后不能谎报“未保存”。
6. 数据已确认完整后写CleanupCommitted或CleanupRolledBack，再删除trash/staging/日志；清理失败只报告警告并保留可重试日志，允许后续普通修改。下次启动对cleanup-only日志不按过期业务摘要误判当前数据损坏。

如果已提交但日志收尾或缓存失效失败，仍返回已提交结果和重启提示，同时阻止普通操作，直到重新启动对账。未提交且补偿无法确认完整旧集时也阻止普通操作，保留两套文件和自动备份，明确RecoveryRequired。

## 启动恢复与架构

DatabaseService 先取得同目录进程锁和维护权，最小打开**同一共享连接**，在schema迁移和IsInitialized之前调用RestoreRecoveryService；高于支持版本仍先拒绝。IOperationRecoveryService映射到同一DatabaseService，InitializationViewModel等待其成功才导航主业务，没有新旁路数据库连接或构造循环。

未提交：数据库摘要必须是旧集，校验自动备份与隔离旧文件，再撤回目标/还原旧managed。已提交：数据库摘要必须是新集，核对/补齐目标managed，再收尾。未知/多份/损坏日志、文件摘要不足时停止主业务，不静默清空。原子日志重写遗留临时文件仅在有效主日志存在时识别；最终随主日志清理，不能无依据删日志。

公共ManagedBookFiles提供实际文件写/移动/安装/日志及checkpoint故障点，后续导入/删除复用；当前只实现restore日志类型，P4/P7引入其他操作时扩展恢复分派，未知类型当前停止处理。

新增 IDataSetInvalidationService：UI实现通过原资源定位器访问单例，不构造注入Main形成循环；释放同一文档/布局实例并调用VM.InvalidateDataSet，保留Settings当前页，清除旧内容和栈。列表/概览/统计的旧异步响应核对ParameterVersion，不回填已清空单例。原DataEpoch租约仍拦截恢复前等待的旧写入，即使整数ID/BookUid复用也不接受旧代际。

## 用户入口与阶段边界

IBackupService仍为原外观，导出委托完整ZIP，恢复委托完整服务并返回完整自动备份路径；LastWarning提供清理/重启说明。设置选择器保存ZIP、打开ZIP/旧JSON；确认文案说明整体替换电子书与会话，本机主题/密钥保留。旧JSON输入生成Manual、新BookUid、Revision=0，不混入当前电子书。

P3只验证合成原书/canonical/资源，不启用真实导入或Reader路由。四个checkpoint的真实Process.Kill验证是应用进程中断恢复，不等于断电、磁盘损坏或跨文件系统原子性证明。恢复staging/trash位于同一数据目录；原始来源文件永远不在删除或移动清单内。
