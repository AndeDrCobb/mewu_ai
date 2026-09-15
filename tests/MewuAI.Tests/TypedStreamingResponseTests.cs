// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Text.Json;
using mewu_ai_Assistant.AI;
using mewu_ai_Assistant.Models;
using Xunit;

namespace MewuAI.Tests;

public sealed class TypedStreamingResponseTests
{
    [Fact]
    public void MistralTransitionPreservesThinkingAndFirstTextBeforeStringAnswerAndFinish()
    {
        string[] events=
        [
            """data: {"choices":[{"delta":{"content":[{"type":"thinking","thinking":[{"type":"text","text":"First, "}]}]}}]}""",
            """data: {"choices":[{"delta":{"content":[{"type":"thinking","thinking":[{"type":"text","text":"compare."}]},{"type":"text","text":"The result "}]}}]}""",
            """data: {"choices":[{"delta":{"content":"is 42."},"finish_reason":"stop"}]}"""
        ];
        var progress=new List<AiStreamDelta>();
        var accumulator=new StreamingResponseAccumulator();
        var completed=false;
        foreach(var line in events)
        {
            Assert.True(StreamingResponseParser.TryParse(line,out var delta,out var done,out var truncated));
            Assert.False(truncated);
            Assert.False(delta.ReasoningIsCumulative);
            completed=accumulator.Accept(delta,done,new RecordingProgress(progress),null);
        }
        Assert.True(completed);
        Assert.Equal("The result is 42.",accumulator.BuildResult().Answer);
        Assert.Equal("First, compare.",accumulator.BuildResult().Reasoning);
        Assert.Collection(progress,
            delta=>Assert.Equal(new AiStreamDelta("","First, "),delta),
            delta=>Assert.Equal(new AiStreamDelta("The result ","compare."),delta),
            delta=>Assert.Equal(new AiStreamDelta("is 42.",""),delta));
    }

    [Theory]
    [InlineData("stop",false)]
    [InlineData("length",true)]
    public void TypedFinalEventKeepsBothChannelsWhenReportingFinish(string finish,bool truncated)
    {
        var line="data: "+JsonSerializer.Serialize(new
        {
            choices=new[]
            {
                new
                {
                    delta=new {content=new object[]
                    {
                        new {type="thinking",thinking=new[]{new {type="text",text="Final check."}}},
                        new {type="text",text="Last answer fragment."}
                    }},
                    finish_reason=finish
                }
            }
        });
        Assert.True(StreamingResponseParser.TryParse(line,out var delta,out var done,out var limited));
        Assert.True(done);
        Assert.Equal(truncated,limited);
        Assert.Equal("Last answer fragment.",delta.Content);
        Assert.Equal("Final check.",delta.ReasoningContent);
    }

    [Fact]
    public void UnknownAndMalformedChunksDoNotBecomeVisibleTextOrDiscardValidSiblings()
    {
        const string line="""
            data: {"choices":[{"delta":{"content":[null,"not a chunk",42,{"text":"untyped"},{"type":"unknown","text":"unknown"},{"type":"text","text":{}},{"type":"thinking","thinking":"not an array"},{"type":"thinking","thinking":[null,{"text":"untyped"},{"type":"text","text":5},{"type":"text","text":"check"},{"type":"thinking","thinking":[{"type":"text","text":"nested"}]}]},{"type":"text","text":"valid"}]}}]}
            """;
        Assert.True(StreamingResponseParser.TryParse(line,out var delta,out var done));
        Assert.False(done);
        Assert.Equal("valid",delta.Content);
        Assert.Equal("check",delta.ReasoningContent);
    }

    [Theory]
    [InlineData("reasoning_content")]
    [InlineData("thinking_content")]
    [InlineData("reasoning")]
    public void ExplicitReasoningChannelPreventsDuplicatingTypedThinking(string field)
    {
        var body=new Dictionary<string,object>
        {
            [field]="authoritative",
            ["content"]=new object[]
            {
                new {type="thinking",thinking=new[]{new {type="text",text="duplicate"}}},
                new {type="text",text="answer"}
            },
            ["reasoning_details"]=new[]{new {text="cumulative duplicate"}}
        };
        var line="data: "+JsonSerializer.Serialize(new {choices=new[]{new {delta=body}}});
        Assert.True(StreamingResponseParser.TryParse(line,out var delta,out _));
        Assert.Equal("authoritative",delta.ReasoningContent);
        Assert.Equal("answer",delta.Content);
        Assert.False(delta.ReasoningIsCumulative);
    }

    [Fact]
    public void TypedThinkingTakesPrecedenceOverCumulativeReasoningDetails()
    {
        const string line="""data: {"choices":[{"delta":{"content":[{"type":"thinking","thinking":[{"type":"text","text":"typed"}]}],"reasoning_details":[{"text":"duplicate"}]}}]}""";
        Assert.True(StreamingResponseParser.TryParse(line,out var delta,out _));
        Assert.Equal("typed",delta.ReasoningContent);
        Assert.False(delta.ReasoningIsCumulative);
    }

