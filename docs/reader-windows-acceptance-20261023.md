# feature/reader-native Windows 验收与合并依据

日期：2026-10-23（Asia/Shanghai）。验收对象 `e50612ba60f7823efc7d65f95266c8c6ec4547c3`，main 基准 `0280730`。用户已授权“验收 feature 分支，补充 Windows 实机，如果没问题就并入 main”。使用独立 worktree；本机应用均指定隔离 READJOURNAL_DATA_DIR。

本轮结论：P0～P7 与原有手动业务在本机 Windows x64、实际 150% DPI 的已测范围内通过，未发现阻塞合并的业务问题，可以按授权合并 main。此结论不等于 P8～P10 或全部 Windows 显示环境已验收。

## 来源与范围

- 原始资料：`raw/1-阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md`、`raw/期末项目与报告要求.docx`；SHA256 与未改动检查在 `docs/evidence/reader/windows-20261023/summary.json`。
- 执行标准：`output/reader-plan-v2/` 三份已确认规划、`docs/reader-execution.md`、P0～P7 阶段协议与验收记录。
- 代码：feature 分支的 Code；保留 Linux 历史结果，所有下述 Windows 结论来自本轮新执行。
- 本轮不生成正式报告或源码交付包，不改 raw/output/参考项目，不调用内容 AI API。NuGet 还原和 Git 同步使用既有来源，不新增外部技术论据。

## 环境与自动化

Windows 11 x64（10.0.26300），.NET SDK 8.0.424，Runtime 8.0.30。真实窗口 DPI=144，Avalonia RenderScaling=1.5，实测全屏 2560×1600；硬件与系统字段见 `environment.json`。不调整用户系统缩放、不申请管理员权限。

以下证据路径均相对于 `docs/evidence/reader/windows-20261023/`。

| 检查 | 本轮 Windows 结果 | 证据 |
|---|---|---|
| 锁定依赖还原及生产 Release 全量回归 | 最终 272 项：270 通过、0 失败、2 跳过 | restore.log、test-fixed.log、windows-release-fixed.trx |
| 186 文件白名单独立源码 | 重新打包/独立解包/锁定还原/Release 测试，同样 270/0/2 | clean-restore.log、clean-test.log、windows-clean-source.trx、source-package.json |
| 原生实验自动测试 | 14/14，无跳过 | prototype-test.log、windows-prototype.trx |
| P2 迁移与独占锁 | 真实 schema1 迁移/重开字段一致；第二进程占用时拒绝，正常释放与强制退出后可重开 | p2-process/probe.json |
| P3 恢复中断 | 4 个真实子进程检查点强制退出，启动均恢复完整旧集或新集；损坏日志保留材料并阻止业务 | p3-process/probe.json、P3-probe-rerun.log |
| P4 导入/删除中断 | 6 个导入点及 4 个删除点实际中断；真实 TXT 完整备份往返、来源与文件摘要正确 | p4-process/probe.json、P4-probe.log |
| P7 EPUB 兼容往返 | 11 个确定性支持/降级变体逐本执行会话/书摘/完整 ZIP 删除恢复/精确回跳；15 类拒绝输入由生产回归覆盖 | p7-compatibility/compatibility.json、P7-probe.log、windows-release-fixed.trx |
| P1 Windows 原生桌面探针 | 正常退出 0；真实反向拖选、Shift 跨页复制、焦点、窗口重排均通过 | native-p1-final/probe.json、native-p1-final-driver.json |

原生实验本机进程首屏 755.35ms，百万字加载/排版/离屏绘制 54.09ms，翻页 P95=2.80ms，重排 P95=6.79ms；1471 页索引最大心跳间隔 26.90ms，20 次开书第20次相对第6次托管保留增量 208 字节。数据仅代表本机实验和记录的负载、字体、缓存条件；P95 包含同线程导航与原生离屏绘制，不是物理屏幕端到端延迟，不将实验数值冒称正式阅读器压力测试。

## 真实 Windows 应用交互

使用 UIAutomation 操作实际 Release 窗口，正文选择通过 Win32 真实鼠标拖动与键盘输入完成。留存 43 张有效桌面截图（包含 P1 实验），截图索引在 summary.json，操作时间、进程和退出码见 actions.jsonl。原始应用日志未删减。

