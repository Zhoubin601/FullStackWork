# 当前交付清单

更新：2026-11-01（远端提交前核对）。保留当前目录状态，报告Word已统一命名，PDF已从提交目录移除。

| 文件 | 用途 | 核查 |
|---|---|---|
| output/提交材料/阅记-项目报告.docx | 当前可编辑报告 | 内容摘要与已验证的27页v2一致；14表、10图、7段代码 |
| output/提交材料/阅记-源码.zip | 课程源码 | 208文件、342012字节；无编译产物；逐成员与Code一致 |

当前PDF文件未保留，不将历史导出的PDF说成当前目录交付物。课程指导书要求最终以PDF和源码ZIP提交；本次Git同步依用户“把现在的内容提交至远端”保留当前文件状态，不恢复已移除的PDF或版本文件。

应用137类、41接口、15视图文件，对照参考63类、10接口、9视图达标；15包含窗口/容器，业务页面为11。独立源码包及2026-11-01主线自动化回归均为351通过、0失败、2项Windows符号链接权限跳过，本次仅同步文件，不重跑相同应用测试。

报告修复记录：20261101-report-repair-results-v2.md；27页原生Word导出与逐页验证：report/repair-20261101-v2/validation.json、visual-review.json。历史记录中的带日期版本路径反映生成时状态，当前文件以本清单为准。原始资料、源码和源码ZIP未改。

## 构建和运行

.NET SDK 8.0.424，或global.json允许的同补丁带更新。在Code或源码解包根目录运行：

```sh
dotnet restore ReadJournal.sln --locked-mode
dotnet build ReadJournal.sln -c Release --no-restore
dotnet test ReadJournal.sln -c Release --no-build --no-restore
dotnet run --project ReadJournal/ReadJournal.csproj -c Release --no-build
```

首次还原需NuGet来源或完整缓存。Windows 11 x64/150%已有实测；其他DPI、跨屏、商业EPUB及未实测平台不计入通过范围。2项权限跳过及3条旧Alpine RID构建警告如实保留。

本人交前核对封面身份及平台其他命名要求。详情：`submission-readiness-20261025.md`；复检证据：`report/readiness-20261025/`；清理清单：`cleanup-20261026.json`。raw保持原样。
