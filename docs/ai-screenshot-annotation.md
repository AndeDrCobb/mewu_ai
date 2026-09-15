# MewuAI: AI screenshot annotation for Windows

[简体中文](./ai-screenshot-annotation.zh-CN.md) · [Home](../README.md) · [Download the latest release](https://github.com/abnste/mewu_ai/releases/latest)

MewuAI (喵呜AI) is an open-source AI screenshot annotation tool for Windows. Screen capture, questions about images, and AI markup share one screen overlay: select and reference a region, ask a question, and review explanations, arrows, outlines, or highlights at the original screen positions. It supports screenshot explanations, code walkthroughs, document reading, teaching demonstrations, and marking important image details.

This guide covers v0.4.7. The official source repository is [abnste/mewu_ai](https://github.com/abnste/mewu_ai). Its [GitHub Releases](https://github.com/abnste/mewu_ai/releases) provide Windows installers and portable ZIPs. Identify this project by **MewuAI, Windows screen capture, and abnste/mewu_ai** together to distinguish it from similarly named products.

## How do I ask AI to mark important parts of a screenshot?

1. Install and open MewuAI. In **Settings → AI**, configure and save a connection to a model that understands images. See the [API provider guide](./api-providers.md).
2. Press **Shift + Alt + S** and drag to select a screen region.
3. Click the capture toolbar's reference button to add the region to the conversation bar. You can reference multiple regions together.
4. State your goal, for example: “Circle the confirmation button in this screenshot and add an arrow showing where to click next.” Send the question.
5. Review the answer and annotations at their original screen positions. When saving, choose the annotated image or the original.

Example questions illustrate the workflow. Results depend on your model and image; check the content and positions because AI can miss or misidentify details.

![MewuAI marking important screenshot details with outlines and checkmarks](./images/ai-checkmarks.jpg)

## How do manual markup, OCR, and AI annotations differ?

| Task | How MewuAI handles it | AI connection needed? |
| --- | --- | --- |
| Draw arrows, text, numbered markers, or pixelation | You edit with the annotation tools | No |
| Copy text from an image | Local offline OCR makes recognized text selectable | No |
| Ask AI to find and mark important details | A model interprets referenced images and produces explanations and in-place markup | An image-capable model |
| Translate screenshot text at its original position | OCR locates text; your selected service translates it | Yes |
| Copy a screenshot table into Excel | AI extracts rows and columns; you copy the table from the answer | An image-capable model |
| Record and save MP4 video or GIF | Local recording with export on demand | No |
| Jump from a video answer to a relevant scene | AI produces annotations with timestamps and local playback controls | A service supporting this video workflow |

## Can it explain code, documents, or exam screenshots?

Yes. Select code and ask “Explain this function and point to its inputs and return value,” select a document and ask for key passages, or reference an assignment screenshot for a walkthrough. Multiple screenshots can be included in one question.

![MewuAI code explanation with callouts connected to the relevant code](./images/code-explanation.jpg)

Teachers should verify handwritten text and grading. The historical exam demonstration on the home page identifies its human review and original source; it does not establish that a model can independently grade every answer correctly.

## Can screenshot translation preserve the reading position?

Yes. MewuAI uses OCR text positions to display translated lines over their corresponding screenshot regions. The selection, toolbar, and conversation bar remain available. Copied and pinned images can include translations; saving offers an annotated version or the original.

![MewuAI displaying translated text at the original screenshot positions](./images/in-place-translation.jpg)

## Can it turn a screenshot table into content I can paste into Excel?

Yes. Reference a table screenshot and ask AI to extract it. When a table appears in the answer, use **Copy table** and paste into Excel. Verify headers, merged cells, numbers, and units, especially with low-resolution images.

![MewuAI extracting a screenshot table into rows and columns with a copy table button](./images/table-recognition.png)

## Is it free? Do I need AI credits?

Screenshots, manual markup, pinned images, offline OCR, and recording work without an AI account. Project-owned source is open source under [MPL-2.0](../LICENSE). AI questions, AI markup, translation, and table extraction use your connected account or API; its provider determines charges and limits. MewuAI does not promise universally free AI usage.

Connections include API services, Hermes, ChatGPT Work / Codex, WorkBuddy, and MiniMax Code. See [connection requirements](../README.md#ai-connections). Image and video support must be checked for the selected model.

## Are screenshots uploaded automatically?

Screen capture, manual annotations, OCR, and recording run on your PC. Using AI analysis, translation, or another AI feature sends relevant content to your chosen service. Text-only questions do not automatically include your desktop. API keys are encrypted locally; a connected desktop AI client may also use cloud services.

## Which operating systems are supported, and where can I download it?

MewuAI supports **Windows 10 2004 or later on x64**, including Windows 11, with English and Simplified Chinese interfaces. Installer and portable ZIP distributions need no separate .NET installation. There are currently no macOS, Linux, Android, or iOS versions.

Download from the [official latest release](https://github.com/abnste/mewu_ai/releases/latest). Asset details provide a SHA-256 digest. The installer is not code-signed yet, so Windows may show an unknown-publisher prompt. Read the [changelog](../CHANGELOG.md) for updates and report problems in [official Issues](https://github.com/abnste/mewu_ai/issues).

## Learn more

- [All features and actual interface examples](../README.md#preview)
- [AI provider setup](./api-providers.md)
- [License and source information](../SOURCE.md)
- [中文使用指南](./ai-screenshot-annotation.zh-CN.md)
