# 喵呜AI 0.4.8 / MewuAI 0.4.8

- 修复 [Issue #9](https://github.com/abnste/mewu_ai/issues/9)：裸 API 地址的模型目录请求会有限尝试原地址、`/v1` 和 `/api/v1`，成功的地址显示为未保存草稿并可保存后用于后续请求；全部失败才提示 API 错误。
- 改进 [Issue #9](https://github.com/abnste/mewu_ai/issues/9) 的 DeepSeek 思考流状态：收到思考增量时明确显示仍在思考，只有终态没有正文时才提示思考-only；不关闭思考或降低输出预算。
- 改进兼容 API 的回答读取，支持常见的对象及类型化文本块，避免把思考或未知内容混入正文。包含社区贡献 [PR #6](https://github.com/abnste/mewu_ai/pull/6) 及后续兼容修复。
- 修复视觉问答中完整的非 JSON 代码块被误判为损坏回复、提示“只有思考”的问题，包括没有标明语言的代码块。批注 JSON 尾部损坏时保留完整有效回答与已有标注；真正截断的网络回复仍会提示失败。
- API 设置在输入地址时先检查未完成的内容，格式错误的密钥会显示提示，自动加载模型列表时不再因此崩溃。切换连接继续保留编辑草稿，默认连接仍由用户明确选择。
- 设置窗口增加最大化与还原按钮，也可双击标题栏切换。默认保持紧凑尺寸，最大化后保留系统任务栏的可见空间。

下载：[安装版 EXE](https://github.com/abnste/mewu_ai/releases/download/v0.4.8/MewuAI-Setup-0.4.8-win-x64.exe) 或 [便携版 ZIP](https://github.com/abnste/mewu_ai/releases/download/v0.4.8/MewuAI-Portable-0.4.8-win-x64.zip)。支持 Windows 10 2004 及以上 x64 系统，无需另装 .NET。SHA-256 可在 GitHub 下载资产详情中查看，不附带单独校验文件。

使用 v0.2.5 及更早版本时，请下载安装器手动升级。此前版本的改动见[更新日志](https://github.com/abnste/mewu_ai/blob/v0.4.8/CHANGELOG.md)。

---

- Fix [Issue #9](https://github.com/abnste/mewu_ai/issues/9): model catalog loading now tries the original bare API endpoint, `/v1`, and `/api/v1` within a bounded sequence. A successful endpoint is shown as an unsaved draft and can be saved for later requests; an API error is shown only after all candidates fail.
- Improve [Issue #9](https://github.com/abnste/mewu_ai/issues/9) DeepSeek thinking-stream status: show that reasoning is still in progress when a reasoning delta arrives, and report reasoning-only failure only after a terminal response without an answer. Thinking and output budgets remain unchanged.
- Improve answer reading for compatible APIs, including supported object forms and typed text blocks, while keeping reasoning and unknown content out of the answer. Includes community contribution [PR #6](https://github.com/abnste/mewu_ai/pull/6) and follow-up compatibility fixes.
- Fix complete non-JSON code blocks in visual answers being rejected as broken replies and reported as reasoning only, including blocks without a language label. Preserve a complete valid answer and existing annotations when the annotation JSON tail is malformed; interrupted network responses still report a failure.
- Validate unfinished endpoint addresses in API settings before continuing. Malformed keys now show a message without crashing automatic model loading. Switching connections continues to preserve editing drafts, and the default changes only when explicitly selected.
- Add maximize and restore buttons to Settings, with title-bar double-click support. Keep the compact default size and leave the system taskbar visible when maximized.

Downloads: [installer EXE](https://github.com/abnste/mewu_ai/releases/download/v0.4.8/MewuAI-Setup-0.4.8-win-x64.exe) or [portable ZIP](https://github.com/abnste/mewu_ai/releases/download/v0.4.8/MewuAI-Portable-0.4.8-win-x64.zip). Requires Windows 10 2004 or later, x64; no separate .NET installation is needed. SHA-256 digests are available in GitHub asset details; no separate checksum file is included.

If you use v0.2.5 or earlier, download the installer to upgrade manually. See the [changelog](https://github.com/abnste/mewu_ai/blob/v0.4.8/CHANGELOG.md) for earlier changes.
