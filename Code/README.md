# 阅记 · 原生电子书阅读与书摘管理

离线桌面阅读应用，采用 .NET 8、Avalonia 11、CommunityToolkit.Mvvm 和 SQLite，保留UI/Library/UnitTest三工程、真实DI容器、ServiceLocator、ViewLocator与根/菜单/内容三层导航。

当前源码为2026-10-23的阅读器与AI扩展交付，功能代码基线9f6ae53，P10完成本机Windows150%范围验收。用户明确跳过其他显示缩放补测，100%/200%及跨屏仍未验证；不将其认作支持范围。旧课程报告与旧源码ZIP属于历史交付，不代表本版扩展。

## 构建与启动

需要 .NET SDK 8.0.424（global.json允许同补丁带更新），在本目录执行：

```sh
dotnet restore ReadJournal.sln --locked-mode
dotnet build ReadJournal.sln -c Release --no-restore
dotnet test ReadJournal.sln -c Release --no-build --no-restore
dotnet run --project ReadJournal/ReadJournal.csproj -c Release --no-build
```

Windows可用 build.ps1，Linux可用build.sh。首次还原需要NuGet来源或完整缓存；Linux运行另需图形桌面和Avalonia原生依赖。业务运行不需联网，只有用户主动确认的真实AI操作联网。离线Mock无需Key。旧SQLite原生包可能产生NETSDK1206旧Alpine RID提示；未据此声称支持Alpine或macOS。

## 功能与使用

- 手动书籍：书库新增，按闭区间记录真实页码与分钟数，支持书摘、收藏、计划、统计。
- TXT/EPUB：通过文件选择器多选导入，或将文件放到数据目录的books本层后扫描。复制到受管库，不移动或修改来源文件。TXT支持严格UTF-8/BOM和显式GB18030；EPUB支持无DRM文字流式内容、目录、基本样式及受检本地图片。
- 阅读器：目录跳转、左右键/PageUp/PageDown、F11全屏、字体/字号/配色。文字位置独立于动态页号，窗口或字号变化后按正文锚点恢复。拖选正文，Ctrl+C复制、Ctrl+S保存可精确回跳的书摘；图片不当作文字引用。
- 会话：首屏呈现后开始计时，正常返回/菜单/切书/关窗时幂等保存会话与当前位置。时长是关闭减打开，不扣闲置。强制结束进程不会补造会话；位置可能回到最后一次成功提交。
- 计划与统计：手动记录保留页数；电子书只累计真实时长和正文位置百分比，不虚构物理页数。读完标记与回看进度分开。重置保留历史/书摘，遇到多个历史计划将阻止整次重置并提示先整理。
- AI：当前页、选区、全书页范围分析；解释选区；通过逐字核验的候选自动保存，提供批次撤销。分析/解释只显示结果；撤销保留已修改、已收藏及原已存在的书摘。AI解读并不因原句通过核验而保证正确。

## AI设置与数据保护

默认Mock，真实模式需在设置页取消Mock、确认上传知情、保存配置，并在隐藏输入框保存会话Key；每次在阅读器再确认所选范围和可能费用。Key仅在进程内存，保存/错误/离开时输入框清空；退出后必须重新输入，不写普通配置、日志或备份。可随时删除。不要在聊天、源码或文件中保存Key。

2026-10-23官方配置依据使用 https://api.deepseek.com/chat/completions ，默认deepseek-flash，可选deepseek-v4-pro；非流式JSON，显式关闭thinking。旧deepseek-chat/deepseek-reasoner配置仍可读/Mock可用，真实发送前提示更新模型，不静默替换选择。模型与价格可能变化，日后使用前核验官方说明。

默认输入24000 UTF16单元、输出2048 token、超时45秒；输入硬上限60000 UTF16单元，页范围最多200页/2048片段。输入上限按字符单元计算，与token或真实费用不同。显示供应商返回的usage；未提供则显示未提供，金额以供应商账单为准。超时/取消/错误不自动重发；本地保存重试不重新请求模型。

## 数据目录、备份与恢复

默认数据在当前用户LocalApplicationData的ReadJournal目录，源码目录不存业务数据；READJOURNAL_DATA_DIR可指定隔离目录。books本层是收件区，books/managed为受管文件；不要手改canonical文件。相同文件去重；保留来源的已删除书不会被扫描自动重建，可经文件选择器主动重导入。

设置页导出完整ZIP，包含业务数据、电子书原件、规范化正文和图片资源；旧JSON仅用于恢复手动书籍。恢复先完整校验并生成当前库自动备份，再整体替换业务数据与受管文件。本机主题、AI配置和会话Key不随业务恢复替换。请备份ZIP，不要仅复制正在写入的SQLite文件。

同一数据目录只能由一个实例写入；崩溃遗留.lock文件本身不表示仍被占用。启动会恢复导入/删除/整体恢复操作日志并对账。遇到RecoveryRequired、损坏日志或Missing时保留operations/staging/trash/backups等材料；补回文件，或明确删除损坏条目后再从完整备份恢复。不得把缺文件库当作完整备份。来源/收件文件不随删除或恢复被移走。

## 验证与已知边界

P9最终独立源码334项：332通过、0失败、2项Windows符号链接权限跳过。真实短文分析/解释成功；提取首轮全拒绝保留，提示词修订后2条原文核验/保存/定位/撤销通过。P10工作区和独立源码各334项：332通过、0失败、2项符号链接权限跳过；生产容器/视图/路由探针152项、原生实验14项通过，旧库迁移和14处实际进程中断恢复通过。新库及独立构建应用的迁移库实际窗口闭环已核对；详见仓库docs/reader-p10-acceptance-20261023.md及对应evidence。

已实测Windows150%缩放。100%/200%与跨屏按用户本轮决定保留未验证，不能用离屏渲染模拟值代替。EPUB主要以确定性自建样本验收，未新增真实商业书籍样本，不宣称市场兼容率。复杂CSS/固定版式/DRM、非UTF-8 XML、SVG/公式/音视频暂不支持或明确降级。未实测的平台、字体和图像格式不视为全面支持。

## 源码打包

`python pack.py <新ZIP路径>`按delivery-files.txt显式白名单打包，拒绝覆盖已有ZIP，核对全部文件内容与10,000,000字节上限。不要加入bin/obj、运行库/数据库、Key、大样本或验收证据。依赖及许可证见DEPENDENCIES.md和THIRD-PARTY-NOTICES.md。P10未完全验收的候选包只放仓库docs，正式output不放测试包。
