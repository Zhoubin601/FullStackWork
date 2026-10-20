# 已确认的项目背景与约束

更新日期：2026-10-20。

- 项目：个人离线阅读与书摘管理；Avalonia/.NET8、MVVM、SQLite。Code放源码和测试，过程资料在docs，raw保持只读。
- 用户要求严格采用DailyPoetryA真实调用结构：ServiceCollection注册→BuildServiceProvider→IServiceProvider→定位器属性解析；服务/VM单例；ServiceLocator.Current从应用资源取得；三层导航和常量、MainViewModel菜单和返回栈、统一SetParameter、反射和按类型缓存ViewLocator。不得用壳VM、宿主或页面工厂替代。
- 参考基线为63类、10接口、9视图；课程最低45类、7接口、9视图。当前修复后应用66类、14接口、10业务页。
- 本轮用户已授权修复全部已发现缺陷并提供报告身份；身份已填入正式报告，不在此长期背景中重复保存。编辑加载/保存输入保护、首次成功读取门禁、新建ID回填、重试、计划/备份一致性已修复。
- Release正式测试68项全通过；新版ZIP全新NuGet还原构建测试同样68项通过，真实Linux窗口相关流程和首次/再次启动成功，绑定诊断0。Windows桌面未实测，报告如实保留边界，课程指导书未指定OS。
- 当前交付：output/20261020-阅记-源码-v2.zip（114文件102816字节），output/20261020-阅记-项目报告-v1.pdf（19页）。旧版ZIP和报告草稿保留。
- 继续时先读docs/repair-results-20261020.md和docs/delivery-checklist.md；修复前审查保留为历史证据，不能再当成当前未修复状态。本轮已获用户明确授权提交并推送至origin/main；同步记录见docs/git-sync.md。
