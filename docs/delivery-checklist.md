# 当前交付清单

更新：2026-10-26。此前v1/v2交付已被最终提交材料替代，过期输出已按用户授权清理。

| 文件 | 用途 | 核查 |
|---|---|---|
| output/提交材料/阅记-项目报告.pdf | 课程正式报告 | 28页；19目录项、跳转及书签核对；字体嵌入；章节符合指导书 |
| output/提交材料/阅记-源码.zip | 课程源码 | 208文件、342012字节；无编译产物；逐成员与Code一致 |
| output/提交材料/阅记-项目报告.docx | 可编辑备份 | 与PDF正文和表格内容对应；13表、10图 |

按现有指导书可直接提交PDF和ZIP。应用137类、41接口、15视图文件，对照参考63类、10接口、9视图文件达标；15包含窗口/容器，业务页面为11。

独立源码包2026-10-25已锁定还原、Release构建0错误，353项中351通过、0失败、2项因Windows符号链接权限跳过。最终包相对于独立测试包仅README/CHANGELOG文字不同。2026-10-26仅同步与清理，无运行逻辑变更、不重跑功能测试、不调用收费AI。

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
