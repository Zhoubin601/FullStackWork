# 桌面 UI 功能链路独立复验

日期：2026-10-20。直接证据为 `Code` 实际源码、独立 X11 窗口、只读 SQLite 查询、系统 GTK 文件选择器生成的 JSON、应用本轮日志。参考资料为 `Code/README.md`、`docs/tools/ui_driver.py` 和各视图/命令实际源码；未修改原始指导书、参考项目和应用代码。

## 结论

本轮实际操作已打通“空库初始化 → 新增书籍 → 阅读记录 → 书摘收藏 → 阅读计划 → 概览/统计 → 导出 → 恢复 → 重启”。基线版本 10 个业务页面全部实际到达。随后退出旧进程，使用本轮修复后的最终 Release 程序重启，复验最近记录、编辑保存返回、记录修改/删除、书籍级联删除、恢复及恢复后切页，均正常。未发现新的 UI 阻塞或绑定错误。

## 环境与边界

Debian 13 / Linux x64，.NET 8.0.424，Xvfb `:118`，1280×960。系统未预装 Xvfb/xdotool，已从允许的 Debian 软件源下载至 `/workspace/scratch/ui-audit` 并用户目录解包。数据隔离在 `/workspace/scratch/ui-audit/data`，没有使用用户日常数据库。全部写入通过真实 UI 完成，SQLite 只用于事后只读核对。

基线进程来自提交 `20d9c67`；最终进程来自同一工作区本轮架构/异步修复后的 Release 构建。最终程序集 SHA256 保存在 `docs/evidence/architecture-flow-20261020/ui/validation.json`，没有把旧截图当成最终二进制证据。

Windows 未实测。X11 日志保留 DBus/libX11 名称发现、SESSION_MANAGER、软件 GLX 回退提示，实际使用软件渲染；不表示硬件 GPU 验证。忙碌期间导航的确定性竞态由主代理永久测试验证，本 UI 复验只检查用户正常操作及修复后的页面加载。

## 实测流程与证据

证据目录：`docs/evidence/architecture-flow-20261020/ui/`。

| 流程 | 实际结果 | 截图 |
|---|---|---|
| 新库启动 | 自动初始化并进入空概览、空书库 | `01-initialization.png`、`05-library.png`（01实际为初始化完成后的空概览） |
| 新建 120 页书籍 | 书名、作者、分类、简介正确保存，详情可达 | `06-book-editor.png`～`09-book-detail.png` |
| 新建阅读记录 | 第10～20页、15分钟；详情进度20/120，累计11页 | `10-record-dialog.png`～`12-record-saved.png` |
| 新建收藏书摘 | 正文/感想、页12及收藏写入；默认关联详情书籍 | `13-quote-editor.png`～`15-quote-saved.png` |
| 新建阅读计划 | 使用真实日期选择器设10月24日；剩余100页，每日建议20页 | `16-plan-editor.png`～`19-plan-saved.png` |
| 页面/统计 | 10业务页全部可达；统计11页、15分钟，概览关联书籍、记录、计划 | `20-quotes.png`～`24-settings.png`，其他编辑/详情页见前述截图 |
| 导出/恢复 | 原生GTK保存/打开选择器，JSON导出成功；恢复确认后产生自动备份 | `25-export-picker.png`～`30-restore-success.png` |
| 最终版重启 | 四表数据均保留，概览近期记录含正确书名及11页/15分钟 | `31-final-restart-overview.png`、`32-final-detail.png` |
| 最终版编辑 | 作者改为 `UI Audit Author final`，保存返回；记录改为10～30页、25分钟，详情显示21页/25% | `33-final-book-editor.png`～`35-final-record-updated.png` |
| 最终版删除 | 删除记录重新计算；删除书籍连同书摘/计划，四表计数全部为0 | `36-final-record-delete-confirm.png`～`39-final-book-deleted.png`、`deletion-counts.json` |
| 最终版恢复和切页 | 通过系统选择器从原JSON恢复，成功离开设置到概览，四表恢复1/1/1/1；最近记录/统计重新正确显示 | `40-final-restore-confirm.png`～`45-final-statistics.png` |

中间一次 GTK 路径键入出现测试驱动输入错误，文件选择器明确拒绝不存在的路径；随后从 Recent 列表选择实际导出文件完成恢复，见28/29。初次后台 Xvfb 会话退出导致的启动失败仅是环境准备记录，`app.log`/`xvfb.log`与02/03属于准备过程，不计作应用功能失败或成功证据；有效运行日志为 `app-run.log` 和 `final-app.log`。

## 数据与日志核验

最终恢复后只读查询：Book=1、ReadingRecord=1、Quote=1、ReadingPlan=1。记录为 BookId=1、StartPage=10、EndPage=20、Minutes=15；书摘 Page=12、IsFavorite=1；计划目标日期为2026-10-24。操作过程产生两份自动备份（第一份在旧版同库恢复前，第二份在最终版空库恢复前）。恢复后的作者回到原JSON中的 `UI Audit Author`，表明恢复确实替换了编辑后的数据。

`app-run.log` 和 `final-app.log` 的 `[Binding]`、`BindingError`、`Binding error` 诊断计数均为0。最终程序集哈希、四表完整记录、自动备份名称、截图清单、平台限制详见 `validation.json`；导出文件副本为 `roundtrip.json`。保留本轮 X11 驱动 `ui-driver.py` 以说明截图来自实时窗口，未改写渲染图像。
