# 更新历史 / Changelog

每个正式版本对应自己的源码标签、安装包和双语发行说明。后续功能写入新版本；旧版本说明保留当时发布内容。/ Each release has its own source tag, packages, and bilingual notes. Later changes belong to later releases.

## 未发布 / Unreleased

- 部分采纳 [PR #10](https://github.com/abnste/mewu_ai/pull/10)：Codex / WorkBuddy 支持手动选择本机可执行文件，设置窗口支持最小化；同时修复所选路径保存及环境 Provider 导入时的路径保留。PR 中放宽 Codex 认证类型的改动暂未采纳。 / Partially adopt [PR #10](https://github.com/abnste/mewu_ai/pull/10): allow manually selecting the local Codex / WorkBuddy executables and minimizing the settings window; also persist selected paths and retain them during environment-provider imports. The PR's relaxed Codex account-type validation is not adopted.

## 0.4.8 — Issue #9 API 地址与 DeepSeek 状态 / Issue #9 endpoint recovery and DeepSeek states

- 修复 [Issue #9](https://github.com/abnste/mewu_ai/issues/9) 中 API 模型目录在裸地址返回网页或错误 JSON 时设置页可能崩溃的问题。现在有限尝试原地址、`/v1` 和 `/api/v1`；成功后把可用地址显示为未保存草稿，保存后用于后续请求，全部失败才提示 API 错误。 / Fix [Issue #9](https://github.com/abnste/mewu_ai/issues/9): recover model catalogs when a bare endpoint returns HTML or invalid JSON. Try the original address, `/v1`, and `/api/v1` within a bounded sequence; show a successful endpoint as an unsaved draft for later requests, and report an API error only after all candidates fail.
- 改进 [Issue #9](https://github.com/abnste/mewu_ai/issues/9) 中 DeepSeek 思考流的状态反馈：收到 `reasoning_content` 增量时显示仍在思考，只有终态没有正文时才显示思考-only 错误；不关闭思考、不降低输出预算。 / Improve [Issue #9](https://github.com/abnste/mewu_ai/issues/9) DeepSeek thinking-stream feedback: show that reasoning is still in progress when `reasoning_content` arrives, and report reasoning-only failure only after a terminal response without an answer; do not disable thinking or reduce output budgets.
- 部分采纳 [PR #8](https://github.com/abnste/mewu_ai/pull/8) 的 DeepSeek 思考开关适配与回归测试。 / Partially adopt the DeepSeek thinking-toggle compatibility and regression coverage from [PR #8](https://github.com/abnste/mewu_ai/pull/8).

## 0.4.7 — API 回复与设置稳定性 / API replies and settings stability

- 修正视觉问答中完整代码块回答被误判为损坏 JSON、继而提示“只有思考”的问题；保留正常回答，继续拒绝真正截断的回复。 / Preserve complete fenced-code answers in visual conversations instead of mistaking them for broken JSON and reporting reasoning only. Genuinely interrupted responses are still rejected.
- API 设置在填写地址时先校验未完成输入，格式错误的密钥显示提示而不使自动模型加载崩溃；设置窗口增加最大化/还原按钮，支持双击标题栏切换，默认仍保持紧凑尺寸。 / Validate endpoint drafts before loading credentials and report malformed API keys without crashing automatic model loading. Add maximize/restore buttons and title-bar double-click support while keeping the compact default window size.

- 补齐兼容 API 的分块与对象形式正文读取，严格区分正文、思考和未知内容类型；批注 JSON 尾部损坏时保留完整有效回答与已有标注，不将截断的网络回复当作完成。 / Read supported typed and object-form answers from compatible APIs while keeping reasoning and unknown content out of the answer. Preserve a complete valid answer and existing annotations when annotation JSON is malformed, without treating interrupted responses as complete.

- [完整说明 / Full notes](./docs/release-notes-v0.4.7.md)

## 0.4.6 — API 接入、标注编辑与应用快照 / API connections, annotation editing and app snapshots

- 按 2026 年 9 月官方接口更新国内外 API 接入模板，添加服务商分组与搜索；完善模型目录格式、分页及对话模型筛选，适配新推理模型参数、图文能力和类型化流式回复。已有连接、密钥及默认选择保持原样。 / Update China and global API presets against September 2026 documentation, with grouped service search, model catalog pagination and chat filtering, current reasoning-model parameters, vision capabilities and typed streaming responses. Existing connections, keys and defaults are preserved.

- 颜色选择改为可直接点选、拖动的全彩色板：外圈选色相，中间调整饱和度与明暗，保留同步的 RGB 和 HEX 精确输入。 / Replace RGB sliders with a full-color palette: choose a hue on the ring and saturation and brightness on the center plane, with synchronized RGB and HEX inputs.

- 整理手工标注工具分组并补充可调整两端点的直线。按 Shift 绘制或调整时，直线和箭头锁定水平、垂直或 45°，矩形保持正方形，椭圆保持正圆。序号圆底更紧凑，删除后优先复用空缺编号；选中对象可改色，文字字体、字号和荧光底色可修改并撤销重做。空白文本草稿离开后自动清理，马赛克拖动时直接预览真实像素化效果。 / Group manual annotation tools and add lines with editable endpoints. Hold Shift while drawing or resizing to constrain lines and arrows to horizontal, vertical or 45°, rectangles to squares, and ellipses to circles. Number markers are more compact and reuse deleted numbers. Selected objects support color changes; text fonts, sizes and highlights support undo and redo. Empty text drafts are removed when abandoned, and mosaic drags preview real pixelation.

- 恢复紧凑的设置窗口高度，修正 API 连接默认标签和更多操作图标居中、卡片底部留白、刷新图标与菜单阴影裁切。手工标注仅在点击“完成”后自动复制，绘制、编辑和撤销重做期间保留原剪贴板；AI 标注返回后仍可自动复制。复制图不包含编辑光标、焦点框或控制点。 / Restore compact settings height and fix default-badge and menu-icon alignment, card padding, the refresh icon and clipped menu shadows. Manual annotations copy only when Finish is clicked; drawing, editing, undo and redo preserve the clipboard. Returned AI annotations still copy automatically. Copies exclude editor carets, focus borders and handles.

- API 设置改为可命名的连接列表，点击原地展开，添加时选择服务商；提供重命名、设为默认和删除操作。切换时保留模型、密钥及未完成的高级设置草稿，编辑或测试备用连接不再更改默认连接。 / API settings now show named connections with inline editors and service selection when adding. Rename, set a default, or remove connections from their menus. Switching preserves model, key and unfinished advanced-setting drafts; editing or testing a backup connection no longer changes the default.

- 修正设置页复选框勾号偏移，增加 API 接入点操作按钮与列表的间距，并统一 API Key 输入框及相邻按钮的高度。 / Center settings checkmarks, separate endpoint actions from the selector, and align the API key field and its adjacent button with the other form controls.

- 截图画布支持右键穿透：右键点击或按住拖动操作底层应用的左键，Ctrl＋右键传递真正的右键。松开后恢复截图界面并保留已有截图；对话条、工具栏和 OCR 的右键菜单保留。 / On the capture canvas, right-click or hold and drag to operate the underlying application's left button; Ctrl + right-click forwards a real right-click. Releasing restores the overlay and preserves existing captures. Composer, toolbar and OCR context menus remain available.

- 无延时截图在快捷键所在的界面线程直接冻结画面，去掉启动过程的两次额外排队，避免抓取时机落到后续画面。 / Zero-delay screenshot requests freeze the current frame directly on the hotkey UI thread, eliminating two unnecessary dispatcher hops.

- 修复置顶长图放大时受窗口尺寸约束而变形的问题，窗口和图片同步等比例缩放；改进滚动拼接接缝，避免重复保留上一帧的底部边框。 / Fix pinned long images distorting at native window size limits, and join scrolling frames within their overlap to avoid repeating bottom borders.
- 自动吸附窗口后可截取应用快照：在前台保持冻结画面，通过系统窗口渲染接口取得可访问滚动区域的原始图像并自动拼接，完成后恢复滚动位置、置顶并引用；取消或关闭也先恢复位置。依赖应用提供可用的渲染和滚动接口，不以文字重排代替原图，不将拼接失败当作完整截图。手动拖选仍保留滚动长截图。 / Window-snapped selections can capture original pixels from an accessible scroll area through Windows compositor capture while the foreground stays frozen. Capture restores the original scroll position, then pins and references the result; cancellation and closing also restore first. Rendering and scroll support depend on the application. No reflowed text substitution or incomplete captures reported as complete; manual selections retain scrolling capture.
- 长截图取消固定 24 段限制，改用增量拼接，仅保留合成图与最新匹配帧，按实际图像容量控制内存。 / Remove the fixed 24-segment limit and merge incrementally, retaining the composite and latest matching frame within the image capacity budget.

- [完整说明 / Full notes](./docs/release-notes-v0.4.6.md)

## 0.4.5 — 回复解析与贴图层级 / Reply parsing and pinned windows

- 修复刚置顶的贴图被当前截图层遮挡；贴图保持在上方，之后开启的截图层位于已有贴图下方。 / Keep newly pinned images above the current capture overlay and later capture overlays below existing pins.
- 双语 README 增加数据标注和表格识别实图，扩展为八宫格。 / Expand the bilingual gallery to eight examples with data annotation and table recognition screenshots.
- 修复截图问答中模型前言和未转义引号导致整段 JSON 显示的问题；保留可恢复的正文、分段及通过校验的批注。 / Fix raw JSON appearing in screenshot answers when a model adds a preamble or unescaped quotes; retain recoverable text, paragraphs and validated annotations.

- 更新中英文独立封面，并补充 Windows 平台说明和合作项目入口。 / Add separate localized covers, clarify Windows support and link to a partner project.
- [完整说明 / Full notes](./docs/release-notes-v0.4.5.md)

## 0.4.4 — 原位翻译与窗口交互 / In-place translation and window interaction

- 翻译批次并行处理，改善原文与译文的定位及复制、贴图、导出一致性。 / Process translation batches concurrently and improve placement across the overlay, copies, pinned images and exports.
- 修复贴图后的截图层级，调整主界面防捕获以减少录屏冲突。 / Fix capture stacking over pinned images and adjust main-window capture protection.
- 双语 README 改为六宫格演示，并加强源码与发行包隐私检查。 / Add a six-cell bilingual preview gallery and strengthen repository and package privacy checks.
- [完整说明 / Full notes](./docs/release-notes-v0.4.4.md)

## 0.4.3 — 表格识别与回复容量 / Table recognition and response capacity

- MiniMax-M3 问答使用官方最大输出并保留思考，移除固定 8192 token 限制。 / Use M3's documented maximum output while retaining thinking, removing the fixed 8192-token limit.
- 精简表格请求，保留原批注，完整回复后呈现表格；翻译超限和长行自动拆分补译。 / Simplify table requests, preserve annotations and display completed tables; recover translation limit failures with smaller sections.
- 移除独立试卷入口，沿用普通截图问答；更新双语说明。 / Remove the separate paper workflow entry and use ordinary screenshot conversations.
- [完整说明 / Full notes](./docs/release-notes-v0.4.3.md)

## 0.4.2 — 原卷批注与数学排版 / On-paper feedback and math typesetting

- 错因与正确公式在原卷旁显示、避让作答和密集文字，并随带标注试卷导出。 / Place and export feedback beside answers while avoiding other handwriting and dense text.
- 新增整页查看、公式预览与原文编辑；AI 回复支持常用 LaTeX 公式，复制保留原文。 / Add full-page viewing, formula previews, source editing and copyable LaTeX in replies.
- 修正英文演示知识点，强化批改内容语言要求；随包添加数学组件及字体许可。 / Correct English knowledge points, clarify grading output language and package math/font licenses.

## 0.4.1 — 多步计算与批改说明 / Calculation steps and review explanations

- 修复计算步骤挤在同一行，保留换行并支持回车、自动折行和滚动。 / Preserve calculation step breaks with multiline editing, wrapping and scrolling.
- 批改说明可编辑、确认并随逐题 CSV 导出，避免改判后仍显示旧说明。 / Edit and export explanations together with reviewed verdicts.
- README 展示官方真实手写答卷经逐题核对后的完整流程。 / Show the reviewed workflow on an official handwritten exam script.

## 0.4.0 — 教学批改与多页作业 / Reviewed grading and multi-page assignments

- 新增 PDF 选页导入、跨次截图收集、身份去重、双阶段逐题批改与老师核对。/ Added selected PDF imports, cross-capture collections, identity checks, two-stage grading and teacher review.
- 支持共同错题统计、练习编辑及标注页/表格/分离答案的教学包导出。/ Added shared-error summaries, editable practice and teaching-pack export.
- PDF 渲染使用独立有界进程，修复本机 AMD 驱动退出异常。/ Isolated PDF rendering to avoid the reproduced AMD shutdown crash.
- 修复试卷批改中同数字题干抢占答案批注位置，保留数学正负号并限制 OCR 校准范围。/ Prevented repeated text in exam questions from pulling annotations away from student answers.
- 保留回复列表中的原始题号，修正显示与复制从 1 重新编号的问题。/ Preserved original question numbers in displayed and copied lists.
- 补充教学任务的证据、评分与出题规则，明确未生成批注的状态。/ Added evidence and scoring guidance for teaching tasks and clear feedback when annotations are missing.
- 双卷最终实测 8 项符合参考、4 项进入待核；手写识读仍须核对，视觉渠道不可用时明确失败。/ The final live two-page replay yielded eight matching states and four uncertain states; handwriting and provider availability still require review.

## 0.3.4 — 对话图片与翻译排版 / Reply images and translation layout

- 修复 AI 回复图片不显示，接入 Hermes 本地图片、MEDIA 引用及图片工具结果。/ Fixed missing reply images, including local images, MEDIA references, and image-tool results from Hermes.
- 去掉图片的灰色底板和程序标签，复制时保留说明、文字与表情。/ Removed gray image backdrops and app-added labels while preserving descriptions, text, and emoji when copying.
- 修复长译文及双栏译文重叠，统一显示、选择和导出布局。/ Fixed overlapping translations across lines and columns, with consistent display, selection, and export layouts.

[完整双语说明 / Full notes](./docs/release-notes-v0.3.4.md) · [Release](https://github.com/abnste/mewu_ai/releases/tag/v0.3.4) · [v0.3.3 → v0.3.4](https://github.com/abnste/mewu_ai/compare/v0.3.3...v0.3.4)

## 0.3.3 — 视频置顶、思考光效与开源许可 / Pinned videos, activity glow, and license notices

- 修复带标注视频置顶后的闪退。/ Fixed crashes after pinning annotated videos.
- 新增可开关、可调 RGB 颜色的底部思考光效，修复任务栏区域截断。/ Added a configurable AI activity glow that reaches the full display edge.
- 统一历史与最新回复的复制菜单，修复阴影裁切，保留完整内容和表情。/ Unified reply copy menus, fixed clipped shadows, and preserved full text and emoji.
- 关于页新增第三方组件与完整许可的离线查看入口。/ Added offline access to third-party components and full license notices in About.

[完整双语说明 / Full notes](./docs/release-notes-v0.3.3.md) · [Release](https://github.com/abnste/mewu_ai/releases/tag/v0.3.3) · [v0.3.2 → v0.3.3](https://github.com/abnste/mewu_ai/compare/v0.3.2...v0.3.3)

## 0.3.2 — 视频时间、对话输入与中文显示 / Video timing, input, and Chinese text

- 修复 MiniMax 视频时间压缩为零点几秒，以及末尾帧不完整引发的 HTTP 400；保留原视频。/ Fixed compressed MiniMax timestamps and HTTP 400 errors caused by incomplete final frame groups, preserving the original video.
- 修复视频播放/暂停状态、对话条弹出焦点、录制后直接输入和 Ctrl+C 复制回答。/ Fixed playback controls, input focus when the bar appears, typing after recording, and Ctrl+C copying.
- 统一中文回复字体，默认简体中文，保留明确的语言要求、引用与代码。/ Unified Chinese reply fonts and defaulted Chinese answers to Simplified Chinese while preserving explicit language requests, quotations, and code.
- 新增跨截图关联箭头，改善多选区工具条显隐与逐行翻译。/ Added cross-screenshot connection arrows and improved selection toolbars and line-by-line translation.

- 修复首轮回答显示后继续提问仍长时间停在“AI 正在分析”的问题；后台补标会被安全取消并让出请求。/ Fixed follow-up questions remaining stuck on “AI is analyzing” after the first answer; background annotation repair now yields to the new request safely.

- 修复多区域提问期间工具条只响应一个选区的问题，等待回答和后台补标时也能悬停切换。/ Fixed toolbars responding to only one region during multi-region questions, including while waiting for an answer or additional annotations.

- 截图框选、移动或缩放完成后自动聚焦对话输入，鼠标悬停不再打断刚开始的输入。/ The conversation input receives focus after selecting, moving or resizing a screenshot, and hovering no longer interrupts typing.

- 教学演示默认开启，移除截图左上角的共享状态标识；仍可在设置中关闭。/ Teaching mode is on by default, without a sharing-status badge on the capture overlay. It can still be turned off in Settings.

- 教学演示模式支持录屏和长截图，控制条与预览自动避开采集区。全屏没有空位时可用 F8 停止/完成，倒计时中可取消。/ Teaching mode now supports recording and scrolling capture. Controls and previews stay outside the capture area; F8 stops or finishes capture and cancels the countdown.

[完整双语说明 / Full notes](./docs/release-notes-v0.3.2.md) · [Release](https://github.com/abnste/mewu_ai/releases/tag/v0.3.2) · [v0.3.1 → v0.3.2](https://github.com/abnste/mewu_ai/compare/v0.3.1...v0.3.2)

## 0.3.1 — 视频请求诊断 / Video request diagnostics

- 视频请求失败时显示实际渠道、可识别的失败原因、服务代码和安全的追踪编号，不再只显示笼统的 HTTP 状态。
- 对 MiniMax 返回的“视频内容被拒绝”也给出明确提示，避免误导用户反复压缩一个本身未超限的文件。
- 错误处理不会回显服务端可能包含的提示词、媒体数据或认证信息。
- 统一 API 与 MiniMax Code 的非成功响应处理，并补齐视频 422 的安全回归测试。
- 原位翻译可识别常见的 JSON 包装和空白 OCR 行；未按原行数返回时会自动重试一次，再给出可操作的提示。

[完整双语说明 / Full notes](./docs/release-notes-v0.3.1.md) · [Release](https://github.com/abnste/mewu_ai/releases/tag/v0.3.1) · [v0.3.0 → v0.3.1](https://github.com/abnste/mewu_ai/compare/v0.3.0...v0.3.1)

## 0.3.0 — 多渠道 AI 与模型切换 / Multiple AI channels

- 新增 ChatGPT Work / Codex、WorkBuddy、MiniMax Code 桌面接入；保留 API、Hermes。
- 多 API 接入列表、配置共存、单层模型菜单、记住上次渠道和模型。
- 修复纯文字视觉提示、原始 JSON 回答、WorkBuddy 思考/连接等待与模型选项兼容。
- 统一 AI 设置、主页渠道状态、快捷键清空和模型弹层布局。
- 正式采用 MPL-2.0，补齐社区规范；README 使用 GIF，下载资产仅安装 EXE 与便携 ZIP。

[完整双语说明 / Full notes](./docs/release-notes-v0.3.0.md) · [Release](https://github.com/abnste/mewu_ai/releases/tag/v0.3.0) · [v0.2.6 → v0.3.0](https://github.com/abnste/mewu_ai/compare/v0.2.6...v0.3.0)

## 历次正式发布 / Previous published releases

以下版本已在 GitHub 发布安装包。原发行说明通过版本标签永久定位，仓库内文档保留原文。/ These versions have published packages; tagged notes preserve the original release text.

| 版本 / Version | 主要内容 / Highlights | 原发行说明 / Original notes |
| --- | --- | --- |
| [0.2.6](https://github.com/abnste/mewu_ai/releases/tag/v0.2.6) | 标注对象直接编辑、RGB 复制、录屏电脑/麦克风音频、MP3 导出、Hermes 安装后启动、历史复制和高 DPI 菜单 / Annotation handles, RGB copying, recording audio, MP3 export, Hermes startup, history and DPI fixes | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.2.6/docs/release-notes-v0.2.6.md) |
| [0.2.5](https://github.com/abnste/mewu_ai/releases/tag/v0.2.5) | 教学共享、增量回答与交互性能、滚动箭头、设置自动检查更新、录屏测试时序修复 / Teaching mode, streaming performance, scroll arrows, automatic update checks, recording test timing | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.2.5/docs/release-notes-v0.2.5.md) |
| [0.2.3](https://github.com/abnste/mewu_ai/releases/tag/v0.2.3) | Hermes 启动兼容、API 提供商配置、请求参数和贴图阴影 / Hermes startup, API configuration, request parameters and pinned-image shadows | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.2.3/docs/release-notes-v0.2.3.md) |
| [0.2.2](https://github.com/abnste/mewu_ai/releases/tag/v0.2.2) | 自包含包还原与慢机器录屏取消等待，包含 0.2.0 / 0.2.1 的功能 / Self-contained restore and recording cancellation, including 0.2.0 / 0.2.1 changes | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.2.2/docs/release-notes-v0.2.2.md) |
| [0.1.0](https://github.com/abnste/mewu_ai/releases/tag/v0.1.0) | 稳定性与隐私、马赛克坐标、结构化回答、OCR/翻译、历史抽屉 / Stability, privacy, mosaic coordinates, structured answers, OCR/translation and history drawer | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.1.0/docs/release-notes-v0.1.0.md) |
| [0.0.11](https://github.com/abnste/mewu_ai/releases/tag/v0.0.11) | 关于页与设置导航裁切 / About page and settings navigation clipping | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.0.11/docs/release-notes-v0.0.11.md) |
| [0.0.10](https://github.com/abnste/mewu_ai/releases/tag/v0.0.10) | 上传按钮、流式布局、Hermes 状态、批注、翻译与长截图 / Upload button, streaming layout, Hermes status, annotations, translation and scrolling capture | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.0.10/docs/release-notes-v0.0.10.md) |
| [0.0.9](https://github.com/abnste/mewu_ai/releases/tag/v0.0.9) | Release 资产补传 / Release asset upload recovery | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.0.9/docs/release-notes-v0.0.9.md) |
| [0.0.8](https://github.com/abnste/mewu_ai/releases/tag/v0.0.8) | 安装器版本同步、思考与标注布局、附件引用 / Installer version sync, reasoning and annotation layout, attachment references | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.0.8/docs/release-notes-v0.0.8.md) |
| [0.0.4](https://github.com/abnste/mewu_ai/releases/tag/v0.0.4) | 智能框选、长截图、马赛克与统一图像/视频批注 / Window selection, scrolling capture, pixelation and image/video annotations | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.0.4/docs/release-notes-v0.0.4.md) |
| [0.0.1](https://github.com/abnste/mewu_ai/releases/tag/v0.0.1) | 首个 Windows x64 公开版本 / First public Windows x64 release | [中英 / ZH–EN](https://github.com/abnste/mewu_ai/blob/v0.0.1/docs/release-notes-v0.0.1.md) |

## 保留的发布准备记录 / Retained release preparation records

以下文档和标签属于准备或失败构建，没有对应的正式 Release 下载；不能把它们当成已成功发布。/ The following preparation or failed-build records have no corresponding published Release downloads.

- [0.2.4](./docs/release-notes-v0.2.4.md)：正式构建被 GIF 录屏测试拦截，改由 0.2.5 发布。
- [0.2.1](./docs/release-notes-v0.2.1.md)、[0.2.0](./docs/release-notes-v0.2.0.md)：还原/录屏测试未完成发布，功能进入 0.2.2。
- [0.0.7](./docs/release-notes-v0.0.7.md)、[0.0.3](./docs/release-notes-v0.0.3.md)、[0.0.2](./docs/release-notes-v0.0.2.md)：保留的早期发布准备文档。

## 2026-09-07 文档与附件修正 / Documentation and asset correction

此前把 v0.2.6 之后的文档清理追加到 v0.2.6 Release 是一次发布记录错误，现已恢复原始功能说明，新功能按 v0.3.0 独立归档；旧标签和安装包未移动或替换。v0.2.6 的 SHA256SUMS.txt 在 2026-09-07 被移除，而不是发布时没有提供。用户从 v0.2.5 及更早版本升级时应手动下载安装器，避免旧更新器找不到校验文件。

An earlier edit incorrectly added later documentation cleanup to the v0.2.6 release. Its original feature notes are restored, and subsequent features are recorded under v0.3.0. Existing tags and binaries are unchanged. The v0.2.6 checksum attachment was removed on 2026-09-07; it was present at the original release. Users of v0.2.5 or earlier should download the installer manually because those updaters require the old checksum file.
