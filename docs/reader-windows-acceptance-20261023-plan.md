# feature/reader-native Windows 验收与合并计划

用户已授权：验收 feature 分支，补充 Windows 实机；通过后并入 main。目标分支为 origin/feature/reader-native，基准 e50612b；main 基准 0280730。使用独立 worktree，保留主工作区未提交记录，不触碰用户日常数据。

1. 阅读 raw 原规划、output/reader-plan-v2 三份执行标准和 P0～P7 阶段证据，对照新增实现与平台缺口。
2. Windows .NET 8.0.424 锁定依赖还原、Release 构建及全部生产/原生实验回归；必要时修复并回归。
3. 隔离 READJOURNAL_DATA_DIR，使用确定性 TXT/EPUB 样本验证真实 Windows 窗口：原有手动业务、导入/去重、原生排版与真实拖选/复制、字号重排/焦点/大小变化、定位书摘、菜单离开会话、重开续读、完成/重置、完整备份恢复及删除。记录真实屏幕 DPI/显示器，未实际覆盖的倍率/跨屏不得写为通过。
4. 复查旧库迁移、故障恢复与干净源码可复现性；生成 docs 验收记录与可复核日志/截图。原始资料仅只读，API 零调用。
5. 没有阻塞问题后提交验收/修复并合并 main，按用户授权同步远端 main；保留原本地改动。若有无法修复的验收阻塞，不合并并明确原因。

依据：raw/1-阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md；raw/期末项目与报告要求.docx；output/reader-plan-v2；docs/reader-execution.md；docs/evidence/reader/P0～P7/acceptance.md。不使用外部参考资料。