| 流程 | 实测与数据核对 |
|---|---|
| 原有手动业务 | 空概览、空保存校验、添加书籍、1～35 页/40 分钟记录、带收藏书摘、计划、逐级返回、收藏/计划/统计/概览联动、正常关闭。01～07 截图。 |
| TXT 导入 | Windows 原生文件选择器导入 UTF8 样本；再次导入显示 Duplicate，库中仍 1 本；电子书不提供手动页码记录入口。08～11 截图。 |
| 真实选择与书摘 | 鼠标拖选后 Ctrl+C 的原文与 QuoteOrigin 完全一致；Ctrl+S 与再点保存同一范围仍仅 1 个定位书摘。12～13、txt-clipboard.json、txt-after-restart.json。 |
| 重排/完成/重置 | 字号改为28，输入框右键不会翻页；实际改变窗口宽高，正文可读；标记完成后前后翻页不取消完成；确认重置后完成标记清空，书摘/历史保留。14～17、txt-after-restart.json。 |
| 会话/重启/回跳 | 正常关闭后重启，恢复 ch-0002/offset0 与字号28；菜单离开保存会话；书摘回跳精确高亮；全屏与离开后恢复普通窗口。18～19、23、37～40、txt-final.json。 |
| EPUB 图片 | 启动扫描收件区导入 large-image；中文、英文、组合字符/emoji与基本强调样式可读；2400×1800 图片等比独占页；字号22→28重排。24～28 截图。 |
| EPUB 续读/书摘 | 正常关闭重启仍回到同一图片亲和锚点，原文书摘在28字号精确高亮。29～31、epub-image-anchor.json、epub-closed-at-image.json。 |
| UI 完整备份/删除/恢复 | 真实保存对话框导出 ZIP，删除书后库空；真实打开对话框恢复，10 张业务表的行集合与导出前一致，受管文件与 ZIP 逐字节相同；自动备份含恢复前的空库；恢复后原文精确回跳。32～36、epub-before-export.json、epub-after-restore.json、epub-restored-files.json。 |

数据库验证均以只读 SQLite 连接执行：integrity_check=ok，foreign_key_check 无错误。没有通过直接写库伪造上述 UI 操作。受管文件完整往返与原始来源不变另外由各阶段探针核对。

## 发现的问题、修正与重验

1. 首轮 272 项中 230 通过、42 失败：41 项由 `ReaderBackupTestData` 将本地 DateTime 配上 UTC 零偏移触发，另1项因 Windows 无创建符号链接特权。已修正为本地时间构造 DateTimeOffset 后 ToUniversalTime，生产业务实现不变。原始失败日志/TRX 保留，最终工作区和独立源码复验均为 270/0/2。
2. 符号链接用例增加实际权限探测 Fact：只有 Windows ERROR_PRIVILEGE_NOT_HELD 才标记跳过，其他异常仍失败。原有另一个 Windows 直接 return 的用例也改为相同显式跳过，避免未执行记成通过；有权限的 Windows 或 Linux 仍运行原断言。两项实际跳过是 TxtImportTests.SymbolicSourceAndBrokenInboxLinkAreRejectedWithoutBlockingValidInboxBook 和 CompleteBackupTests.SymlinkInManagedContentOrDestinationIsRejected。
3. P2/P3/P4 开发探针锁文件未包含 P7 加入的 AngleSharp/VersOne.Epub，locked-mode 首轮还原拒绝。更新对应3份工具锁文件后锁定还原、构建和探针通过；未升级生产依赖。
4. 新写验收驱动的几处问题已修正：书摘显示含“☆”前缀、导出异步完成需等待、数据库行应比较集合而非无 ORDER BY 的自然顺序。实际 UI/数据库没有因此发生业务错误。保留失败日志和后续成功证据。
5. 原生实验驱动首轮虚拟键输入遗漏 Windows Shift 扫描码，跨页复制等待超时。补齐实际扫描码、扩展键标志与按键时序后 native-p1-final 全通过；native-p1 首轮失败保留，不据此改写业务实现或隐藏失败。
6. 三张主窗口最小化时捕获的黑图已作为本轮临时文件删除；actions.jsonl 保留无效捕获记录，37～40是恢复可见窗口后的补验。探针与截图函数现会检查窗口可见尺寸和屏幕位置。

## 保留边界

- 实际 Windows DPI 仅150%；100%/200% 与不同 DPI 的跨屏移动未实测，不能从 Linux 三倍率结果推断通过。现有阶段验收允许 Linux 通过、Windows 后补；本轮补齐当前本机环境，不宣称完成 P10 全平台发布。
- 两个需要操作系统符号链接权限的用例明确跳过；不为跑测试修改系统安全设置。ZIP 中符号链接恶意条目拒绝已由 Windows 正常回归覆盖。
- 真实商业/第三方 EPUB 资料未提供，覆盖0本。确定性变体不能说明市场兼容率；图片实机样本PNG，不外推其他格式均实测。DRM、固定版式、复杂 CSS、非UTF8 XML、SVG/公式/媒体等边界沿用 P7 协议。
- P8 AI Mock、P9 真实 API、P10 发布仍未实施；不将 P7 合并写成全部规划完成。旧正式 Word/PDF/源码交付没有更新。

后续复核重点是另两档实际 Windows 缩放、跨屏和用户自有 EPUB；上述保留项未发现已测功能的失败，不阻塞当前 P0～P7 合并。
