# 左上角返回按钮修复与验证

日期：2026-10-21。用户反馈左上角返回按钮点击无效，追问是否没有页面栈。

## 结论与实际原因

原来已有页面栈：MainViewModel.ContentStack持有当前模块历史，内容导航压栈、GoBack出栈，菜单切换清栈。缺陷在于原MainView无条件展示返回按钮，GoBackCommand也无CanExecute门槛；在菜单首页，栈只有一项，GoBack直接无操作，却仍给用户可返回的外观。

用户补充“就左上角有个返回按钮啊”，未给出具体详情/编辑失效链路。因此本轮不把所有详情返回都描述为原有功能故障；确认并修复无历史时的入口问题，同时实际验证原内容返回链路。

## 修改

- `Code/ReadJournal.Library/ViewModels/MainViewModel.cs`：新增从现有ContentStack派生的CanGoBack；命令CanExecute与实际执行均检查是否有上一页。集合变化时通知属性和命令状态，确保压栈/出栈/菜单清栈后同步更新。
- `Code/ReadJournal/Views/MainView.axaml`：返回按钮绑定CanGoBack可见性，仅有上一页时显示；提示为“返回当前模块的上一页”。
- `Code/ReadJournal.UnitTest/ArchitectureTests.cs`：新增真实MainView绑定回归，覆盖首页不可返回且不显示、编辑页显示且可执行、通过按钮实际绑定命令回到书库、菜单切换重置按钮状态。
- `Code/ReadJournal.UnitTest/NavigationRaceTests.cs`：原“返回与菜单不能重复弹确认”场景补上真实的栈底列表页，使测试在确有上一页的情况下触发返回确认。

不改变菜单清栈、内容压栈、主VM持栈、离开确认或三层导航机制。此返回为模块内返回，不是跨左侧菜单的浏览历史。

## 验证

1. 修复前先运行新增回归：失败，原因是首页GoBackCommand.CanExecute仍为true，证据`docs/evidence/back-navigation-20261021/before-fix.{log,trx}`。
2. 修复后重新构建并运行全套测试：**94通过、0失败、0跳过**，证据`docs/evidence/back-navigation-20261021/after-fix.{log,trx}`。未因本次修复把原93项历史结果冒充新数量。
3. Windows实际窗口，隔离数据目录`docs/evidence/back-navigation-20261021/ui-data`：
   - 概览、书库首页不显示返回入口：`root-after-fix.png`、`library-root.png`。
   - 添加书籍进入空编辑页，按钮出现；点击左上角返回，回到书库且入口隐藏：`editor-before-back.png`、`library-after-back.png`。
   - 在隔离库录入测试书籍，打开详情，点击左上角返回回到书库：`detail-open.png`、`library-after-detail-back.png`。
   - 书库→详情→编辑资料→返回详情→返回书库，验证多级栈：`nested-editor.png`、`detail-after-nested-back.png`及实际点击过程。
   - 切至统计首页不显示返回：`statistics-root.png`。
   - 自动输入工具的WM_CHAR数字输入未生效，实际应用正确提示“总页数必须为正整数”；改用真实键盘数字输入100后保存成功。中间截图仅保留为过程，不将输入工具问题认作应用缺陷。
4. 应用源码变更的git diff空白检查通过。测试进程正常关闭，已用默认用户数据目录重新启动修复后的Release应用，进程39232、窗口“阅记 · 阅读与书摘”、Responding为true（本轮启动时状态）。没有修改用户原有业务数据。

本次未重复运行历史71项独立探针，真实容器/导航/ViewLocator由全套测试中的ArchitectureTests覆盖；不宣称所有界面或所有业务重新人工验收。

## 来源、技能和文件边界

- 原始资料：`raw/期末项目与报告要求.docx`，前轮已读取；本轮直接依据当前源码、参考项目导航机制与`docs/plan.md`中已确认“菜单清栈、栈底返回不改变状态”的约束。
- 本地ui-ux-pro-max技能搜索仅采用All平台Disabled States的“清晰指示不可交互状态”建议；Mobile返回历史建议未用来更改桌面应用导航约定。不使用外部资料。
- 新增本记录、计划、测试日志/TRX、WindowsUI辅助脚本和截图，并补充来源、分析、问题和可复用进度。所有过程在docs，不生成正式output。
- 原始资料、参考源码、正式Word和既有源码ZIP均未改；已交付v5源码包不包含本次修复，后续若提交打包需另增版本，不能沿用“与当前Code逐字节一致”的旧结论。
- 本次已完成修复与验证，无阻塞。用户可在当前已重启窗口复核：进入书库→打开书籍详情/添加书籍后出现返回；模块首页不再出现无效按钮。
