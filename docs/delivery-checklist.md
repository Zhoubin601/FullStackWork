# 阅记当前交付清单与复现说明

## 2026-10-21 统一v1正式交付（最新，优先于以下历史）

- output/v1含23页项目报告v1.docx、同版PDF及源码v1.zip；包119文件114395字节，与当前Code逐字节一致，已含返回入口修复。
- 工作区94项通过；本次源码包独立解包还原/构建/94项通过并实际启动；当前程序集71项独立架构探针通过。报告目录、页码和23页渲染验证完成。
- 原output中Word v2/v3和源码v5已移至docs/report/release-v1/previous-deliveries，正式output仅v1。永久删除被自动审批拒绝，所以采用归档，不宣称删除旧文件成功。
- 完整元数据、过程和限制见release-v1-20261021.md及docs/report/release-v1/validation.json；原始资料、参考和用户业务数据保持。

## 2026-10-21 当前实际文件状态（优先于下文历史记录）

- 当前Code已包含返回入口修复，完整94项通过，Windows实际返回链已验证。
- output现有源码v5未包含本次4个文件变化，需新增版本后才能称为当前源码交付；原v5包大小和纯源码格式仍符合要求。
- output现有报告为用户已保存的v2/v3.docx，没有PDF；v3章节完整，测试正文仍为93项并引用v5。需保留微调、同步最新结果并导出正式PDF。
- 因此不能将下文“已交付”“Windows未实测”等历史描述当作当前完整状态。准确检查见`requirements-status-20261021.md`；本轮只做状态核对，未生成新正式文件。

更新：2026-10-21，P1修复复验后。本页记录当前源码交付；旧源码包、既有正式报告和报告草稿保留。

## 当前交付

- 源码：`output/20261021-阅记-源码-v4.zip`，119文件、113136字节；SHA256 `de47a514929f6e9ac9e0d6115b69f762dd968987f9bfec227c31de20331d51b7`。修复收藏跨恢复旧实体覆盖，增加统一忙碌离开/直接导航保护；新增10项永久回归。
- Release93项全通过，源码v4独立解包、锁定还原/构建后同样93项通过（使用现有NuGet缓存，不复制bin/obj）；架构探针71项通过。详细修复与实际UI证据见`docs/p1-fix-20261021.md`和`docs/evidence/p1-fix-20261021/`。
- 应用仍为66类、14接口、10业务页，保留原DI、单例、三层导航、主VM栈及反射ViewLocator。PDF保留为前轮v2/68项报告，本轮没有要求重做PDF；Windows桌面仍未实测。用户随后已授权本轮全部成果同步origin/main，具体记录见`docs/git-sync.md`。

## 上轮交付（v3历史记录）

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
