# 阅记（ReadJournal）— 原生电子书阅读器与 AI 阅读助手

## 项目简介

**阅记原生电子书阅读器与 AI 阅读助手**（简称“阅记 / ReadJournal”）是基于 **.NET 8**、**Avalonia 11.0.10**、**CommunityToolkit.Mvvm 8.2.2**、**Microsoft.Extensions.DependencyInjection 8.0.0** 与 **SQLite（`sqlite-net-pcl 1.8.116`，`user_version = 2`）** 开发的本地桌面电子书阅读器与个人阅读管理应用。

系统同时支持纸质书等手动书籍管理与无 DRM 的 TXT/EPUB 原生电子书阅读，提供章节目录、原生文本与图片排版、稳定正文锚点（`DocumentKey` + `ChapterKey` + `Utf16Offset` / `NonTextBlockKey`）续读、自动阅读会话计时、原生选区复制与书摘保存、精确原文回跳、基于 DeepSeek 的 AI 阅读解读与逐字核验书摘生成、阅读计划、混合阅读统计，以及包含数据库与受管电子书资源的完整 ZIP 备份恢复与异常中断恢复对账能力。

---

## 正式交付物（唯一正式提交入口）

课程原始要求见 [`raw/期末项目与报告要求.docx`](raw/期末项目与报告要求.docx)（需提交不大于 10MB 且不含编译结果的源码压缩包，以及按给定模板编制的 **PDF 格式**项目报告）。

正式交付物统一位于 [`output/提交材料/`](output/提交材料/)：

| 交付物类型 | 文件路径 | 规格与页数/文件数 | 字节大小 | SHA256 | 说明 |
| --- | --- | --- | ---: | --- | --- |
| **正式项目报告（PDF）** | [`output/提交材料/阅记-项目报告.pdf`](output/提交材料/阅记-项目报告.pdf) | **27 页**（19 个目录项/书签/链接、14 表、10 图、7 段代码） | `2,199,879` 字节（约 2.10 MB） | `0605288c6a50ab1e5ff19fb276624b06154e954426c96e1d3a751de6d2ff6841` | **课程要求唯一正式报告提交件** |
| **正式源码压缩包（ZIP）** | [`output/提交材料/阅记-源码.zip`](output/提交材料/阅记-源码.zip) | **208 个文件**（与 [`Code/delivery-files.txt`](Code/delivery-files.txt) 逐项一致） | `342,012` 字节（约 334.00 KB，`< 10MB`） | `118095ab068e02276fd341832fdb60b9fad254142b7c3e500617df2e6dba8432` | **课程要求正式源码提交件**（不含 `bin/`、`obj/` 等编译产物） |
| **报告可编辑源文件（DOCX）** | [`output/提交材料/阅记-项目报告.docx`](output/提交材料/阅记-项目报告.docx) | **27 页**（与正式 PDF 内容、版式、页码 1:1 对应） | `2,447,721` 字节（约 2.33 MB） | `7563abfa1941cedd8c336ad84eff4a04ccca97ae66027f4dbd690c25bfbca20c` | **仅作为可编辑源文件保留**，不替代 PDF 正式提交件 |

- **交付核对清单**：[`docs/delivery-checklist.md`](docs/delivery-checklist.md)
- **报告与历史归档索引**：[`docs/report/README.md`](docs/report/README.md)
- **最终交付审查报告**：[`docs/final-submission-audit.md`](docs/final-submission-audit.md)

---

## 技术栈与课程架构对应

| 维度 | 采用技术 / 实现概况 |
| --- | --- |
| 运行时与框架 | .NET 8（`global.json` 固定 SDK `8.0.424` 补丁带）、C# 12、Avalonia `11.0.10` |
| MVVM 与依赖注入 | `CommunityToolkit.Mvvm 8.2.2`、`Microsoft.Extensions.DependencyInjection 8.0.0` |
| 本地持久化 | SQLite（`sqlite-net-pcl 1.8.116`），Schema 版本 `user_version = 2`（10 张业务表 + `AppMetadata` 元数据表） |
| 电子书解析与排版 | `VersOne.Epub 3.3.6`、`AngleSharp 1.8.5`、自研原生 `ReaderSurface` 与 `AvaloniaReaderLayoutService`（无 WebView 依赖） |
| AI 阅读辅助 | `HttpClient` + `System.Text.Json` 对接 DeepSeek HTTPS 非流式接口，配合 `AiQuoteVerifier` 连续原文逐字核验 |
| 自动化测试 | `xUnit 2.5.3`、`Moq 4.20.70`、`Avalonia.Headless.XUnit 11.0.10` |
| 代码与视图规模 | 生产代码含 **137 个类、41 个接口、134 个 C# 源文件、15 个 AXAML 视图文件（含 11 个业务视图）**；满足并超过参考项目 `DailyPoetryA`（63 类、10 接口、9 视图，70% 门槛为 45 类、7 接口、9 视图） |
| 8 项核心架构思想 | 1) MVVM 分离；2) `ViewModelBase.SetParameter` 统一导航参数注入；3) 面向 `IService` 接口编程与构造注入；4) 基于 `ServiceCollection` / `IServiceProvider` 的 `ServiceLocator`；5) `Root`/`Menu`/`Content` 三层导航与 `MainViewModel` 返回栈；6) `ViewLocator` 反射创建与类型缓存；7) IO 服务化与异步协作；8) `ReadJournal`（UI）与 `ReadJournal.Library`（业务核心）双工程分层 |

