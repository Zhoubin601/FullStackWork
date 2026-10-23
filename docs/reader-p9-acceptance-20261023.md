# P9 真实 DeepSeek 联调验收

日期：2026-10-23。P8来源origin/feature/reader-ai-p8=4baef83；本地工作分支codex/reader-p9。用户先授权3次，随后明确“解除限制，你随便用”，追加调用不再受旧3次上限限制。临时Key仅由用户在应用设置页输入，两轮结束均在应用内删除，从未在聊天、代码或文件中接收。

## 结果

| 项目 | 结论与证据 |
|---|---|
| P9-01 官方配置/知情 | 已读取官方文档并记录日期、endpoint/Bearer/JSON/模型及价格依据；当前deepseek-flash、thinking=disabled。实际UI范围确认，用户应用内会话Key及上传知情，见reader-p9-provider-reference.md。 |
| P9-02 分析/解释 | 86字符当前页分析2355ms，输入451/输出88 token；10字符真实拖选解释1407ms，输入184/输出27 token；均不写书摘。real-analysis.json、real-explanation.json。 |
| P9-03 提取/定位/撤销 | 首轮2候选因原文/提示定位不匹配全拒绝，零新增，真实失败保留。中文JSON/提示词修订后，86字符提取2129ms，输入243/输出101 token，2候选全部逐字核验通过并自动保存；从书摘回到原文后复制选区与保存原句一致，撤销删除2条并保留不可变批次回执。real-extraction-refined.json、real-locate-undo.json。 |
| P9-04 范围与usage | 真实调用只发86/10字符自建短文；2169字符、全书15页的范围被600输入上限拦截，未发Provider请求、无新增。服务usage与费用区分，缺usage不作0。input-limit-ui.json及Mock序列化/范围回归。 |
| P9-05 故障/离线阅读 | 真实模式确认后250ms取消，无新增，仍可改字号；删除Key后缺凭据在HTTP前提示，正常退出。另用真实HttpClientHandler/实际TCP连接到拒绝连接的本地代理，得到Offline分类，正文仍可读取且正常提交1阅读会话，真实DeepSeek调用0。real-cancellation.json、real-cleanup.json、offline-probe-final.log、final-cleanup.json。 |

实际完成4次Provider响应（包含首轮失败的提取），另有1次真实模式取消尝试。4次已返回usage合计输入1331、输出343 token；取消没有usage，可能已计费，不能据此声称账单总额。无自动重试；额外请求均在用户解除次数限制之后主动触发。

## 本地修正及复现

默认模型改为当前官方deepseek-flash，保留旧配置可读/Mock可用，旧模型真实发送前明确提示更新；不静默替换用户保存的选择。服务显示实际token，保存重试保留usage而不重复HTTP。请求模型须与配置一致，预取消不发HTTP。

内层正文JSON保留中文；提取示例去掉可被误用的上下文占位值，要求逐字连续复制。验证器未放宽，只细分找不到原句与紧邻提示不匹配。首轮原响应按约定未留存，因此不能断定首次两条候选分别因何失败，也不把修订后成功说成唯一根因证明。

P8凭据扫描Windows失败修正为关闭SQLite后扫描全部持久文件，仍包含数据库。最终200文件白名单独立打包/解包/锁定还原/Release构建与全量回归：334总计，332通过、0失败、2符号链接权限跳过。新增7项回归；最终AiWorkflowTests25/25。证据clean-release-final.trx、source-sha256-final.json、summary-final.json。当前Code逐文件与最终独立测试包相同。

网络故障探针：`dotnet run --project docs/tools/ReaderP9NetworkProbe/ReaderP9NetworkProbe.csproj -c Release -- <新的隔离目录>`。只使用随机合成凭据和进程独立代理，不改系统网络、不用用户Key、不接触DeepSeek；这是实际TCP失败，不宣称拔网线或全系统断网实测。探针首轮枚举名称编译错误已更正，原失败日志保留。

## 边界与交付状态

在上述Windows和合成短文范围内，P9核心联调通过；不外推模型整体质量、任意书籍/所有网络故障表现。Windows符号链接2项权限跳过，100%/200%与跨屏、真实商业EPUB仍未补测。P10发布整理、新版正式报告和正式源码ZIP未开展。raw及既有output全部SHA256未变。

最终临时Key已删除，Mock已恢复，应用正常退出0；隔离库8条正常会话、0条书摘，integrity_check=ok、外键无错误。记录仅保留元数据，不留真实响应正文或Key。凭据格式扫描无命中；未读取用户Key，因此没有声称做过真实Key精确字节搜索。

当前变更保留在本地codex/reader-p9，未提交、未合并main、未推送；原有未提交文档及stash备份保留。本轮没有新的待确认事项；后续P10按另行范围推进。
