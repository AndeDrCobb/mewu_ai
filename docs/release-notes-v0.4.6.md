# 喵呜AI 0.4.6 / MewuAI 0.4.6

- 按截至 2026 年 9 月 14 日的官方资料更新 API 接入，提供 21 项国内外服务商及自定义连接模板，支持分组搜索和实时模型目录。完善目录分页、对话模型筛选、新模型请求参数、图文能力与流式回复兼容，并保留 Kimi 连续追问所需的完整回复上下文；该额外上下文仅保存在内存。实际可调用模型以服务商账户权限为准。[接入清单与说明](./api-providers.md)
- API 设置改为可命名、原位展开的连接卡片，添加时选择服务商，支持重命名、设为默认和删除。编辑与测试备用连接不再改变默认项，切换时保留密钥、模型和未保存的高级设置草稿。
- 整理手工标注工具，增加可编辑端点的直线。按住 Shift 约束直线、箭头、正方形和正圆；选中对象后可改色，文字字体、字号和荧光底色支持调整及撤销重做。序号更紧凑并复用删除后的空缺编号，清理未输入内容的文本框，马赛克拖动时显示真实像素化预览。
- 取色改为全彩色板，支持色相环、饱和度与明暗平面，以及 RGB、HEX 精确输入。手工标注仅在点击“完成”后自动复制，绘制和编辑期间不覆盖剪贴板；复制结果不含编辑光标或控制点。
- 自动吸附窗口后可截取应用快照：前台保持冻结画面，自动采集受支持滚动区域的原始画面，恢复原位置后置顶并引用。取消或关闭也先恢复位置；可用性取决于应用的渲染与滚动接口。手动框选仍可滚动截长图，取消固定 24 段限制，按实际图像容量控制内存。
- 修复置顶长图放大变形、拼接边缘重复、截图触发滞后和应用快照无反馈。截图画布支持右键穿透点击与拖动，Ctrl＋右键传递真正的右键操作。
- 修正设置页勾号、默认标签和更多操作图标的对齐，统一输入框高度，恢复紧凑布局并修复菜单阴影裁切。设置窗口现在可以被截图；密钥继续以圆点显示并加密保存。

下载：`MewuAI-Setup-0.4.6-win-x64.exe`（安装版）或 `MewuAI-Portable-0.4.6-win-x64.zip`（便携版）。SHA-256 可在 GitHub 下载资产详情中查看，不附带单独校验文件。

---

- Update API connections against official documentation available on September 14, 2026, with 21 China/global service and custom-connection templates, grouped search and live model catalogs. Improve pagination, chat-model filtering, current model parameters, vision support and streaming compatibility. Retain the complete assistant context needed for Kimi follow-ups in memory only. Callable models still depend on your provider account. [Supported services and setup](./api-providers.md)
- Redesign API settings as named connection cards with inline editors and service selection when adding. Rename, set a default, or delete connections. Editing or testing a backup connection preserves the default; switching keeps key, model and unfinished advanced-setting drafts.
- Organize manual annotation tools and add lines with editable endpoints. Hold Shift to constrain lines, arrows, squares and circles. Change selected objects' colors and edit text fonts, sizes and highlights with undo and redo. Compact numbered markers reuse deleted numbers, empty text drafts are cleared, and mosaic drags preview real pixelation.
- Replace RGB sliders with a full-color palette, including a hue ring, saturation/brightness plane and precise RGB/HEX inputs. Manual annotations copy only when Finish is clicked, preserving the clipboard while drawing or editing. Copied images exclude editing carets and handles.
- Window-snapped selections can capture app snapshots while the foreground stays frozen. Capture supported scroll areas as original pixels, restore the scroll position, then pin and reference the result. Cancellation and closing also restore first. Availability depends on the application's rendering and scrolling interfaces. Manual selections retain scrolling capture without the fixed 24-segment limit, with memory controlled by image capacity.
- Fix pinned long images distorting when enlarged, repeated stitching edges, delayed screenshot capture and missing app-snapshot feedback. Right-click or hold and drag on the capture canvas to operate the underlying application's left button; Ctrl + right-click passes through a real right-click.
- Align settings checkmarks, default badges and menu icons, unify input heights, restore compact spacing and fix clipped menu shadows. The settings window can now be captured; keys remain masked and encrypted on disk.

Downloads: `MewuAI-Setup-0.4.6-win-x64.exe` (installer) or `MewuAI-Portable-0.4.6-win-x64.zip` (portable). SHA-256 digests are available in GitHub asset details; no separate checksum file is included.
