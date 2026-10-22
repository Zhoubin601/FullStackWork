# P5 正式Reader与会话实施计划

用户最新授权P4推送后依次完成P5/P6/P7，各阶段验收并推送。P4已正常推送并回读69b783abab2dfa02984121fa878da2fd6c74d692；本阶段依据reader-plan-v2接口/总体规划/执行表P5-01～09，继续Linux验收，Windows保留，无API。

1. ReaderSessionService：首屏后Begin，注册Reader、HasStarted/LastOpened；SessionId幂等关闭、原始毫秒/偏移/本地关闭日期、负差异常；会话动作门禁先于数据租约和SQL门禁。CloseFailed保留冻结命令，重试查提交事实，继续阅读在已提交时开新会话、未提交时保留原会话。
2. 原三层导航增加NavigateToAsync，Main统一RequestNavigationAsync；所有运行期调用等待离开，void仅初始化或受控提交区间。同书定位不关闭，多书切换先关闭；复用单例View/VM和原栈，不重复节点。保存返回也核对当前页面并走统一门禁。
3. ReaderViewModel/UI原生Surface：首屏绘制通知才Begin；打开取消/失败零会话，独立IsLayoutBusy/IsAiBusy；左右键与按钮、目录、字体/背景偏好、全屏。后台布局取消及ParameterVersion/ReaderInstanceId/DocumentKey/epoch/LayoutId拒绝旧响应。
4. 字体/视口重排保存正文锚点，不把旧页号持久化。后台分页协作让出UI，离开先取消/等待工作、清空画面后释放布局/正文；缓存保持有界。大文本组装与字符索引移出UI线程，未达到性能目标不编造通过。
5. 详情临时展示自动会话，P6再统一历史；显式完成/重置上层编排按契约补充，P6增加完整用户入口。
6. P5-01～09逐项回归、注入起止时间/关闭失败/丢失确认/重试/继续、真实桌面翻页与重排/关窗/续读、20次切换与后台索引离开、独立源码验证。通过后提交并推送P5，再进入P6。

需要补充的接口将同步reader-p5-protocol.md：后台BuildIndexAsync、CloseFailed继续入口与临时会话查询、首屏呈现生命周期；不得以空实现或仅伪测试冒充正式Reader。
