# API 接入 / API connections

核对日期：**2026-09-14**。服务地址与兼容行为以各服务商官方文档为依据；模型列表由软件实时读取服务商目录，不把文档中的示例型号自动设为你的默认模型。

Verified **September 14, 2026** against provider documentation. MewuAI loads model lists from the service; examples below are not automatically selected as your model.

## 设置连接 / Set up a connection

1. 打开 **设置 → AI → API → 添加连接**，按名称搜索服务商。 / Open **Settings → AI → API → Add connection** and search for a service.
2. 填写该服务商、对应地区的 API Key，刷新模型列表并选择模型；也可直接输入模型 ID。 / Enter the API key for that service and region, refresh models, then choose or enter a model ID.
3. 点击测试连接。确认后保存设置；需要切换默认连接时，使用该连接的更多操作菜单。 / Test the connection and save settings. Use the connection's menu to explicitly change the default.

同一家服务商的国内、国际或不同区域密钥未必通用。模型目录可能列出平台支持的全部模型，实际调用还取决于账户权限、余额及区域。加载失败时保留当前模型和手输入口。 / Keys may differ across regions. A catalog can include all platform models; access still depends on your account, balance and region. A failed refresh preserves your selected model and manual input.

## 国内服务 / China services

| 服务 / Service | 预填地址 / Preset base URL | 官方文档 / Official documentation |
| --- | --- | --- |
| MiniMax 国内 | `https://api.minimax.cn/v1` | [OpenAI-compatible API](https://platform.minimax.cn/docs/api-reference/text-chat-openai) |
| 火山方舟 / Volcengine Ark | `https://ark.cn-beijing.volces.com/api/v3` | [模型目录 / Models](https://www.volcengine.com/docs/82379/1330310) |
| 阿里百炼 / Alibaba Cloud Bailian | `https://dashscope.aliyuncs.com/compatible-mode/v1` | [兼容接口 / Compatibility](https://help.aliyun.com/zh/model-studio/compatibility-of-openai-with-dashscope), [模型目录 / Models](https://help.aliyun.com/zh/model-studio/list-models) |
| DeepSeek | `https://api.deepseek.com/v1` | [API](https://api-docs.deepseek.com/), [更新 / Updates](https://api-docs.deepseek.com/updates/) |
| Kimi 国内 / Kimi China | `https://api.moonshot.cn/v1` | [模型与接入 / Models](https://platform.kimi.com/docs/models), [目录 / Catalog](https://platform.kimi.com/docs/api/list-models) |
| 智谱 GLM / Zhipu GLM | `https://open.bigmodel.cn/api/paas/v4` | [模型 / Models](https://docs.bigmodel.cn/cn/guide/start/model-overview) |
| 腾讯 TokenHub / Tencent TokenHub | `https://tokenhub.tencentmaas.com/v1` | [API 接入 / API access](https://cloud.tencent.com/document/product/1823/130078), [模型目录 / Catalog](https://cloud.tencent.com/document/product/1823/130079) |
| 百度千帆 / Baidu Qianfan | `https://qianfan.baidubce.com/v2` | [模型目录 / Models](https://cloud.baidu.com/doc/qianfan-api/s/Dmba8k71y) |
| 硅基流动 / SiliconFlow | `https://api.siliconflow.cn/v1` | [模型目录 / Models](https://api-docs.siliconflow.cn/docs/api/models-get) |

已有 MiniMax `api.minimaxi.com` 连接继续保留原地址。百炼还支持在高级设置中填写官方提供的工作空间专属兼容地址；模型目录会按其官方路径读取。 / Existing MiniMax connections retain `api.minimaxi.com`. For Bailian, you can enter an official workspace-specific compatible endpoint in Advanced settings; the catalog uses its corresponding official route.

## 国际服务 / Global services

| 服务 / Service | 预填地址 / Preset base URL | 官方文档 / Official documentation |
| --- | --- | --- |
| OpenAI | `https://api.openai.com/v1` | [模型 / Models](https://developers.openai.com/api/docs/models), [模型列表 / List models](https://developers.openai.com/api/reference/resources/models/methods/list) |
| Anthropic Claude | `https://api.anthropic.com/v1` | [OpenAI SDK compatibility](https://platform.claude.com/docs/en/cli-sdks-libraries/libraries/openai-sdk), [Models](https://platform.claude.com/docs/en/api/models/list) |
| Google Gemini | `https://generativelanguage.googleapis.com/v1beta/openai` | [OpenAI compatibility](https://ai.google.dev/gemini-api/docs/openai) |
| xAI Grok | `https://api.x.ai/v1` | [Chat Completions](https://docs.x.ai/developers/model-capabilities/legacy/chat-completions), [Grok 4.6](https://docs.x.ai/developers/models/grok-4.6) |
| OpenRouter | `https://openrouter.ai/api/v1` | [Models](https://openrouter.ai/docs/api/api-reference/models/list-all-models-and-their-properties) |
| Groq | `https://api.groq.com/openai/v1` | [Compatibility](https://console.groq.com/docs/openai), [Models](https://console.groq.com/docs/models) |
| Mistral AI | `https://api.mistral.ai/v1` | [Model catalog](https://docs.mistral.ai/api/endpoint/models), [Models](https://docs.mistral.ai/models) |
| Together AI | `https://api.together.ai/v1` | [Compatibility](https://docs.together.ai/docs/inference/openai-compatibility), [Serverless models](https://docs.together.ai/docs/serverless/models) |
| MiniMax 国际 / Global | `https://api.minimax.io/v1` | [API](https://platform.minimax.io/docs/api-reference/text-chat-openai) |
| 阿里百炼国际 / Alibaba Cloud International | `https://dashscope-intl.aliyuncs.com/compatible-mode/v1` | [区域及兼容地址 / Regions and endpoints](https://help.aliyun.com/zh/model-studio/batch-interfaces-compatible-with-openai) |
| Kimi 国际 / Global | `https://api.moonshot.ai/v1` | [API](https://platform.kimi.com/docs/api/models-overview) |

其他兼容服务、中转、Ollama 或 LM Studio 可使用“自定义兼容服务”。填写 API 基础地址，例如以 `/v1` 结尾的地址，不要填写完整的 `/chat/completions` 路径。HTTP 只用于本机回环地址，远程服务使用 HTTPS。 / Use **Custom compatible service** for other compatible APIs, gateways, Ollama or LM Studio. Enter the API base URL, such as one ending in `/v1`, rather than the complete `/chat/completions` route. HTTP is supported for local loopback services; remote services use HTTPS.

## 2026 年 9 月模型变化 / September 2026 model changes

下面是本次适配核对的代表型号，实际可选列表以刷新结果为准。 / Representative models checked for this update; refresh the catalog for the current list.

| 服务 / Service | 代表型号 / Model examples | 接入说明 / Compatibility notes |
| --- | --- | --- |
| OpenAI | `gpt-6-astra`, `gpt-5.6-sol`, `gpt-5.6-terra`, `gpt-5.6-luna` | 图文问答；GPT-6 Astra 不接受自定义 `temperature` 或 `top_p`。 / Text and image input; GPT-6 Astra does not accept custom `temperature` or `top_p`. [官方 / Source](https://developers.openai.com/api/docs/guides/latest-model) |
| DeepSeek | `deepseek-flash`, `deepseek-v4-pro` | 9 月 10 日更新的 Flash 支持图片，Pro 仍是文本模型。 / Flash, updated September 10, supports images; Pro is text-only. [官方 / Source](https://api-docs.deepseek.com/updates/) |
| Kimi | `kimi-k3`, `kimi-k2.7-code`, `kimi-k2.7-code-highspeed`, `kimi-k2.6` | K3 始终思考，不强制添加旧版 `thinking` 或采样参数。 / K3 always reasons; obsolete thinking and sampling overrides are not injected. [官方 / Source](https://platform.kimi.com/docs/models) |
| 阿里百炼 / Bailian | `qwen3.8-max`, `qwen3.8-flash` | 实时读取目录；新旧型号不通过猜测版本号替换。 / Load the live catalog without replacing saved IDs by guessing versions. [官方 / Source](https://help.aliyun.com/zh/model-studio/qwen3-8-max) |
| 智谱 / Zhipu | `glm-5.3`, `glm-5.3-flash` | 区分纯文本 5.3 和可识图的 Flash。 / Distinguish text-only 5.3 from visual Flash. [官方 / Source](https://docs.bigmodel.cn/cn/guide/start/model-overview) |
| Claude | `claude-opus-5` | 使用官方 OpenAI 兼容接口；此兼容层不会返回完整思考详情，部分原生功能未开放。 / Uses official OpenAI compatibility, which does not expose detailed thinking or every native feature. [官方 / Source](https://platform.claude.com/docs/en/cli-sdks-libraries/libraries/openai-sdk) |
| Gemini | `gemini-3.8-flash` | 兼容接口支持图片；原生 API 的视频理解能力不能直接等同于兼容接口视频输入。 / Images are supported; native video understanding does not imply compatible API video input. [官方 / Source](https://ai.google.dev/gemini-api/docs/openai) |
| xAI | `grok-4.6` | 使用官方图文 Chat Completions 接口。 / Uses official text/image Chat Completions. [官方 / Source](https://docs.x.ai/developers/models/grok-4.6) |

软件不再给所有模型固定发送采样温度，也不会为了接入新模型而自动关闭思考。屏幕问答对已核实最大输出值的模型使用对应上限；未知型号或上下文共享预算的模型保留服务端预算规则，不设置一个通用的小额度。 / MewuAI does not inject a fixed temperature into every model or disable reasoning to make a connection work. Screen requests use verified model-specific output maxima where applicable; unknown or shared-context budgets retain provider defaults rather than a universal small limit.

OpenAI 官方连接使用 Chat Completions，因此目录排除已知只能使用 Responses 的型号，例如 GPT-5.5 Pro；第三方平台可能提供自己的兼容转换，其目录按该平台元数据处理。 / The official OpenAI connection uses Chat Completions and excludes known Responses-only models such as GPT-5.5 Pro. Third-party platforms may provide their own compatible routing. [官方 / Source](https://developers.openai.com/api/docs/models/gpt-5.5-pro)

Kimi 新模型的连续追问会在内存中保留所需的原始回复上下文；这部分信息不会额外写入磁盘历史。 / Kimi follow-ups preserve the required original assistant context in memory without adding it to disk history.

图片、视频和最大输出能力取决于**服务地址与型号的组合**。目录加载和协议测试不能证明所有账号都可调用，也不能替代实际识图准确率验证。 / Image, video and output capabilities depend on the **endpoint and model together**. Catalog and protocol checks do not establish access for every account or guarantee visual accuracy.
