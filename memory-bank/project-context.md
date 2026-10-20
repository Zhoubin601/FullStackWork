# 已确认的项目背景与约束

更新日期：2026-10-20。

- 项目：个人离线阅读与书摘管理；Avalonia/.NET8、MVVM、SQLite。Code放源码和测试，过程资料在docs，raw保持只读。
- 用户要求严格采用DailyPoetryA真实调用结构：ServiceCollection注册→BuildServiceProvider→IServiceProvider→定位器属性解析；服务/VM单例；ServiceLocator.Current从应用资源取得；三层导航和常量、MainViewModel菜单和返回栈、统一SetParameter、反射和按类型缓存ViewLocator。不得用壳VM、宿主或页面工厂替代。
- 参考基线为63类、10接口、9视图；课程最低45类、7接口、9视图。当前修复后应用66类、14接口、10业务页。
- 本轮用户已授权修复全部已发现缺陷并提供报告身份；身份已填入正式报告，不在此长期背景中重复保存。编辑加载/保存输入保护、首次成功读取门禁、新建ID回填、重试、计划/备份一致性已修复。
- 最新架构/功能复核：概览VM内查询已迁入IReadingRecordStorage；删除固定目标并防止旧完成事件弹走新页面，Loaded等待旧操作后加载最新参数；设置页数据操作期间阻止切页/关闭。核心DI、三层导航及主VM栈保持原结构。
- 最新Release正式测试83项全通过；源码v3全新解包锁定依赖还原、构建与同一83项通过。本轮旧版基线68项、删除和Loaded中间版76项仅为历史阶段证据。Windows桌面未实测，课程指导书未指定OS。
- 最终DLL独立架构探针71项通过；本轮真实Linux窗口完成新库业务闭环及最终版重启、编辑/删除/恢复，删除后四表全0，恢复后各1，绑定诊断0，证据见最新UI审查。
- 当前源码交付：output/20261020-阅记-源码-v3.zip（118文件109424字节）。output/20261020-阅记-项目报告-v1.pdf（19页）仍是前轮报告，未为本轮审查重做；最新结论读docs/architecture-flow-review-20261020.md。旧版ZIP和报告草稿保留。
- 继续时先读docs/architecture-flow-review-20261020.md和docs/architecture-strict-audit-20261020.md；此前失败证据不代表修复后的状态。本轮开始及推送前fetch确认HEAD与origin/main均20d9c67；用户随后明确要求“推送远端”，本轮成果纳入本次提交，目标origin/main；授权及核验方式见docs/git-sync.md。
