# 阅记当前交付清单与复现说明

更新：2026-10-20，最新架构与功能贯通复核后。本页记录当前源码交付；源码v1/v2、既有正式报告和报告草稿保留。

## 当前交付

- 源码：`output/20261020-阅记-源码-v3.zip`，118文件、109424字节；SHA256 `089ca8f42b4777cb290cd0ee119214ac365b015d2e92ebc01a1d36a2faf6e8a0`。包含本轮架构边界、异步导航修复及15项新增永久回归；全部文件在白名单，无bin、obj、TestResults、运行库或数据库。
- 既有正式报告：`output/20261020-阅记-项目报告-v1.pdf`，19页、1046510字节，对应前轮v2源码和68项测试；本轮未要求重做PDF，新增83项及架构/功能结果见最新复核文档。SHA256 `2ef350f2e76dcd88c4afbb24429d7d096a6a697617e1b9ff5976327df9569034`。源文件为docs/report/report.tex。
- 实际应用：66类、14接口、10业务页面，另有初始化、主窗口、主容器、记录弹窗。课程最低为45类、7接口、9视图。
- Release83项全部通过，包含原68项及本轮15项；源码v3全新解包、使用本轮已填充的共享NuGet缓存还原后，同样83项通过。独立最终DLL架构探针另有71项全部通过。
- 最新修复及验证：`docs/architecture-flow-review-20261020.md`；严格对应表：`docs/architecture-strict-audit-20261020.md`；逐条测试：`docs/test-results.md`；本轮证据：`docs/evidence/architecture-flow-20261020/`及另外两组审查证据目录。前轮结果保留在`docs/repair-results-20261020.md`。

## 解压后复现

安装.NET 8 SDK，global.json固定8.0.424并允许同补丁带新补丁。在解包根目录执行：

```sh
dotnet restore ReadJournal.sln --locked-mode
dotnet build ReadJournal.sln -c Release --no-restore
dotnet test ReadJournal.sln -c Release --no-build --no-restore
dotnet run --project ReadJournal/ReadJournal.csproj -c Release --no-build
```

也可用Windows `powershell -ExecutionPolicy Bypass -File build.ps1` 或Linux `sh build.sh`。首次还原需要NuGet网络来源或完整包缓存，业务运行无需联网。默认库在用户LocalApplicationData/ReadJournal；隔离验证可设置READJOURNAL_DATA_DIR。包内README包含操作、数据和打包说明。

## 实际验证边界

- 当前为Debian13/linux-x64，SDK8.0.424、运行时8.0.30，真实Xvfb/X11窗口。本轮新隔离库和最终DLL完成录入、修改、删除、恢复及切页；删除后四表全0、恢复后各1，最终窗口绑定诊断0。完整结果见最新UI审查和汇总；前轮演示库3/4/2/3实体是历史证据。
- 固定SQLite原生依赖仍产生3条NETSDK1206旧Alpine RID提示，Linux x64实测正常，Alpine未实测。图形环境GLX回退软件渲染，不代表GPU硬件验证。
- Windows桌面尚未实测。指导书未指定操作系统；PDF明确记录平台边界，没有将Linux结果作为Windows通过证据。
- 原始资料和旧版交付未覆盖。用户已明确要求将本轮成果推送远端，同步范围与核验方式见docs/git-sync.md。
