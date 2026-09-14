// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ProviderCatalogCompatibilityTests
{
    private static readonly Dictionary<string, string> NoHeaders = [];

    [Theory]
    [InlineData("https://api.together.xyz/v1")]
    [InlineData("https://api.together.ai/v1")]
    [InlineData("https://api.mistral.ai/v1")]
    public async Task TogetherAndMistralAcceptDocumentedRootArrays(string endpoint)
    {
        using var client = new HttpClient(new Handler(_ => RawJson("""
            [{"id":"text-chat","type":"chat"},{"id":"code-chat","type":"code"},
             {"id":"text-completion-only","capabilities":{"completion_chat":false}},
             {"id":"text-vision","capabilities":{"completion_chat":true,"vision":true}},
             {"id":"image-generator","type":"image"},{"id":"moderator","type":"moderation"}]
            """)));
        Assert.Equal(["code-chat", "text-chat", "text-vision"], await Load(client, endpoint));
    }

    [Fact]
    public async Task OpenRouterKeepsChatRoutedProModelsAndRemovesBatchVariants()
    {
        using var client = new HttpClient(new Handler(_ => Json(new { data = new[] {
            new { id = "vendor/flagship-pro" }, new { id = "vendor/flagship-pro:batch" }, new { id = "vendor/vision:free" } } })));
        Assert.Equal(["vendor/flagship-pro", "vendor/vision:free"], await Load(client, "https://openrouter.ai/api/v1"));
    }

    [Fact]
    public async Task AllPagesShareOneResponseSizeBudget()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            calls++;
            return Json(new { data = new[] { new { id = "model-" + calls } }, has_more = true, last_id = "cursor-" + calls,
                description = new string('x', ProviderModelCatalogService.MaximumResponseBytes - 200) });
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => Load(client, "https://api.anthropic.com/v1"));
        Assert.Equal(5, calls);
    }

    [Fact]
    public async Task AnthropicUsesAuthenticatedCursorPaginationAndEscapesCursor()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            calls++;
            Assert.Equal("api.anthropic.com", request.RequestUri!.Host);
            Assert.Equal("/v1/models", request.RequestUri.AbsolutePath);
            Assert.Equal(calls == 1 ? "?limit=100" : "?limit=100&after_id=last%2Fid%3Fvalue%3D1", request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("synthetic-key", request.Headers.Authorization.Parameter);
            Assert.Equal("2023-06-01", Assert.Single(request.Headers.GetValues("anthropic-version")));
            return calls == 1 ? Json(new { data = new[] { new { id = "claude-example-b" } }, has_more = true, last_id = "last/id?value=1" })
                : Json(new { data = new[] { new { id = "claude-example-a" } }, has_more = false });
        }));
        var result = await Load(client, "https://api.anthropic.com/v1");
        Assert.Equal(["claude-example-a", "claude-example-b"], result);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("https://dashscope.aliyuncs.com/compatible-mode/v1")]
    [InlineData("https://dashscope-intl.aliyuncs.com/compatible-mode/v1")]
    [InlineData("https://dashscope-us.aliyuncs.com/compatible-mode/v1")]
    [InlineData("https://cn-hongkong.dashscope.aliyuncs.com/compatible-mode/v1")]
    [InlineData("https://workspace-123.cn-beijing.maas.aliyuncs.com/compatible-mode/v1")]
    [InlineData("https://workspace-123.ap-southeast-1.maas.aliyuncs.com/compatible-mode/v1")]
    public async Task DashScopePreservesRegionAndWorkspaceWithNativePagination(string endpoint)
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(request =>
        {
            calls++;
            Assert.Equal(new Uri(endpoint).Host, request.RequestUri!.Host);
            Assert.Equal("/api/v1/models", request.RequestUri.AbsolutePath);
            Assert.Contains($"page_no={calls}&page_size=100", request.RequestUri.Query);
            Assert.Contains("capabilities=VU", request.RequestUri.Query);
            return Json(new { output = new { total = 2, page_no = calls, models = new[] { new { model = "qwen-example-" + calls,
                inference_metadata = new { request_modality = new[] { "Text", "Image", "Video" }, response_modality = new[] { "Text" } } } } } });
        }));
        Assert.Equal(["qwen-example-1", "qwen-example-2"], await Load(client, endpoint));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("https://api.siliconflow.cn/v1")]
    [InlineData("https://api.siliconflow.com/v1")]
    public async Task SiliconFlowSelectsChatSubtypeWithoutDroppingMultimodalModels(string endpoint)
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal(endpoint + "/models?sub_type=chat", request.RequestUri!.AbsoluteUri);
            return Json(new { data = new[] { new { id = "vendor/vision-chat" }, new { id = "vendor/reasoning-chat" } } });
        }));
        Assert.Equal(["vendor/reasoning-chat", "vendor/vision-chat"], await Load(client, endpoint));
    }

    [Theory]
    [InlineData("https://api.anthropic.com.attacker.test/v1")]
    [InlineData("https://api.anthropic.com:444/v1")]
    [InlineData("https://api.anthropic.com/proxy/v1")]
    [InlineData("https://dashscope.aliyuncs.com.attacker.test/compatible-mode/v1")]
    [InlineData("https://a.b.cn-beijing.maas.aliyuncs.com/compatible-mode/v1")]
    [InlineData("https://api.siliconflow.cn/proxy/v1")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai")]
    public async Task CustomAndGoogleEndpointsKeepTheirPathAndReceiveNoUndocumentedPaging(string endpoint)
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal(endpoint + "/models", request.RequestUri!.AbsoluteUri);
            Assert.False(request.Headers.Contains("anthropic-version"));
            return Json(new { data = new[] { new { id = "text-model" } } });
        }));
        Assert.Equal(["text-model"], await Load(client, endpoint));
    }

    [Fact]
    public async Task QianfanAndOpenRouterMetadataExcludeNonConversationOutputs()
    {
        const string body = """
            {"data":[
              {"id":"visual-chat","type":"image2text","architecture":{"input_modalities":["image","text"],"output_modalities":["text"]}},
              {"id":"language-chat","type":"chat","architecture":{"output_modalities":["text"]}},
              {"id":"images","type":"text2image"},
              {"id":"speech","architecture":{"output_modalities":["audio"]}},
              {"id":"vectors","type":"embeddings"},
              {"id":"reranking","type":"rerank"},
              {"id":"unavailable","status":"offline"},
              {"id":"custom-future-model"}
            ]}
            """;
        foreach (var endpoint in new[] { "https://qianfan.baidubce.com/v2", "https://openrouter.ai/api/v1" })
        {
            using var client = new HttpClient(new Handler(_ => RawJson(body)));
            Assert.Equal(["custom-future-model", "language-chat", "visual-chat"], await Load(client, endpoint));
        }
    }

    [Fact]
    public async Task ModelIdsExcludeUnambiguousNonChatFamiliesWithoutRestrictingVendors()
    {
        string[] models = ["whisper-1", "text-embedding-3-large", "gpt-image-1", "gpt-example-transcribe", "gpt-example-realtime",
            "tts-1", "dall-e-3", "vendor/FLUX.1-dev", "sora-2", "gpt-example", "claude-example", "vendor/new-multimodal"];
        using var client = new HttpClient(new Handler(_ => Json(new { data = models.Select(id => new { id }) })));
        Assert.Equal(["claude-example", "gpt-example", "vendor/new-multimodal"], await Load(client, "https://api.openai.com/v1"));
    }

    [Fact]
    public async Task TokenHubOnlyOffersOnlineModels()
    {
        using var client = new HttpClient(new Handler(_ => Json(new { data = new[] {
            new { id = "hy-example", status = "online" }, new { id = "old-example", status = "pre-offline" },
            new { id = "retired-example", status = "offline" } } })));
        Assert.Equal(["hy-example"], await Load(client, "https://tokenhub.tencentmaas.com/v1"));
    }

    [Theory]
    [InlineData("{\"data\":[],\"has_more\":true,\"last_id\":\"cursor\"}")]
    [InlineData("{\"data\":[{\"id\":\"x\"}],\"has_more\":true}")]
    [InlineData("{\"data\":[{\"id\":\"x\"}],\"has_more\":\"true\"}")]
    public async Task AnthropicRejectsBrokenPaginationInsteadOfReturningPartialResults(string body)
    {
        using var client = new HttpClient(new Handler(_ => RawJson(body)));
        await Assert.ThrowsAsync<InvalidDataException>(() => Load(client, "https://api.anthropic.com/v1"));
    }

    [Fact]
    public async Task RepeatingCursorAndRepeatingDashScopePageFailClosed()
    {
        using var anthropic = new HttpClient(new Handler(_ => Json(new { data = new[] { new { id = "x" } }, has_more = true, last_id = "x" })));
        await Assert.ThrowsAsync<InvalidDataException>(() => Load(anthropic, "https://api.anthropic.com/v1"));
        using var dashscope = new HttpClient(new Handler(_ => Json(new { output = new { total = 2, models = new[] { new { model = "same-page" } } } })));
        await Assert.ThrowsAsync<InvalidDataException>(() => Load(dashscope, "https://dashscope.aliyuncs.com/compatible-mode/v1"));
    }

    [Fact]
    public async Task PageLimitCannotTurnAnIncompleteCatalogIntoSuccess()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            calls++;
            return Json(new { data = new[] { new { id = "model-" + calls } }, has_more = true, last_id = "cursor-" + calls });
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => Load(client, "https://api.anthropic.com/v1"));
        Assert.Equal(ProviderModelCatalogService.MaximumPages, calls);
    }

    [Fact]
    public async Task CancellingBetweenPagesDoesNotRequestOrReturnLaterPages()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            calls++;
            cancellation.Cancel();
            return Json(new { data = new[] { new { id = "model-a" } }, has_more = true, last_id = "cursor-a" });
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProviderModelCatalogService(client)
            .GetModelsAsync("https://api.anthropic.com/v1", "synthetic-key", NoHeaders, cancellation.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AnthropicCustomAuthenticationRemainsExclusiveAndVersionIsNotDuplicated()
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("synthetic-header-key", Assert.Single(request.Headers.GetValues("x-api-key")));
            Assert.Equal("2023-06-01", Assert.Single(request.Headers.GetValues("anthropic-version")));
            return Json(new { data = Array.Empty<object>(), has_more = false });
        }));
        var headers = new Dictionary<string, string> { ["x-api-key"] = "synthetic-header-key", ["anthropic-version"] = "2023-06-01" };
        Assert.Empty(await new ProviderModelCatalogService(client).GetModelsAsync("https://api.anthropic.com/v1", "", headers, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProviderModelCatalogService(client)
            .GetModelsAsync("https://api.anthropic.com/v1", "synthetic-key", headers, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TooManyModelIdsFailRatherThanSilentlyTruncating()
    {
        using var client = new HttpClient(new Handler(_ => Json(new { data = Enumerable.Range(0, ProviderModelCatalogService.MaximumModels + 1).Select(i => new { id = "model-" + i }) })));
        await Assert.ThrowsAsync<InvalidDataException>(() => Load(client, "https://example.test/v1"));
    }

    [Fact]
    public async Task InvalidJsonDoesNotExposeRemoteResponseText()
    {
        using var client = new HttpClient(new Handler(_ => RawJson("{sensitive-remote-response}")));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Load(client, "https://example.test/v1"));
        Assert.DoesNotContain("sensitive", error.Message);
        Assert.Null(error.InnerException);
    }

    private static Task<IReadOnlyList<string>> Load(HttpClient client, string endpoint) =>
        new ProviderModelCatalogService(client).GetModelsAsync(endpoint, "synthetic-key", NoHeaders, TestContext.Current.CancellationToken);
    private static HttpResponseMessage Json(object body) => RawJson(JsonSerializer.Serialize(body));
    private static HttpResponseMessage RawJson(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
