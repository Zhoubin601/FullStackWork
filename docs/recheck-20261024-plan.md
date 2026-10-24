# 2026-10-24 拉取更新与全功能复验计划

用户要求：拉取最新更新，并测试所有功能是否达到既定要求。已有工作区修改保留；不改 raw，不覆盖历史 output，不自动提交或推送。真实 API 测试已获本轮授权，仅发送合成短文，密钥只用于进程内存、不落盘；记录元数据和核验结论，不记录密钥或供应商完整响应。

## 依据与范围

- 原始资料：raw/1-阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md。
- 已确认细化范围：output/reader-plan-v2 下的实施规划、接口契约和分阶段验收表；Code/README.md；docs/reader-p9-acceptance-20261023.md；docs/reader-p10-acceptance-20261023.md。
- 当前实现：Code 下 .NET 8 / Avalonia / SQLite 三工程；本轮开始 HEAD=82070fa，分支 main。历史进度文档中的分支状态需按实际 Git 更新。
- 既有用户决定：其他显示缩放补测已跳过；不把 Windows 150% 的结果外推到跨屏/其他 DPI；不声称未提供的真实商业 EPUB 兼容性。

## 执行顺序

1. 保存起始 Git 状态及 raw 摘要，fetch 远端；核对更新内容，使用可保留本地修改的方式同步。
2. 锁定还原、Release 构建、全量自动化测试；有更新时对最终代码重新执行。
3. 执行迁移/单实例、备份恢复中断、TXT导入删除中断、EPUB兼容、生产DI/导航/性能探针及原生分页实验。
4. 用隔离数据目录重新执行实际 Windows UI 阅读与书摘、手动业务、计划统计、备份恢复；真实AI最小闭环验证分析/解释/提取/原文核验/定位/撤销，并删除会话密钥。
5. 失败则定位修复并针对性复验；汇总实际通过、跳过、失败与未测内容，核对 raw 与既有用户修改保留情况。

过程证据写入 docs/evidence/recheck-20261024；未通过的内容不能认作达标。用户未要求重做正式课程报告或发布新包，本轮不改历史报告。

## 外部资料

来源名称：DeepSeek Your First API Call / Chat Completions API。
链接：https://api-docs.deepseek.com/ 、https://api-docs.deepseek.com/api/create-chat-completion/ 。
引用日期：2026-10-24。
核验：官方仍列出 deepseek-flash、deepseek-v4-pro，接口 https://api.deepseek.com/chat/completions；真实调用采用当前项目默认 Flash、thinking=disabled、JSON返回。
