# 最终交付清单（Delivery Checklist）

## 1. 项目与当前交付状态

- **项目正式名称**：阅记原生电子书阅读器与 AI 阅读助手（ReadJournal）
- **项目简介**：基于 .NET 8、Avalonia 11.0.10、CommunityToolkit.Mvvm 8.2.2、Microsoft.Extensions.DependencyInjection 8.0.0 与 SQLite（`sqlite-net-pcl 1.8.116`，`user_version = 2`）开发的本地桌面电子书阅读器与 AI 阅读辅助应用。
- **当前状态**：已完成最终审查、版本标识清理、PDF 重新导出核验、源码包重建与独立构建测试，可直接提交课程作业。

---

## 2. 原始资料保护核验（`raw/`）

`raw/` 目录保持只读，未修改、未重命名、未移动、未覆盖：

| 原始资料路径 | 字节大小 | SHA256 | 状态 |
| --- | ---: | --- | --- |
| [`raw/期末项目与报告要求.docx`](../raw/期末项目与报告要求.docx) | `16,710` 字节 | `352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d` | 完好（只读） |
| [`raw/1-阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md`](../raw/1-阅记-原生电子书阅读器与AI阅读助手-总体规划-v1.md) | `21,529` 字节 | `62c53dcd213a9b645d2adc08c6f8aba7f5894fa011b6529a0dd3574464d9833d` | 完好（只读） |

---

## 3. 正式交付物核验（`output/提交材料/`）

正式交付目录仅保留当前有效的正式交付文件，文件名已去除 `v1`、`v2`、日期前缀等历史发布标识：

| 角色 | 文件路径 | 页数 / 文件数 | 字节大小 | SHA256 | 核验结论 |
| --- | --- | --- | ---: | --- | --- |
| **正式项目报告（课程规定提交件）** | [`output/提交材料/阅记-项目报告.pdf`](../output/提交材料/阅记-项目报告.pdf) | **27 页**（19 个目录项/书签/链接、14 表、10 图、7 段代码） | `2,199,879` 字节（约 2.10 MB） | `0605288c6a50ab1e5ff19fb276624b06154e954426c96e1d3a751de6d2ff6841` | **已存在并逐页核验通过**（全部 11 种字体子集嵌入，无空白页、无截断、无表格溢出） |
| **正式源码压缩包（课程规定提交件）** | [`output/提交材料/阅记-源码.zip`](../output/提交材料/阅记-源码.zip) | **208 个源码与资源文件** | `342,012` 字节（约 334.00 KB，`< 10MB`） | `118095ab068e02276fd341832fdb60b9fad254142b7c3e500617df2e6dba8432` | **完整有效**（与 [`Code/delivery-files.txt`](../Code/delivery-files.txt) 及 [`Code/`](../Code/) 逐字节一致，不含 `bin/`、`obj/`、`.vs/` 或敏感凭据） |
| **报告可编辑源文件（保留源文件）** | [`output/提交材料/阅记-项目报告.docx`](../output/提交材料/阅记-项目报告.docx) | **27 页** | `2,447,721` 字节（约 2.33 MB） | `7563abfa1941cedd8c336ad84eff4a04ccca97ae66027f4dbd690c25bfbca20c` | **仅作为可编辑源文件保留**，`officecli validate` 零错误，正文与表格内容与正式 PDF 100% 一致 |

---

## 4. 课程要求逐项对照（对照 `raw/期末项目与报告要求.docx`）