    [Fact]
    public void MiniMaxStringAndCumulativeDetailsStillUseExistingAccumulatorSemantics()
    {
        string[] events=
        [
            """data: {"choices":[{"delta":{"content":"A","reasoning_details":[{"text":"check"}]}}]}""",
            """data: {"choices":[{"delta":{"content":"Answer","reasoning_details":[{"text":"checked"}]},"finish_reason":"stop"}]}"""
        ];
        var progress=new List<AiStreamDelta>();
        var accumulator=new StreamingResponseAccumulator(contentIsCumulative:true);
        foreach(var line in events)
        {
            Assert.True(StreamingResponseParser.TryParse(line,out var delta,out var done));
            Assert.True(delta.ReasoningIsCumulative);
            accumulator.Accept(delta,done,new RecordingProgress(progress),null);
        }
        Assert.Equal("Answer",accumulator.BuildResult().Answer);
        Assert.Equal("checked",accumulator.BuildResult().Reasoning);
        Assert.Equal(new AiStreamDelta("nswer","ed"),progress[1]);
    }

    [Fact]
    public void GroqReasoningStringIsSeparateFromTheAnswer()
    {
        const string line="""data: {"choices":[{"delta":{"content":"answer","reasoning":"check"}}]}""";
        Assert.True(StreamingResponseParser.TryParse(line,out var delta,out _));
        Assert.Equal(new AiStreamDelta("answer","check"),delta);
    }

    [Theory]
    [InlineData("""{"text":"answer"}""")]
    [InlineData("""{"content":"answer"}""")]
    [InlineData("""{"text":"","content":"answer"}""")]
    [InlineData("""{"text":"answer","content":"duplicate"}""")]
    public void SingleUntypedGatewayWrapperRetainsItsPlainAnswer(string contentJson)
    {
        var delta=ParseCompletedContent(contentJson);
        Assert.Equal("answer",delta.Content);
        Assert.Empty(delta.ReasoningContent);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("output_text")]
    [InlineData("text_delta")]
    public void DeclaredTextBlocksReadTextForBothObjectAndArray(string type)
    {
        var chunk=JsonSerializer.Serialize(new {type,text=" answer ",content="not text",delta="not text"});
        foreach(var contentJson in new[]{chunk,"["+chunk+"]"})
        {
            var delta=ParseCompletedContent(contentJson);
            Assert.Equal(" answer ",delta.Content);
            Assert.Empty(delta.ReasoningContent);
        }
    }

    [Theory]
    [InlineData("text")]
    [InlineData("output_text")]
    [InlineData("text_delta")]
    public void DeclaredTextBlocksNeverFallBackToContentOrDelta(string type)
    {
        var chunk=JsonSerializer.Serialize(new {type,content="wrong field",delta="wrong field"});
        foreach(var contentJson in new[]{chunk,"["+chunk+"]"})
        {
            var delta=ParseCompletedContent(contentJson);
            Assert.Empty(delta.Content);
            Assert.Empty(delta.ReasoningContent);
        }
    }

    [Theory]
    [InlineData("\"unknown\"")]
    [InlineData("\"thinking\"")]
    [InlineData("\"thinking_delta\"")]
    [InlineData("\"reasoning\"")]
    [InlineData("\"reasoning_text\"")]
    [InlineData("\"tool_use\"")]
    [InlineData("\"input_json_delta\"")]
    [InlineData("\"image_url\"")]
    [InlineData("\"response.output_text.delta\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    public void UnknownOrMalformedDeclaredTypesNeverBecomePlainAnswers(string typeJson)
    {
        var chunk=$$"""{"type":{{typeJson}},"text":"must stay hidden","content":"must stay hidden"}""";
        foreach(var contentJson in new[]{chunk,"["+chunk+",{\"type\":\"text\",\"text\":\"valid\"}]"})
        {
            var delta=ParseCompletedContent(contentJson);
            Assert.Equal(contentJson[0]=='['?"valid":string.Empty,delta.Content);
            Assert.Empty(delta.ReasoningContent);
        }
    }

    [Fact]
    public void ThinkingObjectDoesNotExposeTextAndPreservesOnlyTypedThinkingChildren()
    {
        const string contentJson="""
            {"type":"thinking","text":"not an answer","content":"not an answer","thinking":[{"type":"text","text":"check"},{"type":"unknown","text":"hidden"},{"text":"hidden"}]}
            """;
        var delta=ParseCompletedContent(contentJson);
        Assert.Empty(delta.Content);
        Assert.Equal("check",delta.ReasoningContent);
        Assert.False(delta.ReasoningIsCumulative);
    }

    [Theory]
    [InlineData("reasoning_content")]
    [InlineData("thinking_content")]
    [InlineData("reasoning")]
    public void ExplicitReasoningStillOverridesSingleThinkingObject(string field)
    {
        var body=new Dictionary<string,object>
        {
            [field]="authoritative",
            ["content"]=new {type="thinking",text="not an answer",thinking=new[]{new {type="text",text="duplicate"}}},
            ["reasoning_details"]=new[]{new {text="duplicate"}}
        };
        var line="data: "+JsonSerializer.Serialize(new {choices=new[]{new {delta=body,finish_reason="stop"}}});
        Assert.True(StreamingResponseParser.TryParse(line,out var delta,out var done));
        Assert.True(done);
        Assert.Empty(delta.Content);
        Assert.Equal("authoritative",delta.ReasoningContent);
        Assert.False(delta.ReasoningIsCumulative);
    }

    private static AiStreamDelta ParseCompletedContent(string contentJson)
    {
        var line=$$"""data: {"choices":[{"delta":{"content":{{contentJson}}},"finish_reason":"stop"}]}""";
        Assert.True(StreamingResponseParser.TryParse(line,out var delta,out var done,out var truncated));
        Assert.True(done);
        Assert.False(truncated);
        return delta;
    }

    private sealed class RecordingProgress(List<AiStreamDelta> values):IProgress<AiStreamDelta>
    {
        public void Report(AiStreamDelta value)=>values.Add(value);
    }
}
