# 阅记当前交付清单与复现说明

更新：2026-10-20，用户授权修复后。本页记录当前交付；旧版源码v1及报告草稿保留。

## 当前交付

- 源码：`output/20261020-阅记-源码-v2.zip`，114文件、102816字节；SHA256 `2f723f261c640fc59621ed5736ae86c7bad500da006fd04ad1f86e2760f76dd5`。新增计划一致性规则、编辑及计划永久回归、README均在白名单。无bin、obj、TestResults、运行库或数据库。
- 正式报告：`output/20261020-阅记-项目报告-v1.pdf`，19页、1046510字节，身份和所需章节齐全；逐页渲染检查缺字0、越界0。SHA256 `2ef350f2e76dcd88c4afbb24429d7d096a6a697617e1b9ff5976327df9569034`。源文件为docs/report/report.tex。
- 实际应用：66类、14接口、10业务页面，另有初始化、主窗口、主容器、记录弹窗。课程最低为45类、7接口、9视图。
- Release68项全部通过，包含原40项、编辑回归20项、计划回归8项；新版ZIP全新解包、空NuGet目录还原后同样68项通过，首次/再次启动通过。
- 完整修复说明：`docs/repair-results-20261020.md`；逐条测试：`docs/test-results.md`；本轮证据：`docs/evidence/fix-20261020/`。

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

- 当前为Debian13/linux-x64，SDK8.0.424、运行时8.0.30，真实Xvfb/X11窗口。四表实际导出恢复及重启保持3/4/2/3实体，十业务页绑定诊断0。
- 固定SQLite原生依赖仍产生3条NETSDK1206旧Alpine RID提示，Linux x64实测正常，Alpine未实测。图形环境GLX回退软件渲染，不代表GPU硬件验证。
- Windows桌面尚未实测。指导书未指定操作系统；PDF明确记录平台边界，没有将Linux结果作为Windows通过证据。
- 用户仍应复核报告封面身份与课程提交流程。原始资料和旧版交付未覆盖，用户已另行授权同步至远端main，提交与推送结果以docs/git-sync.md及本轮Git核验为准。