| 序号 | 课程原始要求 | 本项目实际完成情况 | 是否符合 |
| ---: | --- | --- | --- |
| 1 | 功能必须有实际意义、逻辑自洽 | 实现手动书籍与 TXT/EPUB 原生电子书统一管理、原生排版与稳定锚点续读、自动会话计时、书摘与精确原文回跳、DeepSeek AI 阅读辅助与原文逐字核验、阅读计划、混合统计及完整 ZIP 备份恢复，业务闭环自洽。 | 符合 |
| 2 | 页面数量不少于 `DailyPoetryA`（9 个视图） | `Code/ReadJournal/Views/` 共 **15 个 AXAML 视图文件**（含 **11 个业务页面视图** + `MainWindow`、`MainView`、`InitializationView`、`ReadingRecordEditView`），大于参考项目的 9 个视图。 | 符合 |
| 3 | 接口和类数量不少于参考项目的 70%（参考项目 63 类、10 接口；70% 门槛为 45 类、7 接口） | 生产工程 `ReadJournal` + `ReadJournal.Library`（合并 `partial`，不含测试、生成代码、`record`/`struct`/`enum`）共 **137 个类、41 个接口、134 个 C# 源文件**，远超 45 类 / 7 接口门槛。 | 符合 |
| 4 | 体现 8 项核心技术与架构思想 | 1) MVVM 分离；2) `ViewModelBase.SetParameter` 统一导航参数注入；3) 面向 `IService` 接口与构造注入；4) 基于 `ServiceCollection` / `IServiceProvider` 的 `ServiceLocator`；5) `Root`/`Menu`/`Content` 三层导航与 `MainViewModel` 返回栈；6) `ViewLocator` 反射创建与缓存；7) IO 服务化与异步协作；8) `ReadJournal`（UI）与 `ReadJournal.Library`（业务核心）双工程分层。 | 符合 |
| 5 | 提交项目源码压缩包：仅含项目源码和资源文件，不含编译结果，大小不超过 10MB | [`output/提交材料/阅记-源码.zip`](../output/提交材料/阅记-源码.zip) 含 **208 个文件**，大小 **342,012 字节（约 334.00 KB）**，不含 `bin/`、`obj/`、`.vs/` 等编译结果。 | 符合 |
| 6 | 提交项目报告：使用给定模板（封面 4 项字段 + 6 大部分 13 个二级小节），**最终必须以 PDF 格式提交报告** | [`output/提交材料/阅记-项目报告.pdf`](../output/提交材料/阅记-项目报告.pdf) 严格采用给定封面表格（项目名称、姓名、学号、班级）及全部 19 个模板章节标题（`项目背景与意义` + `1.`～`5.` 章及 `1.1`～`4.3` 节），共 **27 页**。 | 符合 |

---

## 5. 构建与测试核验记录

1. **当前源码与独立源码 ZIP 构建及单元测试（2026-11-01）**：
   - 分别在 [`Code/`](../Code/) 工作区与 [`output/提交材料/阅记-源码.zip`](../output/提交材料/阅记-源码.zip) 独立解包目录执行：
     - `dotnet restore ReadJournal.sln --locked-mode`（通过，0 错误）
     - `dotnet build ReadJournal.sln -c Release --no-restore`（通过，0 错误，仅 3 条已知 `NETSDK1206` 旧 `alpine-x64` RID 提示）
     - `dotnet test ReadJournal.sln -c Release --no-build --no-restore`
   - 测试结果：共 **353 项自动化测试，351 项通过、0 项失败、2 项跳过**。
   - 跳过项说明：`CompleteBackupTests.SymlinkInManagedContentOrDestinationIsRejected` 与 `TxtImportTests.SymbolicSourceAndBrokenInboxLinkAreRejectedWithoutBlockingValidInboxBook` 共 2 项因当前 Windows 普通账户未开启符号链接创建权限（`SeCreateSymbolicLinkPrivilege` / 开发者模式）而显式跳过，不计入通过数。
2. **专项架构、排版、性能与真实窗口验收记录（2026-10-24 ～ 2026-10-25）**：
   - 生产架构与离屏性能探针：153 项检查全部通过（覆盖 15 个 ViewModel 与 36 个服务接口，20 次反复切书后文档/布局/解码图片引用归零）。
   - 原生排版实验：14 / 14 通过。
   - EPUB 样本验证：11 个支持/降级变体、15 个异常拒绝变体全部通过。
   - 真实窗口与真实 DeepSeek API 验收：在 Windows 11 x64（144 DPI / 150% 缩放）下完成手动业务、TXT/EPUB 导入与续读、三面板折叠、字体搜索与缺失字体回退、真实 DeepSeek 解读/选区整段书摘/页内多候选核验/批次撤销、完整 ZIP 导出恢复（10 张业务表与 12 个受管文件 SHA256 一致）及 14 处进程中断恢复对账。

---

## 6. 尚存限制与未验证事项

- **DPI 与多屏边界**：已完成 Windows 11 x64（150% 缩放）实际窗口验收与离屏渲染采样；Windows 100%/200% 物理缩放及跨显示器动态 DPI 切换尚未做专门实机扩测。
- **符号链接权限用例**：2 项符号链接安全拒绝测试在无创建符号链接权限的 Windows 普通账户下自动跳过，需在开启系统开发者模式或管理员权限环境下执行实测。
- **EPUB 版式与平台范围**：当前原生阅读器面向无 DRM 的流式 TXT/EPUB（UTF-8 XML/XHTML），不支持 DRM、复杂外部 CSS、固定版式 EPUB、内嵌脚本/音视频或 PDF/OCR；尚未纳入商业出版物样本库统计市场兼容率，macOS / Linux 桌面实际窗口未作完整人工验收。