---

## 主要功能

1. **阅读概览（Overview）**：汇总总藏书、在读与已读数量、手动累计页数、总阅读时长（手动记录分钟数 + 电子书自动会话时长）、进行中阅读计划与近期阅读活动。
2. **书库管理（Books）**：支持手动录入与编辑纸质书、多选导入 TXT/EPUB 电子书、扫描收件区目录、按来源 SHA256 去重、关键词搜索、阅读状态筛选及受管副本安全删除。
3. **原生电子书阅读器（Reader）**：提供章节目录跳转、键盘与按钮翻页、F11 全屏、本机字体搜索与混排预览、字号与纸张主题调节、目录/排版/AI 三面板独立收起、稳定正文锚点与图片锚点续读，以及首屏实际绘制确认后启动自动阅读会话。
4. **书摘与收藏（Quotes）**：支持手动录入书摘、阅读器原生选区 `Ctrl+C` 复制与 `Ctrl+S` 保存可定位书摘、相同原文位置去重、收藏筛选、编辑感想及一键精确回跳电子书原文位置。
5. **AI 阅读辅助（AI Assistant）**：支持当前页、当前选区或指定页范围解读与书摘生成；选区生成完整保留用户圈选原文并由 AI 生成整体解读，页内/页范围候选经 `AiQuoteVerifier` 逐字连续匹配、字符簇边界与上下文消歧核验后事务入库，支持本地保存重试与按批次安全撤销。
6. **阅读计划与统计（Plans & Statistics）**：制定阅读目标日期并自动联动书籍进度与完成状态；分别统计手动页码进度与混合阅读时长，电子书不伪造物理页码。
7. **设置与数据恢复（Settings & Backup）**：支持浅色/深色主题切换、DeepSeek 配置与仅驻留进程内存的会话 API Key 管理；提供包含 SQLite 快照与受管电子书文件的完整 ZIP 备份导出与恢复、旧版 JSON 兼容恢复，以及基于 `staging`/`trash` 与持久化阶段日志的异常中断恢复对账。

---

## 构建、运行与打包说明

应用源码位于 [`Code/`](Code/)（也可直接解压 [`output/提交材料/阅记-源码.zip`](output/提交材料/阅记-源码.zip) 独立构建运行）：

### 1. 还原、构建与测试

```powershell
cd Code
dotnet restore ReadJournal.sln --locked-mode
dotnet build ReadJournal.sln -c Release --no-restore
dotnet test ReadJournal.sln -c Release --no-build --no-restore
```

也可直接执行源码目录下的跨平台构建脚本：

- Windows PowerShell：`powershell -ExecutionPolicy Bypass -File Code/build.ps1`
- Linux / macOS：`bash Code/build.sh`

### 2. 启动桌面应用

```powershell
dotnet run --project Code/ReadJournal/ReadJournal.csproj -c Release
```

### 3. 重新生成正式源码压缩包

```powershell
python -X utf8 Code/pack.py output/提交材料/阅记-源码.zip
```

---

## 测试与验证状态

- **最新自动化回归（2026-11-01）**：针对工作区 `Code/` 与 [`output/提交材料/阅记-源码.zip`](output/提交材料/阅记-源码.zip) 独立解包目录分别执行 `--locked-mode` 还原、Release 构建与全量单元测试，**共 353 项测试：351 项通过、0 项失败、2 项 Windows 符号链接测试（`CompleteBackupTests.SymlinkInManagedContentOrDestinationIsRejected`、`TxtImportTests.SymbolicSourceAndBrokenInboxLinkAreRejectedWithoutBlockingValidInboxBook`）因当前 Windows 账号未开启创建符号链接权限而显式跳过（不计入通过数）**。
- **综合验收记录（2026-10-24 ～ 2026-10-25）**：153 项架构集成与离屏性能探针通过、14/14 原生排版实验通过、26 个 EPUB 样本（11 个支持/降级变体、15 个异常拒绝变体）通过、14 处进程中断恢复对账通过，并在 Windows 11 x64（144 DPI / 150% 缩放）完成真实窗口全链路与真实 DeepSeek API 联调验收。
- **已知边界说明**：Windows 100%/200% 缩放与跨显示器切换、管理员/开发者模式下的符号链接拦截实测、商业出版物复杂 EPUB 排版及非 Windows 桌面环境尚未扩大实测范围，报告与交付清单均按已验证事实如实陈述。

---

## 仓库目录结构

```text
raw/          课程原始要求与初始规划资料（只读，严禁修改）
output/       正式交付物（output/提交材料/ 下存放正式 PDF 报告、正式源码 ZIP 及可编辑 Word 源文件）
Code/         阅记（ReadJournal）完整项目源码、构建脚本与依赖锁文件
docs/         过程文档、交付清单、审查报告、测试与验收证据归档
memory-bank/  项目长期背景与已确认技术口径
daily-poetry-a/ 课程参考项目（DailyPoetryA）只读对照副本
```
