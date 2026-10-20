# 阅记 · 阅读与书摘管理

个人离线阅读记录、书摘、阅读计划与统计应用，使用 .NET 8、Avalonia 和 SQLite。

## 构建和启动

安装 .NET 8 SDK（global.json 固定 8.0.424，允许同补丁带的新补丁），在本目录执行：

```sh
dotnet restore ReadJournal.sln --locked-mode
dotnet build ReadJournal.sln -c Release --no-restore
dotnet test ReadJournal.sln -c Release --no-build --no-restore
dotnet run --project ReadJournal/ReadJournal.csproj -c Release --no-build
```

Windows 可执行 `powershell -ExecutionPolicy Bypass -File build.ps1`，Linux 可执行 `sh build.sh`。首次还原需要 NuGet 网络来源或完整包缓存；业务运行无需联网。Linux 启动需具备桌面图形环境及 Avalonia 所需系统库。

## 使用和数据

从“我的书库”新增书籍，在详情中添加阅读记录、书摘与计划。第 10～20 页按闭区间计 11 页，重复阅读计入累计页数，当前进度取最大结束页。已完成只表示存在读至书末的有效记录。

默认数据保存在用户 LocalApplicationData 下的 ReadJournal 目录，不写源码目录。设置 `READJOURNAL_DATA_DIR` 可指定隔离目录；设置 `READJOURNAL_LOG_BINDINGS=1` 可输出框架诊断。设置页支持虚构演示数据、浅深主题及 JSON 备份恢复；恢复前自动备份当前业务数据，主题偏好独立保留。

本版修复编辑加载/保存期间的输入保护、编辑页重试、重复计划及状态变化导致备份不可恢复的问题。若旧备份中某本书存在多个完成计划，修改记录或总页数会使多个计划同时未完成时，应用拒绝整次操作并提示先删除多余计划，原数据保持不变。

## 验证和打包

本版在 Debian 13 / Linux x64 验证。Windows 桌面尚未实测。固定 SQLite 依赖的旧 Alpine RID 会产生 NETSDK1206 警告；未验证 Alpine。

`delivery-files.txt` 是显式源码白名单。重新打包用 `python pack.py <新的ZIP路径>`；脚本拒绝覆盖已有归档，并核对包内容和大小。源码包不包含 bin、obj、测试结果或运行数据库。
