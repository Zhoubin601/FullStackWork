# 阅记交付清单与复现说明

日期：2026-10-20。应用已实现并通过 Linux 桌面实测；**全部课程验收尚未完成：Windows 桌面未实测，报告姓名/学号/班级未提供。** 不把这些项目记为通过。

## 已可交付

- 源码：`output/20261020-阅记-源码-v1.zip`，93,138 字节、110 个显式白名单文件，SHA256 `ae11c13d96614e8b127a0db2e5735fcbbf1a002bc9710e2064068f57c411dee4`。
- 源码包根目录直接含 ReadJournal.sln、ReadJournal、ReadJournal.Library、ReadJournal.UnitTest、固定 SDK/依赖锁文件、构建和打包脚本。不含 bin、obj、TestResults、.git、运行库或资料目录；无外部素材依赖。
- 报告过程稿：`docs/report/report.tex` 与 `docs/report/20261020-阅记-报告草稿-v1.pdf`（18页，已渲染检查）（待身份信息和 Windows 验收）；按原模板完整章节编写，不进入正式 output。PDF 检查结果另见 development-log。
- 实际数量：65 个手写类、14 个接口、10 个业务页面；另有主窗口、主容器、初始化和阅读记录弹窗。详见 application-type-inventory.md / .json。
- 自动化：Release 40 用例全部通过，最终解压包同样 40 用例通过。真实窗口另行验证；最终十页遍历绑定诊断 0。
- 真实截图和构建日志：`docs/evidence/`。测试逐条清单：`docs/test-results.md`。

## 解压后的构建与启动

安装 .NET 8 SDK（global.json 固定 8.0.424，允许同补丁带的较新补丁）。在压缩包解压根目录执行：

```sh
dotnet restore ReadJournal.sln --locked-mode
dotnet build ReadJournal.sln -c Release --no-restore
dotnet test ReadJournal.sln -c Release --no-build --no-restore
dotnet run --project ReadJournal/ReadJournal.csproj -c Release --no-build
```

也可在 Windows 使用 `powershell -ExecutionPolicy Bypass -File build.ps1`（或 PowerShell 7 `pwsh -File build.ps1`），Linux 使用 `sh build.sh`。依赖初次还原需要 NuGet 网络来源或已准备的包缓存。正常业务运行不联网。

默认业务库位于用户 LocalApplicationData 下的 ReadJournal 目录；主题单独保存。隔离验证时可设置 `READJOURNAL_DATA_DIR` 指向专用运行目录；未设置则不写源码目录。`READJOURNAL_LOG_BINDINGS=1` 可将框架诊断输出到终端，不自动往源码目录写日志。

首次使用可直接在书库新增书籍，也可通过“设置与数据 → 载入虚构演示数据”试用；演示只允许空业务库。记录采用实际闭区间页码，10～20 计 11 页；累计含重读，当前进度不累加。恢复备份前自动备份当前业务库，主题保留。

重新打包：在解压根目录执行 `python pack.py <新版本输出路径.zip>`，脚本只复制 delivery-files.txt 的显式条目到临时目录后压缩并复核哈希/大小，拒绝覆盖已有归档。

## 实际验证边界

- 实测平台：Debian 13 / linux-x64，SDK 8.0.424、运行时 8.0.30。Xvfb 是真实 X11 显示服务器；截图来自实际桌面窗口，不是 Headless 输出。
- 最终包解压到全新临时目录，使用新的 NUGET_PACKAGES 和 NUGET_HTTP_CACHE_PATH，restore --locked-mode --no-cache、Release build、test 均退出 0；实际首次/再次启动成功。此次还原使用网络。
- 固定 sqlite-net-pcl 1.8.116 依赖在完整构建时产生 3 条 NETSDK1206（旧 Alpine RID）提示；没有升级或隐藏，Linux x64 已运行验证，Alpine 未验。
- 图形环境无会话管理器，GLX llvmpipe 被框架拒绝后使用软件渲染；应用继续完成实际操作。硬件 GPU 加速未验。
- Windows 桌面、提交者身份信息和最终带完整封面的 PDF 仍待补齐。当前 PDF 只作为可复核过程稿，不作为已完成课程验收的报告。
