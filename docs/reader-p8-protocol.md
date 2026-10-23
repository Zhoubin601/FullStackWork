# P8 AI 本地流程与批次协议

依据已确认reader-plan-v2的第6节、P8-01～10。基准main=cd3386b；本轮代码分支feature/reader-ai-p8。raw、output和冻结fixture不变，schema仍为2。

## 正文范围

AiScopeFactory只从IReaderDocumentService和当前布局提取当前页、选区或全书页范围。页范围先等待可取消的ResolveGlobalPages；未得到映射不创建HTTP请求。一次最多200页、2048个映射片段、硬上限60000 UTF16单元，默认输入上限24000，可在设置中下调。图片剔除为多个连续span，只有同章且端点相接的纯文本才合并；不同章/图片两侧保留边界。

AiScopeSnapshot复制调用者集合，摘要绑定ReaderContext、LayoutId、范围说明、逐span范围及文字；网络前后都核对活跃会话和DataEpoch，并逐span从受管文档重提取。正文是请求中的独立segments；请求不附带书籍数据库ID、BookUid或未选正文。模型只返回候选原句和解读，可提供紧邻前后文作消歧，不接受模型坐标。

原句仅在快照span内逐字匹配，首尾须为字符簇边界；零匹配、无法唯一消歧、跨图片/跨章拼接、空或超长候选拒绝。最多32候选，原句≤8000、感想≤4000、上下文提示各≤256。合法子集可提交，拒绝原因在面板汇总。原句一致不代表AI解读正确。

重排清空尚未提交的页号，取消仍在准备的快照；已提交快照不随翻页/重排改变。页面切换/返回/关闭取消AI令牌，VM版本与Context隔离旧面板；服务在返回和存储时再次核对会话与代际，整数Id重用也不接受旧响应。

## HTTP、Mock与凭据

默认UseMock=true，通过AiMockHandler走同一JSON消息序列化和结构化解析器，不进入真实HttpClient、不读Key、不上传正文。Mock明确显示模拟提示，不作为模型质量证据。

真实适配仅允许https://api.deepseek.com/chat/completions或/v1/chat/completions，标准HTTPS端口，无userinfo/query/fragment；模型限定deepseek-chat/deepseek-reasoner。关闭自动重定向，无自动重试，非流式JSON请求。响应逐块读取，硬限262144字节，必须单条完成且finish_reason=stop；无效/截断JSON、非法候选字段或超额响应拒绝。默认2048输出token、45秒超时，设置范围64～8192 token及1～120秒。

401/403、429、5xx、其他拒绝、离线、断流、超时、取消有独立错误码/中文提示；不保留服务错误正文、请求头或原始异常为inner exception。不打印正文/Key。费用无当前可靠依据，不编造数字，不自动重发可能已计费请求。

ISecretStore实现为SessionSecretStore，仅持有进程会话内存，不写明文文件；重启需重新输入。设置隐藏输入，保存/错误/离开均清空输入框，可删除Key。ai-settings.json仅保存有界非密钥配置，完整ZIP沿用业务实体白名单，不包含此配置或Key。此为规划允许的会话内回退方案，不声称已实现各系统保护存储持久化。真实模式必须显式确认正文上传并主动触发；P8未对真实endpoint执行调用，P9仍需核验官方当前接口与受控真实联调。

## 原子批次、回执与撤销

IQuoteStorage.SaveAiBatchAsync取得会话动作门禁→expectedEpoch操作租约→数据库门禁。HTTP期间不持这些租约。不能因为命令称Verified而信任它：存储再次验证原文、范围、BookUid/DocumentKey/Ready与当前会话。全部合法书摘、QuoteOrigin、AiGenerationBatch与BatchItem在同一事务提交，取消或故障整批回滚。

去重键沿用P6的BookUid+规范化起终位置，与手动选区共用唯一约束；存在项不覆盖感想/收藏。源标为AiGenerated，Quote.Page为空。全拒绝时不建批次；合法但全重复可建零新增摘要。

每个生成使用新BatchId，同BatchId重试读取原始InsertedQuoteIdsJson/计数，级联删除明细不改变回执；数据库触发器禁止改写回执。撤销后的重放返回WasAlreadyUndone，不重建书摘。网络生成成功而本地保存失败时，最多8个待保存命令保留在内存；重试只提交本地事务，成功/取消/离开后清理，重启不恢复未提交候选。提交成功但确认回执丢失也以同BatchId读取原结果。

Undo只需expectedEpoch，不要求仍在Reader。事务逐项检查Revision、完整内容摘要与收藏位，删除本批未变更且未收藏的新增项；已编辑/切换收藏过的项、已存在重复项均保留。内容摘要使用DateTime.Ticks以避免SQLite读取Kind变化；批次只改Status，原始回执保持。重复撤销幂等。阅读器提供最近新增批次撤销，书摘列表提供按来源批次撤销，退出Reader后仍能用；删除全部相关书摘或整本书后不提供无意义列表入口。

## 本轮边界

本轮Linux自动化、真实X11窗口与200文件独立源码验证；P8新增Windows实机尚未执行。P0～P7已有Windows150%结果不代替P8。无真实DeepSeek调用，无真实商业EPUB材料，无P9/P10和新版正式报告/源码ZIP交付；临时验证ZIP只在工作区外供复现验收。
