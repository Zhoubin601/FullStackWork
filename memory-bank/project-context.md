# 已确认的项目背景与约束

更新日期：2026-10-20。

- 项目：个人离线阅读与书摘管理；Avalonia / .NET 8、MVVM、SQLite。
- Code 放源码和测试；过程文档放 Code 外的 docs；raw 原始资料保持不变。
- 用户要求严格采用老师 DailyPoetryA 的具体架构和调用方式，不可仅声称思想相同后另造实现。真实 IoC 链路为 ServiceCollection 注册 → BuildServiceProvider → IServiceProvider → 定位器属性解析，构造依赖由容器注入。
- 当前实施依据是 docs/plan.md v4。保留示例单例注册、ServiceLocator.Current、三层导航和常量、MainViewModel 菜单及返回栈、ViewLocator 反射和按类型缓存。不采用历史 v3 的 ShellViewModel、导航宿主接口或页面工厂。
- 参考基线经语法树复核为 63 类、10 接口、9 视图；最低分别为 45 类、7 接口，证据在 docs/reference-baseline.md 及 JSON 清单。
- 当前应用已实现十个业务页面，实际 65 类、14 接口；Release 40 测试全通过，Linux 真实 UI 和最终解压源码包的还原/构建/测试/首次再次启动均通过。报告姓名、学号、班级及截止日期仍未提供，Windows 桌面未实测。
- 用户现已明确授权按规划实际开发。云工作区已从 origin/main 提交 c8e3c71 恢复资料，完成指定的一处规划措辞修正及阶段 1 需求/设计记录。当前阶段 1 尚未通过：参考 Gitee 源码获取遭代理 HTTP 403，原 Windows 参考目录未附带；取得源码后先复核已有哈希、统计和核心架构，不能直接进入阶段 2。详见 docs/development-log.md 和 docs/stage-1-design.md。

- 参考源码已由用户推送到 daily-poetry-a 并同步提交 0dc2d02，历史阻塞已解除。正式源码包在 output/20261020-阅记-源码-v1.zip（93138字节，110文件）；报告过程稿在 docs/report。继续时先看 docs/delivery-checklist.md 和 docs/development-log.md，补身份/Windows实测并定稿报告，不重做前四阶段。
