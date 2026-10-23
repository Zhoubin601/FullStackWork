# P9 DeepSeek 官方配置核验

查询日期：2026-10-23（Asia/Shanghai）。用户已同意使用官方外部资料；尚未执行真实内容 API。

| 来源名称 | 链接 | 核验用途 |
|---|---|---|
| Your First API Call | https://api-docs.deepseek.com/ | 官方地址、Bearer认证和当前模型 |
| Chat Completions API | https://api-docs.deepseek.com/api/create-chat-completion/ | POST /chat/completions、模型、thinking、JSON响应与usage |
| Models & Pricing | https://api-docs.deepseek.com/quick_start/pricing/ | 当前模型、计价、限制 |

当前官方列出 deepseek-flash（当前映射 DeepSeek-V4.1-Flash-1017）和 deepseek-v4-pro。原 P8 的 deepseek-chat/deepseek-reasoner 不在当前官方可选模型列表，因此不能以 P8 的配置直接开始真实联调，也不能凭 Mock 成功推断旧模型可用。本轮默认更新为 deepseek-flash，真实请求显式 thinking.type=disabled；不默默改写用户已保存的旧模型。

接口沿用 https://api.deepseek.com/chat/completions，Authorization: Bearer，stream=false，response_format.type=json_object；提示词仍须明确要求 JSON，输出继续经本地schema/原文核验。官方列出最大输出393216 token，应用仍使用自己的64～8192上限；本轮短文联调建议1024输出token、600输入UTF16单元。

按查询页，Flash每百万token美元价：输入缓存命中峰时0.006/谷时0.003，未命中峰时0.30/谷时0.15，输出峰时1.20/谷时0.60。峰时为工作日UTC 01～04与06～10（中国法定节假日除外），其他为谷时。价格会变；本轮不自动算账单，不以字符数冒充token或缓存命中。实际usage与估算区分，最终以供应商账单为准。

JSON guide页面两次读取超时；Chat Completions API正文已直接确认json_object要求，不引用未读到的guide内容。没有使用第三方搜索摘要作为配置事实。
