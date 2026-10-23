# P6 本地书摘、统一历史与阅读状态

依据reader-plan-v2接口契约及执行表P6-01～08；承接P5远端af7ebb60bb65498628cd3a9631a82b92de7701c2。

- ReaderQuoteService遵守会话动作门禁→正常数据租约→SQL门禁。提取经过文档服务的精确纯文本范围；跨章节可连接，图片导致不连续则拒绝整个选区。Quote.Page=null；Quote和QuoteOrigin同一事务插入，原文永久存在Origin.OriginalText中。两处注入故障检查事务回滚。
- DedupeKey以BookUid和完整起止位置生成SHA256；同文档/范围返回原QuoteId，既不新增也不覆盖已有感想，不同位置的相同文字可分别保存。编辑Quote.Text/Thoughts/收藏只更新Quote和Revision；定位书摘不可挪至另一书籍。
- ResolveQuoteAsync验证当前源/规范化摘要/解析版本、范围和OriginalText；失配返回明确原因，不猜页号。ReaderQuoteNavigation将持久化范围传ReaderOpenRequest.Target；同书定位保留会话，Reader高亮与字号重排复用同一原生布局。
- Surface鼠标拖选和Shift单击延伸使用NativeLayout.Hit/SelectionRects；Ctrl+C调用系统剪贴板，Ctrl+S保存书摘。P6真实桌面通过粘贴到感想框并比较SQL字段验证原生剪贴板，不通过注入产品内部状态模拟。
- ReadingHistoryService把手动记录与closed-session合并；HistoryKey为来源枚举+各自ID，按实际排序时刻/来源/稳定ID排序、分页。手动日期以本机午夜表示，会话用带offset的实际关闭时间；活动日期保留关闭当地日期。详情每页50项，概览最近5项。手动仍可编辑，会话只可删除活动。
- DeleteAsync先验证epoch并持有数据租约；手动删除复用原有计划一致性规则，会话删除只删EbookReadingSession，不写Progress/完成标记/书摘。统计与书籍活动随剩余历史重算。
- 统计先累加原始毫秒再展示，不对每个短会话进位；真实页数仅来自手动记录。七日图同时显示手动页数与时长，两条图各自按实际最大值归一化，电子书只有时长条。
- MarkCompletedAsync在会话动作门禁内显式写首次完成时间，回看/向前翻不取消。Reset入口由Main.RequestInPlaceAsync串行：确认前预验历史计划；关闭当前会话/释放注册→独占重置（再校验BookUid/epoch/多计划）→新Reader上下文。历史时长/书摘保留，多计划时SQL整次拒绝。正文百分比只表示当前位置，UI明确不等于阅读覆盖率。

新增服务均经ServiceLocator单例注册，沿用原导航/反射ViewLocator及原手动服务。P6不增加业务schema版本、不修改原始资料或冻结fixtures，无真实内容API。
