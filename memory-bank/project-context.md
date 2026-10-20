# 已确认的项目背景与约束

更新日期：2026-10-20。

- 项目：个人离线阅读与书摘管理；Avalonia / .NET 8、MVVM、SQLite。
- Code 放源码和测试；过程文档放 Code 外的 docs；raw 原始资料保持不变。
- 用户要求严格采用老师 DailyPoetryA 的具体架构和调用方式，不可仅声称思想相同后另造实现。真实 IoC 链路为 ServiceCollection 注册 → BuildServiceProvider → IServiceProvider → 定位器属性解析，构造依赖由容器注入。
- 当前实施依据是 docs/plan.md v4。保留示例单例注册、ServiceLocator.Current、三层导航和常量、MainViewModel 菜单及返回栈、ViewLocator 反射和按类型缓存。不采用历史 v3 的 ShellViewModel、导航宿主接口或页面工厂。
- 参考基线经语法树复核为 63 类、10 接口、9 视图；最低分别为 45 类、7 接口，证据在 docs/reference-baseline.md 及 JSON 清单。
- 当前只完成规划，Code 尚未实施。报告姓名、学号、班级及截止日期资料未提供。
