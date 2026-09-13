# 喵呜AI 0.4.5 / MewuAI 0.4.5

- 修复截图问答直接显示整段 JSON：模型在结构化回复前添加说明或漏转义引号时，恢复可解析的回答正文、段落与有效批注。
- 修正贴图层级：刚置顶的贴图立即显示在当前截图层上方；再次截图时，新截图层位于已有贴图下方，重新激活也不会遮住贴图。
- 双语 README 增加数据标注和表格识别实图，扩展为八宫格，保留真实试卷来源及视频 GIF。
- 更换为分别本地化的中英文封面，参考真实对话条布局并缩小面板比例；补充暂不支持 macOS 的说明和合作项目 Ta 的入口。

下载：`MewuAI-Setup-0.4.5-win-x64.exe`（安装版）或 `MewuAI-Portable-0.4.5-win-x64.zip`（便携版）。SHA-256 可在 GitHub 下载资产详情中查看，不附带单独校验文件。

---

- Fix raw JSON appearing in screenshot answers. Recover readable text, paragraph breaks and valid annotations when a model adds a preamble or leaves quotes unescaped in a structured reply.
- Correct pinned-image stacking: a newly pinned image immediately appears above the current capture overlay, and subsequent capture overlays stay below existing pins, including after reactivation.
- Expand the bilingual README gallery to eight examples with real data annotation and table recognition screenshots, retaining the exam source and video GIF.
- Add separate Chinese and English covers based on the real conversation-bar layout with a compact panel. Clarify that macOS is not currently supported and link to the partner project Ta.

Downloads: `MewuAI-Setup-0.4.5-win-x64.exe` (installer) or `MewuAI-Portable-0.4.5-win-x64.zip` (portable). SHA-256 digests are available in GitHub asset details; no separate checksum file is included.
