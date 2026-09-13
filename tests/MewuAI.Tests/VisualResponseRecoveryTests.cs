// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using mewu_ai_Assistant.AI;
using mewu_ai_Assistant.Models;
using Xunit;

namespace MewuAI.Tests;

public sealed class VisualResponseRecoveryTests
{
    // Synthetic content reproduces the transport shape without publishing a conversation.
    private const string Payload = """
        {
          "annotationProtocol": "mewu.visual-annotations/1",
          "answer": "选择**示例选项**。\n\n说明：这是"默认项"，也称"首选项"。",
          "annotationMode": "append",
          "annotations": [
            {
              "target": {"regionIndex": 0, "referenceHandle": "ref-example"},
              "kind": "callout",
              "geometry": {"coordinateSpace": "normalized", "rect": {"x": 0.04, "y": 0.63, "width": 0.56, "height": 0.13}},
              "style": {"color": "#5B85E8"},
              "label": "示例选项",
              "content": {"text": "示例标注"}
            }
          ]
        }
        """;
    private const string Expected = "选择**示例选项**。\n\n说明：这是\"默认项\"，也称\"首选项\"。";

    [Theory]
    [InlineData("")]
    [InlineData("<|tool|>\n现在输出 JSON。先给结论。\n")]
    [InlineData("下面是结果：\n```json\n")]
    public void RecoversAnswerAndValidatedAnnotations(string preamble)
    {
        var text = preamble + Payload + (preamble.Contains("```") ? "\n```" : "");
        var result = StructuredResponseParser.Parse(text, "已有思考", expectStructuredResponse: true);
        Assert.Equal(Expected, result.Answer);
        Assert.Equal("已有思考", result.Reasoning);
        Assert.Equal(AiAnnotationUpdateMode.Append, result.AnnotationUpdateMode);
        var annotation = Assert.Single(result.Annotations);
        Assert.Equal("ref-example", annotation.ReferenceHandle);
        Assert.Equal(0.63, annotation.Y);
        Assert.Equal("#5B85E8", annotation.EffectiveStyle.Color);
    }

    [Fact]
    public void StreamingNeverShowsPreambleOrProtocolAtAnyChunkBoundary()
    {
        var text = "<|tool|>\n现在输出 JSON。\n" + Payload;
        for (var length = 1; length <= text.Length; length++)
        {
            var preview = StructuredResponseParser.GetStreamingAnswerPreview(text[..length]);
            Assert.StartsWith(preview, Expected, StringComparison.Ordinal);
        }
        Assert.Equal(Expected, StructuredResponseParser.GetStreamingAnswerPreview(text));
    }

    [Fact]
    public void OrdinaryProseKeepsItsProtocolExample()
    {
        var text = "下面是格式示例：\n" + Payload;
        var result = StructuredResponseParser.Parse(text);
        Assert.Equal(text, result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public void UnknownProtocolNeverExecutesRecoveredAnnotations()
    {
        var text = "结果：\n" + Payload.Replace("mewu.visual-annotations/1", "mewu.visual-annotations/99");
        var result = StructuredResponseParser.Parse(text, expectStructuredResponse: true);
        Assert.Equal(Expected, result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public void RecoveredAnnotationsStillRejectInvalidGeometry()
    {
        var result = StructuredResponseParser.Parse(Payload.Replace("0.56", "2.0"), expectStructuredResponse: true);
        Assert.Equal(Expected, result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public void DoesNotPromoteNestedProtocolToRoot()
    {
        var text = "{\"example\":\n" + Payload + "}";
        var result = StructuredResponseParser.Parse(text, expectStructuredResponse: true);
        Assert.Empty(result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public void TruncatedAnnotationTailDoesNotBecomePartOfAnswer()
    {
        var text = "结果：\n" + Payload[..Payload.IndexOf("\"geometry\"", StringComparison.Ordinal)];
        var result = StructuredResponseParser.Parse(text, expectStructuredResponse: true);
        Assert.Equal(Expected, result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public void LooseEscapeFallbackNeverExtractsNestedAnswer()
    {
        const string text = """{"payload":{"answer":"nested\@text","annotations":[]}}""";
        var result = StructuredResponseParser.Parse(text, expectStructuredResponse: true);
        Assert.Empty(result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public void CompleteValidProtocolAfterPreambleIsParsedNormally()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            annotationProtocol = "mewu.visual-annotations/1", answer = Expected, annotations = Array.Empty<object>()
        });
        Assert.Equal(Expected, StructuredResponseParser.Parse("结论如下：\n" + json, expectStructuredResponse: true).Answer);
    }
}
