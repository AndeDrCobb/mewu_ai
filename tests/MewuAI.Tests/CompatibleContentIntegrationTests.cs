// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Net;
using System.Net.Http;
using System.Text.Json;
using mewu_ai_Assistant.AI;
using mewu_ai_Assistant.Models;
using Xunit;

namespace MewuAI.Tests;

public sealed class CompatibleContentIntegrationTests
{
    [Theory]
    [InlineData(false, "{\"text\":\"Visible answer\"}")]
    [InlineData(true, "{\"text\":\"Visible answer\"}")]
    [InlineData(false, "{\"content\":\"Visible answer\"}")]
    [InlineData(true, "{\"content\":\"Visible answer\"}")]
    [InlineData(false, "{\"type\":\"output_text\",\"text\":\"Visible answer\"}")]
    [InlineData(true, "{\"type\":\"output_text\",\"text\":\"Visible answer\"}")]
    [InlineData(false, "[{\"type\":\"output_text\",\"text\":\"Visible \"},{\"type\":\"text_delta\",\"text\":\"answer\"}]")]
    [InlineData(true, "[{\"type\":\"output_text\",\"text\":\"Visible \"},{\"type\":\"text_delta\",\"text\":\"answer\"}]")]
    public async Task CompatibleContentReachesTheResultAndLiveAnswer(bool streaming, string contentJson)
    {
        var progress = new RecordingProgress();
        var result = await Provider(Response(JsonSerializer.Deserialize<JsonElement>(contentJson), streaming))
            .SendAsync(new AiRequest { Prompt = "test", StreamingProgress = streaming ? progress : null }, TestContext.Current.CancellationToken);

        Assert.Equal("Visible answer", result.Answer);
        Assert.Empty(result.Reasoning);
        Assert.Null(AiResultValidation.GetEmptyAnswerMessage(result));
        Assert.Equal(streaming ? "Visible answer" : string.Empty, string.Concat(progress.Values.Select(delta => delta.Content)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedBlocksKeepOnlyTheRecognizedAnswerAndSeparateThinking(bool streaming)
    {
        object[] content =
        [
            new { type = "thinking", thinking = new[] { new { type = "text", text = "Check first." } } },
            new { type = "unknown", text = "Do not display." },
            new { text = "Do not infer an array item type." },
            new { type = "output_text", text = "Visible " },
            new { type = "text_delta", text = "answer" }
        ];
        var progress = new RecordingProgress();
        var result = await Provider(Response(content, streaming)).SendAsync(
            new AiRequest { Prompt = "test", StreamingProgress = streaming ? progress : null }, TestContext.Current.CancellationToken);

        Assert.Equal("Visible answer", result.Answer);
        Assert.Equal("Check first.", result.Reasoning);
        if (streaming)
        {
            Assert.Equal(result.Answer, string.Concat(progress.Values.Select(delta => delta.Content)));
            Assert.Equal(result.Reasoning, string.Concat(progress.Values.Select(delta => delta.ReasoningContent)));
        }
    }

    [Theory]
    [InlineData(false, "{\"type\":\"thinking\",\"text\":\"MEWU_OK\"}")]
    [InlineData(true, "{\"type\":\"thinking\",\"text\":\"MEWU_OK\"}")]
    [InlineData(false, "{\"type\":\"extension\",\"content\":\"MEWU_OK\"}")]
    [InlineData(true, "{\"type\":\"extension\",\"content\":\"MEWU_OK\"}")]
    [InlineData(false, "[{\"text\":\"MEWU_OK\"},{\"type\":\"unknown\",\"text\":\"MEWU_OK\"}]")]
    [InlineData(true, "[{\"text\":\"MEWU_OK\"},{\"type\":\"unknown\",\"text\":\"MEWU_OK\"}]")]
    public async Task UnsupportedBlocksCannotBecomeAReplyOrPassTheConnectionProbe(bool streaming, string contentJson)
    {
        var content = JsonSerializer.Deserialize<JsonElement>(contentJson);
        var progress = new RecordingProgress();
        var result = await Provider(Response(content, streaming), kimi: true).SendAsync(
            new AiRequest { Prompt = "test", StreamingProgress = streaming ? progress : null }, TestContext.Current.CancellationToken);

        Assert.Empty(result.Answer);
        Assert.NotNull(AiResultValidation.GetEmptyAnswerMessage(result));
        Assert.Null(result.ContinuationMessage);
        Assert.All(progress.Values, delta => Assert.Empty(delta.Content));
        Assert.False(await Provider(Response(content, streaming: false)).TestConnectionAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThinkingOnlyObjectsRemainAnEmptyAnswerFailure(bool streaming)
    {
        var content = new { type = "thinking", thinking = new[] { new { type = "text", text = "MEWU_OK" } } };
        var progress = new RecordingProgress();
        var result = await Provider(Response(content, streaming), kimi: true).SendAsync(
            new AiRequest { Prompt = "test", StreamingProgress = streaming ? progress : null }, TestContext.Current.CancellationToken);

        Assert.Empty(result.Answer);
        Assert.Equal("MEWU_OK", result.Reasoning);
        Assert.Equal("模型只返回了思考内容，未返回最终回答，请重试", AiResultValidation.GetEmptyAnswerMessage(result));
        Assert.Null(result.ContinuationMessage);
        Assert.All(progress.Values, delta => Assert.Empty(delta.Content));
        Assert.False(await Provider(Response(content, streaming: false)).TestConnectionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DoneMarkerCompletesAnObjectDeltaWithoutDroppingItsText()
    {
        var response = Response(new { text = "Visible answer" }, streaming: true, finish: null) + "data: [DONE]\n\n";
        var progress = new RecordingProgress();
        var result = await Provider(response).SendAsync(new AiRequest { Prompt = "test", StreamingProgress = progress }, TestContext.Current.CancellationToken);
        Assert.Equal("Visible answer", result.Answer);
        Assert.Equal("Visible answer", Assert.Single(progress.Values).Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("length")]
    public async Task ACompatibleTextObjectDoesNotTurnIncompleteStreamsIntoSuccess(string? finish)
    {
        var progress = new RecordingProgress();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Provider(Response(new { text = "Partial answer" }, streaming: true, finish: finish))
            .SendAsync(new AiRequest { Prompt = "test", StreamingProgress = progress }, TestContext.Current.CancellationToken));

        Assert.Equal("Partial answer", Assert.Single(progress.Values).Content);
        Assert.Contains(finish is null ? "中断" : "长度限制", error.Message);
    }

    [Fact]
    public async Task ACompatibleObjectCannotBypassNonStreamingLengthFailure()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Provider(Response(new { type = "output_text", text = "Partial answer" }, streaming: false, finish: "length"))
            .SendAsync(new AiRequest { Prompt = "test" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompletionPredicateSeesAssembledTextButNeverUnknownBlocks()
    {
        const string raw = "{\"answer\":\"Complete\",\"annotations\":[]}";
        var split = raw.IndexOf("Complete", StringComparison.Ordinal);
        var response = Response(new { type = "unknown", text = raw }, streaming: true, finish: null)
            + Response(new { text = raw[..split] }, streaming: true, finish: null)
            + Response(new { content = raw[split..] }, streaming: true, finish: "length");
        var observed = new List<string>();
        var result = await Provider(response).SendAsync(new AiRequest
        {
            Prompt = "test",
            ExpectStructuredResponse = true,
            StreamingProgress = new RecordingProgress(),
            StreamingCompletionPredicate = value => { observed.Add(value); return value == raw; }
        }, TestContext.Current.CancellationToken);

        Assert.Equal("Complete", result.Answer);
        Assert.Equal(new[] { raw[..split], raw }, observed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KimiContinuationPreservesExactStructuredTextFromCompatibleObjects(bool streaming)
    {
        const string raw = "  {\"answer\":\"Visible answer\",\"annotations\":[]}\n";
        const string reasoning = "  original reasoning\n";
        var split = raw.IndexOf("Visible", StringComparison.Ordinal);
        var response = streaming
            ? Response(new { text = raw[..split] }, streaming: true, finish: null, reasoning: "  ")
                + Response(new[] { new { type = "output_text", text = raw[split..] } }, streaming: true, reasoning: "original reasoning\n")
            : Response(new { type = "output_text", text = raw }, streaming: false, reasoning: reasoning);
        var result = await Provider(response, kimi: true).SendAsync(new AiRequest
        {
            Prompt = "first",
            ExpectStructuredResponse = true,
            StreamingProgress = streaming ? new RecordingProgress() : null
        }, TestContext.Current.CancellationToken);

        Assert.Equal("Visible answer", result.Answer);
        Assert.Equal("original reasoning", result.Reasoning);
        var continuation = Assert.IsType<AiMessage>(result.ContinuationMessage);
        Assert.Equal(raw, continuation.ProviderContent);
        Assert.Equal(reasoning, continuation.ReasoningContent);

        var inspected = false;
        await Provider(Response("Next answer", streaming: false), kimi: true, inspect: body =>
        {
            inspected = true;
            var messages = body.GetProperty("messages");
            Assert.Equal(3, messages.GetArrayLength());
            Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
            Assert.Equal(raw, messages[1].GetProperty("content").GetString());
            Assert.Equal(reasoning, messages[1].GetProperty("reasoning_content").GetString());
        }).SendAsync(new AiRequest { Prompt = "next", History = [new("user", "first"), continuation] }, TestContext.Current.CancellationToken);
        Assert.True(inspected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedTransportRecoversAnAnswerFromMalformedAnnotationJson(bool streaming)
    {
        const string raw = "{\"answer\":\"Complete answer\",\"annotations\":[oops";
        var result = await Provider(Response(new { type = "output_text", text = raw }, streaming, reasoning: "check"))
            .SendAsync(new AiRequest { Prompt = "question", ExpectStructuredResponse = true,
                StreamingProgress = streaming ? new RecordingProgress() : null }, TestContext.Current.CancellationToken);
        Assert.Equal("Complete answer", result.Answer);
        Assert.Equal("check", result.Reasoning);
        Assert.Empty(result.Annotations);
        Assert.Equal(AiAnnotationUpdateMode.Preserve, result.AnnotationUpdateMode);
        Assert.Null(AiResultValidation.GetEmptyAnswerMessage(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("length")]
    public async Task RecoverableAnswerCannotBypassAnIncompleteTransport(string? finish)
    {
        const string raw = "{\"answer\":\"Complete answer\",\"annotations\":[oops";
        var provider = Provider(Response(new { text = raw }, streaming: true, finish: finish));
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.SendAsync(new AiRequest
        {
            Prompt = "question", ExpectStructuredResponse = true, StreamingProgress = new RecordingProgress()
        }, TestContext.Current.CancellationToken));
    }

    private static string Response(object content, bool streaming, string? finish = "stop", string? reasoning = null)
    {
        var message = new Dictionary<string, object?> { ["content"] = content };
        if (reasoning is not null) message["reasoning_content"] = reasoning;
        var choice = new Dictionary<string, object?> { [streaming ? "delta" : "message"] = message, ["finish_reason"] = finish };
        var json = JsonSerializer.Serialize(new { choices = new[] { choice } });
        return streaming ? "data: " + json + "\n\n" : json;
    }

    private static OpenAiCompatibleProvider Provider(string response, bool kimi = false, Action<JsonElement>? inspect = null) => new(
        new AiProviderSettings
        {
            Type = "OpenAICompatible",
            BaseUrl = kimi ? "https://api.moonshot.cn/v1" : "https://compatible.invalid/v1",
            Model = kimi ? "kimi-k3" : "test-model"
        },
        "synthetic-test-key",
        async (request, _, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            inspect?.Invoke(body.RootElement);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
        },
        _ => TimeSpan.FromSeconds(10));

    private sealed class RecordingProgress : IProgress<AiStreamDelta>
    {
        internal List<AiStreamDelta> Values { get; } = [];
        public void Report(AiStreamDelta value) => Values.Add(value);
    }
}
