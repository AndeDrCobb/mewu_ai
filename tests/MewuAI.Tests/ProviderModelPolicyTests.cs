// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Net;
using System.Net.Http;
using System.Text.Json;
using mewu_ai_Assistant.AI;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ProviderModelPolicyTests
{
    [Theory]
    [InlineData("https://api.openai.com/v1", "gpt-6-astra", true, false)]
    [InlineData("https://api.openai.com/v1", "gpt-5.6-sol", true, false)]
    [InlineData("https://api.openai.com/v1", "gpt-5.6-terra", true, false)]
    [InlineData("https://api.openai.com/v1", "gpt-5.6-luna", true, false)]
    [InlineData("https://api.openai.com/v1", "o3", true, false)]
    [InlineData("https://api.openai.com/v1", "gpt-4o", true, false)]
    [InlineData("https://api.openai.com/v1", "o3-mini", false, false)]
    [InlineData("https://api.openai.com/v1", "o3-mini-2025-01-31", false, false)]
    [InlineData("https://api.openai.com/v1", "gpt-4o-search-preview", false, false)]
    [InlineData("https://openrouter.ai/api/v1", "openai/gpt-4o-search-preview-2025-03-11", false, false)]
    [InlineData("https://api.anthropic.com/v1", "claude-fable-5-1", true, false)]
    [InlineData("https://api.anthropic.com/v1", "claude-opus-5", true, false)]
    [InlineData("https://api.anthropic.com/v1", "claude-sonnet-5", true, false)]
    [InlineData("https://api.anthropic.com/v1", "claude-haiku-4-5-20251001", true, false)]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai", "gemini-3.8-flash", true, false)]
    [InlineData("https://api.x.ai/v1", "grok-4.6", true, false)]
    [InlineData("https://api.deepseek.com/v1", "deepseek-flash", true, false)]
    [InlineData("https://api.deepseek.com/v1", "deepseek-v4-flash", true, false)]
    [InlineData("https://api.deepseek.com/v1", "deepseek-v4-pro", false, false)]
    [InlineData("https://proxy.invalid/v1", "deepseek-v4-flash", false, false)]
    [InlineData("https://api.together.xyz/v1", "deepseek-ai/DeepSeek-V4.1-Flash", true, false)]
    [InlineData("https://api.moonshot.cn/v1", "kimi-k3", true, true)]
    [InlineData("https://api.moonshot.ai/v1", "kimi-k2.7-code-highspeed", true, true)]
    [InlineData("https://api.moonshot.cn/v1", "kimi-k2.6", true, true)]
    [InlineData("https://api.moonshot.cn/v1", "kimi-k2", false, false)]
    [InlineData("https://api.moonshot.cn/v1", "moonshot-v1-128k-vision-preview", true, false)]
    [InlineData("https://api.minimax.cn/v1", "MiniMax-M3", true, true)]
    [InlineData("https://api.minimaxi.com/v1", "MiniMax-M3", true, true)]
    [InlineData("https://api.minimax.io/v1", "MiniMax-M3", true, true)]
    [InlineData("https://api.minimax.cn.evil.invalid/v1", "MiniMax-M3", false, false)]
    [InlineData("https://ark.cn-beijing.volces.com/api/v3", "glm-5-2-260617", false, false)]
    [InlineData("https://ark.cn-beijing.volces.com/api/v3", "glm-5-3-flash", true, true)]
    [InlineData("https://api.groq.com/openai/v1", "openai/gpt-oss-120b", false, false)]
    [InlineData("https://api.groq.com/openai/v1", "qwen/qwen3.8-27b", true, false)]
    [InlineData("https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen3.8-max", true, true)]
    [InlineData("https://workspace123.cn-beijing.maas.aliyuncs.com/compatible-mode/v1", "qwen3.8-max", true, true)]
    [InlineData("https://workspace123.cn-beijing.maas.aliyuncs.com/other/v1", "qwen3.8-max", true, false)]
    [InlineData("https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen3.7-max", false, false)]
    [InlineData("https://api.together.xyz/v1", "Qwen/Qwen3.8-2.4T-A95B", true, false)]
    [InlineData("https://api.mistral.ai/v1", "mistral-medium-3-5", true, false)]
    [InlineData("https://api.mistral.ai/v1", "mistral-small-2603", true, false)]
    [InlineData("https://open.bigmodel.cn/api/paas/v4", "glm-5.3-flash", true, false)]
    [InlineData("https://api.z.ai/api/paas/v4", "glm-5.3-flash", true, false)]
    [InlineData("https://api.z.ai/api/paas/v4", "glm-5.3", false, false)]
    [InlineData("https://proxy.invalid/v1", "gemini-embedding-001", false, false)]
    [InlineData("https://proxy.invalid/v1", "gpt-4o-audio-preview", false, false)]
    [InlineData("https://proxy.invalid/v1", "unknown-model", false, false)]
    public void CapabilitiesAreScopedToTheTransportAndVerifiedFamily(string endpoint, string model, bool image, bool video)
    {
        var capabilities = ProviderModelPolicy.GetCapabilities(Settings(endpoint, model));
        Assert.Equal(image, capabilities.SupportsImage);
        Assert.Equal(video, capabilities.SupportsVideo);
        Assert.Equal(image, capabilities.MaxImageSize > 0);
        Assert.Equal(video, capabilities.MaxVideoSize > 0);
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", "gpt-6-astra", "max_completion_tokens", 128000)]
    [InlineData("https://api.openai.com/v1", "gpt-5.6-sol", "max_completion_tokens", 128000)]
    [InlineData("https://api.minimax.cn/v1", "MiniMax-M3", "max_completion_tokens", 524288)]
    [InlineData("https://api.deepseek.com/v1", "deepseek-flash", "max_tokens", 393216)]
    [InlineData("https://api.deepseek.com/v1", "deepseek-v4-pro", "max_tokens", 393216)]
    public async Task KnownMaximumUsesTheProvidersFieldWithoutForcingSampling(string endpoint, string model, string field, int maximum)
    {
        await Send(Settings(endpoint, model), new AiRequest { Prompt = "test", UseModelMaximumOutputTokens = true }, (http, body) =>
        {
            Assert.Equal(maximum, body.GetProperty(field).GetInt32());
            Assert.False(body.TryGetProperty(field == "max_tokens" ? "max_completion_tokens" : "max_tokens", out _));
            Assert.False(body.TryGetProperty("temperature", out _));
            Assert.False(body.TryGetProperty("top_p", out _));
            Assert.False(body.TryGetProperty("reasoning_effort", out _));
            if (model != "MiniMax-M3") Assert.False(body.TryGetProperty("thinking", out _));
        });
    }

    [Theory]
    [InlineData("https://proxy.invalid/v1", "gpt-6-astra")]
    [InlineData("https://api.openai.com/v1", "unknown-model")]
    [InlineData("https://api.openai.com:8443/v1", "gpt-6-astra")]
    [InlineData("https://api.deepseek.com.attacker.invalid/v1", "deepseek-flash")]
    [InlineData("https://api.moonshot.cn/v1", "kimi-k3")]
    public async Task UnknownOrSharedContextMaximumKeepsTheBackendDefault(string endpoint, string model)
    {
        await Send(Settings(endpoint, model), new AiRequest { Prompt = "test", UseModelMaximumOutputTokens = true, DisableReasoning = true }, (http, body) =>
        {
            foreach (var name in new[] { "max_tokens", "max_completion_tokens", "temperature", "top_p", "thinking", "reasoning_effort" })
                Assert.False(body.TryGetProperty(name, out _), name);
        });
    }

    [Theory]
    [InlineData("deepseek-flash", false, false)]
    [InlineData("deepseek-flash", true, false)]
    [InlineData("deepseek-flash", false, true)]
    [InlineData("deepseek-flash", true, true)]
    [InlineData("deepseek-v4-pro", false, false)]
    [InlineData("deepseek-v4-pro", true, false)]
    [InlineData("deepseek-v4-pro", false, true)]
    [InlineData("deepseek-v4-pro", true, true)]
    public async Task OfficialDeepSeekDisablesThinkingOnlyWhenRequested(string model, bool streaming, bool disableReasoning)
    {
        var response = streaming
            ? "data: {\"choices\":[{\"delta\":{\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}\n\n"
            : "{\"choices\":[{\"message\":{\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}";
        var result = await Send(Settings("https://api.deepseek.com/v1", model), new AiRequest
        {
            Prompt = "test",
            DisableReasoning = disableReasoning,
            StreamingProgress = streaming ? new CollectProgress([]) : null
        }, (http, body) =>
        {
            Assert.Equal(streaming, body.GetProperty("stream").GetBoolean());
            Assert.Equal(disableReasoning, body.TryGetProperty("thinking", out var thinking));
            if (disableReasoning) Assert.Equal("disabled", thinking.GetProperty("type").GetString());
            Assert.False(body.TryGetProperty("reasoning_effort", out _));
        }, response);
        Assert.Equal("OK", result.Answer);
    }

    [Theory]
    [InlineData("https://proxy.invalid/v1")]
    [InlineData("https://api.deepseek.com.attacker.invalid/v1")]
    [InlineData("https://api.deepseek.com:8443/v1")]
    public async Task DeepSeekCompatibleEndpointsDoNotInheritOfficialThinkingParameters(string endpoint)
    {
        await Send(Settings(endpoint, "deepseek-flash"), new AiRequest { Prompt = "test", DisableReasoning = true }, (http, body) =>
        {
            Assert.False(body.TryGetProperty("thinking", out _));
            Assert.False(body.TryGetProperty("reasoning_effort", out _));
        });
    }

    [Theory]
    [InlineData("gpt-6-astra")]
    [InlineData("gpt-5.1")]
    [InlineData("o3")]
    public async Task OpenAiDoesNotAcquireAnUnsupportedUniversalReasoningEffort(string model)
    {
        await Send(Settings("https://api.openai.com/v1", model), new AiRequest { Prompt = "translate", DisableReasoning = true }, (http, body) =>
        {
            Assert.False(body.TryGetProperty("thinking", out _));
            Assert.False(body.TryGetProperty("reasoning_effort", out _));
        });
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", "gpt-6-astra", "max_completion_tokens")]
    [InlineData("https://api.openai.com/v1", "o3", "max_completion_tokens")]
    [InlineData("https://proxy.invalid/v1", "custom", "max_tokens")]
    [InlineData("https://api.minimax.cn/v1", "MiniMax-M3", "max_completion_tokens")]
    [InlineData("https://api.moonshot.cn/v1", "kimi-k3", "max_completion_tokens")]
    public async Task ExplicitUtilityBudgetWinsOverAutomaticMaximum(string endpoint, string model, string field)
    {
        await Send(Settings(endpoint, model), new AiRequest { Prompt = "test", MaxOutputTokens = 32, UseModelMaximumOutputTokens = true },
            (http, body) => Assert.Equal(32, body.GetProperty(field).GetInt32()));
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", "gpt-6-astra", "temperature")]
    [InlineData("https://api.openai.com/v1", "gpt-6-astra", "top_p")]
    [InlineData("https://api.moonshot.cn/v1", "kimi-k3", "temperature")]
    [InlineData("https://api.moonshot.ai/v1", "kimi-k3", "top_p")]
    public void UnsupportedExplicitSamplingFailsBeforeSendingAndPreservesSavedValue(string endpoint, string model, string parameter)
    {
        var settings = Settings(endpoint, model);
        settings.RequestParameters = ProviderRequestParameterPolicy.Parse(JsonSerializer.Serialize(new Dictionary<string, double> { [parameter] = .7 }));
        var error = Assert.Throws<InvalidOperationException>(() => new OpenAiCompatibleProvider(settings, "test-key"));
        Assert.Contains(parameter, error.Message);
        Assert.Equal(.7, settings.RequestParameters[parameter].GetDouble());
    }

    [Fact]
    public async Task ExplicitGenericSamplingIsNotSilentlyDiscarded()
    {
        var settings = Settings("https://proxy.invalid/v1", "gpt-6-astra");
        settings.RequestParameters = ProviderRequestParameterPolicy.Parse("{\"temperature\":0.7,\"top_p\":0.9}");
        await Send(settings, new AiRequest { Prompt = "test" }, (http, body) =>
        {
            Assert.Equal(.7, body.GetProperty("temperature").GetDouble());
            Assert.Equal(.9, body.GetProperty("top_p").GetDouble());
        });
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", "gpt-6-astra", "default")]
    [InlineData("https://api.minimax.cn/v1", "MiniMax-M3", "standard")]
    [InlineData("https://proxy.invalid/v1", "custom", "standard")]
    public async Task StandardServiceTierUsesTheOfficialOpenAiNameWithoutChangingSavedSettings(string endpoint, string model, string expected)
    {
        var settings = Settings(endpoint, model);
        settings.RequestParameters = ProviderRequestParameterPolicy.Parse("{\"service_tier\":\"standard\"}");
        await Send(settings, new AiRequest { Prompt = "test" }, (http, body) => Assert.Equal(expected, body.GetProperty("service_tier").GetString()));
        Assert.Equal("standard", settings.RequestParameters["service_tier"].GetString());
    }

    [Theory]
    [InlineData("gpt-5.5-pro")]
    [InlineData("gpt-5.3-codex")]
    public void OfficialResponsesOnlyModelsGiveAnActionableErrorBeforeSending(string model)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new OpenAiCompatibleProvider(Settings("https://api.openai.com/v1", model), "test-key"));
        Assert.Contains("Responses", error.Message);
        Assert.Contains("Chat Completions", error.Message);
        _ = new OpenAiCompatibleProvider(Settings("https://openrouter.ai/api/v1", model), "test-key");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnthropicVersionHeaderIsAddedOnceWithoutChangingAuthentication(bool customized)
    {
        var settings = Settings("https://api.anthropic.com/v1", "claude-sonnet-5");
        if (customized) settings.CustomHeaders["Anthropic-Version"] = "custom-version";
        await Send(settings, new AiRequest { Prompt = "test" }, (http, body) =>
        {
            Assert.Equal("https://api.anthropic.com/v1/chat/completions", http.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", http.Headers.Authorization!.Scheme);
            Assert.Equal(customized ? "custom-version" : "2023-06-01", Assert.Single(http.Headers.GetValues("anthropic-version")));
        });
    }

    [Fact]
    public async Task GenericMiniMaxOfficialConnectionConsumesCumulativeStreamOnce()
    {
        var chunks = new List<AiStreamDelta>();
        var result = await Send(Settings("https://api.minimax.cn/v1", "MiniMax-M3"),
            new AiRequest { Prompt = "test", StreamingProgress = new CollectProgress(chunks) }, (_, _) => { },
            "data: {\"choices\":[{\"delta\":{\"content\":\"one \"},\"finish_reason\":null}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"one two\"},\"finish_reason\":\"stop\"}]}\n\n");
        Assert.Equal("one two", result.Answer);
        Assert.Equal("one two", string.Concat(chunks.Select(item => item.Content)));
    }

    [Fact]
    public async Task OpenRouterReasoningDetailsKeepRepeatedIncrementalWords()
    {
        var chunks = new List<AiStreamDelta>();
        var result = await Send(Settings("https://openrouter.ai/api/v1", "openai/gpt-6-astra"),
            new AiRequest { Prompt = "test", StreamingProgress = new CollectProgress(chunks) }, (_, _) => { },
            "data: {\"choices\":[{\"delta\":{\"reasoning_details\":[{\"type\":\"reasoning.text\",\"text\":\"again \"}]},\"finish_reason\":null}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"answer\",\"reasoning_details\":[{\"type\":\"reasoning.text\",\"text\":\"again \"}]},\"finish_reason\":\"stop\"}]}\n\n");
        Assert.Equal("again again", result.Reasoning);
        Assert.Equal("again again ", string.Concat(chunks.Select(item => item.ReasoningContent)));
    }

    [Fact]
    public async Task NonStreamingTypedMistralContentPreservesBothReasoningAndAnswer()
    {
        var result = await Send(Settings("https://api.mistral.ai/v1", "mistral-medium-3-5"), new AiRequest { Prompt = "test" }, (_, _) => { },
            "{\"choices\":[{\"message\":{\"content\":[{\"type\":\"thinking\",\"thinking\":[{\"type\":\"text\",\"text\":\"checked\"}]},{\"type\":\"text\",\"text\":\"answer\"}]},\"finish_reason\":\"stop\"}]}");
        Assert.Equal("answer", result.Answer);
        Assert.Equal("checked", result.Reasoning);
    }

    [Theory]
    [InlineData("https://api.moonshot.cn/v1", "kimi-k3", false)]
    [InlineData("https://api.moonshot.ai/v1", "kimi-k2.6", false)]
    [InlineData("https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen3.8-max", false)]
    [InlineData("https://workspace123.cn-beijing.maas.aliyuncs.com/compatible-mode/v1", "qwen3.8-max", false)]
    [InlineData("https://api.minimax.cn/v1", "MiniMax-M3", true)]
    [InlineData("https://ark.cn-beijing.volces.com/api/v3", "doubao-seed-2-1-pro-260628", true)]
    public async Task VideoPayloadUsesOnlyFieldsAcceptedByItsEndpoint(string endpoint, string model, bool fps)
    {
        byte[] bytes = [1, 2, 3, 4];
        await Send(Settings(endpoint, model), new AiRequest { Prompt = "test", Attachments = [new(AiAttachmentType.Video, "video/mp4", bytes, Duration: TimeSpan.FromSeconds(3))] }, (http, body) =>
        {
            var part = body.GetProperty("messages")[0].GetProperty("content")[1];
            Assert.Equal("video_url", part.GetProperty("type").GetString());
            var video = part.GetProperty("video_url");
            Assert.Equal("data:video/mp4;base64,AQIDBA==", video.GetProperty("url").GetString());
            Assert.Equal(fps, video.TryGetProperty("fps", out _));
        });
        Assert.All(bytes, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task ImageCountIsRejectedBeforeNetworkAndSensitiveBuffersAreCleared()
    {
        var images = Enumerable.Range(0, 4).Select(_ => new AiAttachment(AiAttachmentType.Image, "image/png", new byte[] { 1 })).ToList();
        var provider = new OpenAiCompatibleProvider(Settings("https://api.groq.com/openai/v1", "qwen/qwen3.8-27b"), "test-key",
            (_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected network request"), _ => TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SendAsync(new AiRequest { Prompt = "test", Attachments = images }, TestContext.Current.CancellationToken));
        Assert.All(images, image => Assert.Equal(0, image.Data![0]));
    }

    [Fact]
    public async Task QwenVideoMinimumIsCheckedBeforeSending()
    {
        byte[] video = [1];
        var provider = new OpenAiCompatibleProvider(Settings("https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen3.8-max"), "test-key",
            (_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected network request"), _ => TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SendAsync(new AiRequest { Prompt = "test",
            Attachments = [new(AiAttachmentType.Video, "video/mp4", video, Duration: TimeSpan.FromSeconds(1))] }, TestContext.Current.CancellationToken));
        Assert.Equal(0, video[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KimiContinuationKeepsRawPayloadWhileDisplayingTheParsedAnswer(bool streaming)
    {
        const string raw = " {\"answer\":\"Visible answer\",\"annotations\":[]} ";
        const string reasoning = "  original reasoning\n";
        var response = streaming
            ? "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = raw, reasoning_content = reasoning }, finish_reason = "stop" } } }) + "\n\n"
            : JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = raw, reasoning_content = reasoning }, finish_reason = "stop" } } });
        var settings = Settings("https://api.moonshot.cn/v1", "kimi-k3");
        var result = await Send(settings, new AiRequest { Prompt = "first", ExpectStructuredResponse = true,
            StreamingProgress = streaming ? new CollectProgress([]) : null }, (_, _) => { }, response);
        Assert.Equal("Visible answer", result.Answer);
        var assistant = Assert.IsType<AiMessage>(result.ContinuationMessage);
        Assert.Equal(raw, assistant.ProviderContent);
        Assert.Equal(reasoning, assistant.ReasoningContent);

        await Send(settings, new AiRequest { Prompt = "next", History = [new("system", "instructions"),
            new("user", "old disk question"), new("assistant", "old disk answer"), new("user", "first"), assistant] }, (http, body) =>
        {
            var messages = body.GetProperty("messages");
            Assert.Equal(4, messages.GetArrayLength());
            Assert.Equal("instructions", messages[0].GetProperty("content").GetString());
            Assert.Equal("first", messages[1].GetProperty("content").GetString());
            Assert.Equal(raw, messages[2].GetProperty("content").GetString());
            Assert.Equal(reasoning, messages[2].GetProperty("reasoning_content").GetString());
            Assert.DoesNotContain("old disk", messages.GetRawText(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task KimiDoesNotInventMissingReasoningAndOtherProvidersNeverReceiveContinuationFields()
    {
        var settings = Settings("https://api.moonshot.ai/v1", "kimi-k2.7-code");
        var result = await Send(settings, new AiRequest { Prompt = "first" }, (_, _) => { });
        var assistant = Assert.IsType<AiMessage>(result.ContinuationMessage);
        Assert.Equal("OK", assistant.ProviderContent);
        Assert.Null(assistant.ReasoningContent);
        await Send(settings, new AiRequest { Prompt = "next", History = [new("user", "first"), assistant] }, (http, body) =>
            Assert.False(body.GetProperty("messages")[1].TryGetProperty("reasoning_content", out _)));

        var retained = assistant with { ProviderContent = "private raw content", ReasoningContent = "private reasoning" };
        var generic = await Send(Settings("https://proxy.invalid/v1", "custom"), new AiRequest { Prompt = "next", History = [new("user", "first"), retained] }, (http, body) =>
        {
            var reply = body.GetProperty("messages")[1];
            Assert.Equal("OK", reply.GetProperty("content").GetString());
            Assert.False(reply.TryGetProperty("reasoning_content", out _));
            Assert.DoesNotContain("private", body.GetRawText(), StringComparison.Ordinal);
        });
        Assert.Null(generic.ContinuationMessage);
    }

    [Fact]
    public void ProviderImageLimitsAreIndependentOfVideoAndBodyLimits()
    {
        var claude = Settings("https://api.anthropic.com/v1", "claude-sonnet-5");
        Assert.Equal(7_864_320, ProviderModelPolicy.GetCapabilities(claude).MaxImageSize);
        Assert.Equal(32L * 1024 * 1024, ProviderModelPolicy.MaximumRequestBytes(claude));
        var deepseek = Settings("https://api.deepseek.com/v1", "deepseek-flash");
        Assert.Equal(32L * 1024 * 1024, ProviderModelPolicy.GetCapabilities(deepseek).MaxImageSize);
        Assert.Equal(48L * 1024 * 1024, ProviderModelPolicy.MaximumRequestBytes(deepseek));
        var qwen = ProviderModelPolicy.GetCapabilities(Settings("https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen3.8-max"));
        Assert.True(4 * ((qwen.MaxImageSize + 2) / 3) + 32 < 20L * 1024 * 1024);
        Assert.True(4 * ((qwen.MaxVideoSize + 2) / 3) + 32 < 10L * 1024 * 1024);
        Assert.Equal(3, ProviderModelPolicy.MaximumImageCount(Settings("https://api.groq.com/openai/v1", "qwen/qwen3.8-27b")));
        Assert.Equal(5, ProviderModelPolicy.MaximumImageCount(Settings("https://api.groq.com/openai/v1", "qwen/qwen3.6-27b")));
        Assert.Equal(16, ProviderModelPolicy.MaximumImageCount(Settings("https://proxy.invalid/v1", "qwen/qwen3.8-27b")));
        var mistral = Settings("https://api.mistral.ai/v1", "mistral-medium-3-5");
        Assert.Equal(8, ProviderModelPolicy.MaximumImageCount(mistral));
        Assert.Equal(10_000_000, ProviderModelPolicy.GetCapabilities(mistral).MaxImageSize);
        var glm = ProviderModelPolicy.GetCapabilities(Settings("https://api.z.ai/api/paas/v4", "glm-5.3-flash"));
        Assert.Equal(4_999_999, glm.MaxImageSize);
        Assert.Equal(new[] { "image/jpeg", "image/png" }, glm.AcceptedMimeTypes.Order().ToArray());
    }

    [Theory]
    [InlineData("o3-mini")]
    [InlineData("gpt-4o-search-preview")]
    public async Task TextOnlyOpenAiVariantsRejectImagesBeforeNetworkButKeepTextChat(string model)
    {
        var settings = Settings("https://api.openai.com/v1", model);
        var image = new byte[] { 1, 2, 3 };
        var provider = new OpenAiCompatibleProvider(settings, "test-key", (_, _, _) =>
            throw new InvalidOperationException("No image request may reach the network."), _ => TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<NotSupportedException>(() => provider.SendAsync(new AiRequest { Prompt = "test",
            Attachments = [new(AiAttachmentType.Image, "image/png", image)] }, TestContext.Current.CancellationToken));
        Assert.All(image, value => Assert.Equal(0, value));
        var result = await Send(settings, new AiRequest { Prompt = "text question" }, (_, body) =>
            Assert.Equal("text question", body.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("text").GetString()));
        Assert.Equal("OK", result.Answer);
    }

    private static AiProviderSettings Settings(string endpoint, string model) => new() { Type = "OpenAICompatible", BaseUrl = endpoint, Model = model };

    private static async Task<AiResult> Send(AiProviderSettings settings, AiRequest request, Action<HttpRequestMessage, JsonElement> inspect,
        string response = "{\"choices\":[{\"message\":{\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}")
    {
        var provider = new OpenAiCompatibleProvider(settings, "test-key", async (http, _, token) =>
        {
            using var body = JsonDocument.Parse(await http.Content!.ReadAsStringAsync(token));
            inspect(http, body.RootElement);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
        }, _ => TimeSpan.FromSeconds(10));
        return await provider.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class CollectProgress(List<AiStreamDelta> values) : IProgress<AiStreamDelta>
    {
        public void Report(AiStreamDelta value) => values.Add(value);
    }
}
