# P5 正式阅读器与会话验收（Linux）

依据：output/reader-plan-v2 的实施规划/接口/执行表 P5-01～09；用户授权 P4 后连续 P5/P6/P7，每阶段验收后推送。P4 上一阶段真实远端：69b783abab2dfa02984121fa878da2fd6c74d692。

| 用例 | 结果与可复核证据 |
|---|---|
| P5-01 | 原三层导航保留；Reader 为单例内容页，所有菜单/返回/内容/切书/关窗均等待 Main 门禁。ArchitectureTests 与 ReaderLifecycleTests 实际 DI/路由覆盖，ui-01/02、03、11 展示详情进入/返回。旧 void 运行期直接调用明确拒绝。 |
| P5-02 | ReaderLifecycleTests 真实导入+正式布局：首屏回调前零会话且 HasStarted=false，取消后不产生记录；无效书打开失败零记录。真实桌面首次正文呈现后 HasStarted=true、closed-session=0。 |
| P5-03 | ReaderSessionTests 注入 1/12345/86401234/-100ms，精确毫秒、跨天、+08→-04 偏移、关闭当地日期及负差异常通过；无闲置/睡眠扣减。 |
| P5-04 | 同 SessionId 并发/重复关闭、事务确认丢失先查原提交；第二会话向前保存偏移10后重放旧会话不回写300，epoch变更后拒绝旧重放。 |
| P5-05 | before-write/in-transaction/after-commit 三点故障；回滚和单一提交事实正确。VM 留在 CloseFailed 且保留正文，重试使用冻结命令；继续按未提交保留原时钟、已提交新 SessionId/新起点。失败期间保持维护注册。 |
| P5-06 | 正式布局向后两页、向前一页，字号/窗口/DPI重排保留 ReaderPosition，关闭再打开相等。真实桌面保存正文偏移442，28字号重启后保持，ui-verification.json逐字段断言两个会话首尾相接。 |
| P5-07 | 实际容器的详情→Reader→同书定位→切书→返回详情测试；单例只有一个Reader节点，同书SessionId不变、切书先提交旧记录，不串BookId。 |
| P5-08 | ui-01 原生TXT正文、左右键、F11实际全屏、字号28应用及重启续读；reader-preferences独立JSON，主题文件未写。两次正常关窗exit0，默认目录存在性保持false。 |
| P5-09 | 正式布局20次双书切换，分页期间立即离开每次10秒内完成，每次活动布局1→0，无旧呈现回调开新会话；取消源/修订号隔离旧重排/索引，释放画面后释放布局/文档。 |

最终测试以 `p5-acceptance.trx` 及p5-clean-source-accepted.trx 为准；中间 p5-initial 仍为旧同步导航测试，不能替代最终结果。原P4的217项保留，新增会话/生命周期和路由测试17项，总计234。白名单源码包、恢复/测试日志分别见 source-package-accepted.json、clean-restore-accepted.log、test-clean-source-accepted.log；不作为新的正式交付包。

真实桌面证据位于 ui-01/：03初始正文；04翻页；05、06记录驱动输入未等焦点导致228无效字号的真实过程（产品正常拒绝，未保存）；08选中后09输入28，10正确重排；11返回与自动记录；14重启续读。禁止以05的失败输入截图作为字号验收，具体事实见actions.json、process.json及ui-verification.json。软件侧后续增加 Closing 状态，冻结关闭期间的翻页/重排；最终源码重验同样的会话/重排链路。

已知范围：Linux .NET8/Avalonia11；Windows按用户要求保留未测。独立源码首次构建SQLite旧Alpine RID的NETSDK1206警告保留，编译无错误。桌面会话总时长会包含操作/取证间隔，准确值从真实UTC差计算，未伪称不足一分钟。P6统一历史/统计图/选区书摘与P7 EPUB尚待后续各自阶段；无外部内容API调用。raw、output、参考工程、冻结fixtures不改。

中间路由测试首次把来源放在应用数据根目录（正确被来源规则拒绝），改为外部来源；第二次传入空范围（正确被契约拒绝），改为非空范围。两次失败保留在历史TRX；最终p5-acceptance验证完整实际路由，不以这些错误fixture绕过验证。
