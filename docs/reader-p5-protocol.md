# P5 生命周期与接口补充

依据：reader-plan-v2 的 ReaderOpenIdentity、SessionCloseCommand、离开门禁与 P5-01～09。沿用 P2 的文档/布局/会话身份，库层无 Avalonia 依赖。

- `ReaderPhase`：Idle → Opening → Presenting → Active；首屏原生 Surface 完成绘制后在 UI Background 队列通知 `PagePresentedAsync(LayoutId)`，才调用 Begin。打开/取消/呈现失败没有 closed-session；Begin 写 HasStarted/LastOpened 并注册维护门禁。
- `IReaderLayoutService.BuildIndexAsync`：背景优先级协作分页，可取消；布局句柄失效后不发布结果。正文组装与字素边界索引在工作线程，Skia 排版与绘制在 UI 线程。最多两个布局，离开全部释放；现用 Reader 同时一个布局，20 次切书测试每次释放为零。
- `IReaderSessionService.GetForBookAsync`：P5 临时只读 closed-session 展示，P6 换统一历史。
- `ContinueAfterCloseFailureAsync`：动作门禁 → 数据租约 → SQL 查询提交事实。未提交取消冻结而继续原起点/原计时；已提交释放旧注册，当前位置/当前时间开启新实例/SessionId。
- `IReaderAlertService.ChooseCloseFailureAsync` 返回 Stay/Retry/ContinueReading；冻结关闭命令保留起止位置与时间，重试必须相等。已提交 SessionId 优先回放原结果，不重新写 Progress。正常关闭一次 SQL 事务保存 Session 和 Progress。失败保持阅读器和维护注册；只有成功确认/查证提交才释放。
- `NavigateToAsync` 与 Main 的 RequestNavigationAsync 串行所有运行期菜单/直接跳转/返回/切书/关窗。旧 void 仅初始化或 Main 受控提交段可调用；运行期直调用抛错，不静默绕过。相同单例目标只保留一节点；同书定位走 RequestInPlaceAsync，不重开会话。
- `ReaderPreferencesStorage` 只读写 data/reader-preferences.json；字体、字号、行距、边距、paper/light/dark 校验后耐久写入，不写应用主题。布局页号不进入进度，重排始终保留正文 ReaderPosition。
- `UseGlobalBusyForLoad=false` 仅 Reader 使用；IsLayoutBusy 独立于业务 IsBusy，不阻止正常离开；取消并等待打开、首屏 Begin、重排和分页后再释放资源。窗口/字号变更以取消源和布局修订号隔离旧回调。

会话原始毫秒采用 UTC tick 差截断至毫秒，负数置零且 ClockAnomaly=true；关闭本地日期按关闭时间自身 offset，闲置/系统睡眠时间不主动扣除。重试必须先校验 epoch/BookUid/DocumentKey，再查询 SessionId，旧提交不能绕过代际校验。
