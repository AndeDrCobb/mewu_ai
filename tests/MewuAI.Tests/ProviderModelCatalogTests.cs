// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Net;
using System.Net.Http;
using System.Text;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ProviderModelCatalogTests
{
    [Fact]
    public void BareBaseUrlGetsOnlyBoundedOpenAiCompatibleCandidates()
    {
        var normalized = ProviderEndpointPolicy.NormalizeBaseUri("https://api.example.test");
        Assert.Equal(["/", "/v1/", "/api/v1/"], ProviderModelCatalogService.GetEndpointCandidates(normalized).Select(uri => uri.AbsolutePath));

        Assert.Equal(["/api/v1/"], ProviderModelCatalogService.GetEndpointCandidates(
            ProviderEndpointPolicy.NormalizeBaseUri("https://api.example.test/api/v1")).Select(uri => uri.AbsolutePath));
        Assert.Equal(["/compatible-mode/v1/"], ProviderModelCatalogService.GetEndpointCandidates(
            ProviderEndpointPolicy.NormalizeBaseUri("https://dashscope.aliyuncs.com/compatible-mode/v1")).Select(uri => uri.AbsolutePath));
        Assert.Equal(["/v2/"], ProviderModelCatalogService.GetEndpointCandidates(
            ProviderEndpointPolicy.NormalizeBaseUri("https://api.example.test/v2")).Select(uri => uri.AbsolutePath));
    }

    [Fact]
    public async Task BareBaseUrlRetriesV1AfterHtmlOrNonListResponse()
    {
        var paths = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath == "/v1/models"
                ? Json("{\"data\":[{\"id\":\"deepseek-chat\"}]}")
                : RawJson("<html>not an API model list</html>");
        }));

        var catalog = new ProviderModelCatalogService(client);
        var models = await catalog.GetModelsAsync(
            "https://api.example.test", "synthetic-key", new Dictionary<string, string>(), TestContext.Current.CancellationToken);

        Assert.Equal(["/models", "/v1/models"], paths);
        Assert.Equal(["deepseek-chat"], models);
        Assert.Equal("https://api.example.test/v1/", catalog.LastSuccessfulBaseUrl);
    }

    [Theory]
    [InlineData("<html>gateway error</html>")]
    [InlineData("{\"data\":null}")]
    public async Task BareBaseUrlReportsInvalidListAfterAllCandidatesFail(string body)
    {
        var paths = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return RawJson(body);
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => new ProviderModelCatalogService(client).GetModelsAsync(
            "https://api.example.test", "synthetic-key", new Dictionary<string, string>(), TestContext.Current.CancellationToken));
        Assert.Equal(["/models", "/v1/models", "/api/v1/models"], paths);
    }

    [Theory]
    [InlineData("synthetic\rkey")]
    [InlineData("synthetic\nkey")]
    [InlineData("synthetic\0key")]
    public async Task InvalidKeyFormatIsReportedBeforeNetworkingWithoutExposingKey(string key)
    {
        var sent = false;
        using var client = new HttpClient(new Handler(_ => { sent = true; return Json("{\"data\":[]}"); }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProviderModelCatalogService(client).GetModelsAsync("https://api.example.invalid/v1", key,
                new Dictionary<string, string>(), TestContext.Current.CancellationToken));
        Assert.False(sent);
        Assert.DoesNotContain("synthetic", error.ToString(), StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("https://api.minimaxi.com/v1", "MiniMax-M3")]
    [InlineData("https://api.openai.com/v1", "gpt-example")]
    [InlineData("https://ark.cn-beijing.volces.com/api/v3", "glm-5-3-flash")]
    [InlineData("http://localhost:1234/v1", "local-model")]
    public async Task LoadsFromSelectedEndpointAndPreservesIds(string endpoint, string model)
    {
        using var handler = new Handler(request =>
        {
            Assert.Equal(endpoint + "/models", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization.Parameter);
            return Json("{\"data\":[null,1,{}, {\"id\":\"" + model + "\"},{\"id\":\"" + model + "\"}]}");
        });
        using var client = new HttpClient(handler);
        var result = await new ProviderModelCatalogService(client).GetModelsAsync(endpoint, "test-key", new Dictionary<string, string>(), TestContext.Current.CancellationToken);
        Assert.Equal([model], result);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"models\":[]}")]
    public async Task RejectsInvalidEnvelopes(string body)
    {
        using var client = new HttpClient(new Handler(_ => Json(body)));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ProviderModelCatalogService(client).GetModelsAsync("https://api.minimaxi.com/v1", "test", new Dictionary<string,string>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnforcesLimitWithoutContentLength()
    {
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new NonSeekableStream(new byte[ProviderModelCatalogService.MaximumResponseBytes + 1])) }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ProviderModelCatalogService(client).GetModelsAsync("https://api.minimaxi.com/v1", "test", new Dictionary<string,string>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsCompetingCredentialsBeforeNetwork()
    {
        using var client = new HttpClient(new Handler(_ => throw new Xunit.Sdk.XunitException("Unexpected request")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProviderModelCatalogService(client).GetModelsAsync("https://api.minimaxi.com/v1", "test", new Dictionary<string,string> { ["Authorization"] = "Bearer header-key" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DoesNotExposeResponseSecretsOrFollowRedirect()
    {
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Redirect) { Content = new StringContent("secret-path-key") }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new ProviderModelCatalogService(client).GetModelsAsync("https://api.minimaxi.com/v1", "test", new Dictionary<string,string>(), TestContext.Current.CancellationToken));
        Assert.Contains("302", error.Message);
        Assert.DoesNotContain("secret-path-key", error.Message);
    }

    [Fact]
    public async Task VolcengineFiltersGenerationButDoesNotInventRecommendedModels()
    {
        using var client = new HttpClient(new Handler(_ => Json("{\"data\":[{\"id\":\"doubao-seedream\"},{\"id\":\"glm-5-3-flash\"}]}")));
        var models = await new ProviderModelCatalogService(client).GetModelsAsync(VolcengineModelPolicy.StandardBaseUrl, "test", new Dictionary<string,string>(), TestContext.Current.CancellationToken);
        Assert.Equal(["glm-5-3-flash"], models);
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", false)]
    [InlineData("https://openrouter.ai/api/v1", true)]
    public async Task ResponsesOnlyModelsAreFilteredOnlyOnTheOfficialOpenAiEndpoint(string endpoint,bool proxy)
    {
        using var client=new HttpClient(new Handler(_=>Json("{\"data\":[{\"id\":\"gpt-6-astra\"},{\"id\":\"gpt-5.5-pro\"},{\"id\":\"gpt-5.3-codex\"},{\"id\":\"omni-moderation-latest\"}]}")));
        var models=await new ProviderModelCatalogService(client).GetModelsAsync(endpoint,"test",new Dictionary<string,string>(),TestContext.Current.CancellationToken);
        Assert.Contains("gpt-6-astra",models);
        Assert.Equal(proxy,models.Contains("gpt-5.5-pro"));
        Assert.Equal(proxy,models.Contains("gpt-5.3-codex"));
        Assert.DoesNotContain("omni-moderation-latest",models);
    }

    [Theory]
    [InlineData("https://api.minimaxi.com/v1")]
    [InlineData("https://api.minimax.cn/v1")]
    [InlineData("https://api.minimax.io/v1")]
    public void CustomMiniMaxConnectionUsesTheSameVideoAwareProviderAsItsTemplate(string endpoint)
    {
        var provider=AiProviderFactory.CreateConfigured(new AiProviderSettings{Type="OpenAICompatible",BaseUrl=endpoint,Model="MiniMax-M3"},"fixture-key");
        Assert.IsType<mewu_ai_Assistant.AI.MiniMaxProvider>(provider);
        Assert.True(provider.Capabilities.SupportsVideo);
    }

    [Fact]
    public void PresetsUseProviderNamesAndKeepCredentialsIsolated()
    {
        foreach (var preset in ProviderPresetPolicy.All)
        {
            var first = ProviderPresetPolicy.Create(preset);
            var second = ProviderPresetPolicy.Create(preset);
            Assert.NotEqual(first.Id, second.Id);
            Assert.Empty(second.CredentialId);
            Assert.Empty(second.CustomHeaders);
            Assert.Empty(second.SensitiveHeaderCredentialIds);
            Assert.Equal(preset.Id, ProviderPresetPolicy.Detect(first).Id);
        }
        Assert.Equal("MiniMax", new AiProviderSettings().Name);
        Assert.Equal("MiniMax", ProviderPresetPolicy.DisplayName(new AiProviderSettings { Name = "MiniMax M3" }));
        Assert.Equal("My API", ProviderPresetPolicy.DisplayName(new AiProviderSettings { Name = "My API" }));
        Assert.Equal("Custom", ProviderPresetPolicy.Detect(new AiProviderSettings { BaseUrl = "https://api.minimaxi.com.attacker.test/v1" }).Id);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage RawJson(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/html") };
    [Fact]
    public void MainstreamProviderTemplatesUseOfficialEndpointsAndKeepCustomEditing()
    {
        foreach(var id in new[]{"MiniMax","MiniMaxGlobal","Volcengine","DashScope","DeepSeek","Moonshot","Zhipu","Tencent","Baidu","SiliconFlow","OpenAI","Anthropic","Google","xAI","OpenRouter","Groq","Mistral","Together","Custom"})
            Assert.Single(ProviderPresetPolicy.All,p=>p.Id==id);
        Assert.Equal(ProviderPresetPolicy.All.Length,ProviderPresetPolicy.All.Select(p=>p.Id).Distinct().Count());
        Assert.Equal("Custom",Assert.Single(ProviderPresetPolicy.All,p=>p.RequiresBaseUrl).Id);
        Assert.All(ProviderPresetPolicy.All.Where(p=>!p.RequiresBaseUrl),p=>Assert.True(Uri.IsWellFormedUriString(p.BaseUrl,UriKind.Absolute)));
        Assert.Equal("OpenAI",ProviderPresetPolicy.Detect(new AiProviderSettings{Type="OpenAICompatible",BaseUrl="https://api.openai.com/v1/"}).Id);
        Assert.Equal("Custom",ProviderPresetPolicy.Detect(new AiProviderSettings{Type="OpenAICompatible",BaseUrl="https://example.test/v1"}).Id);
        Assert.All(ProviderPresetPolicy.All.Where(p=>p.Type!="MiniMax"),p=>Assert.Empty(p.DefaultModel));
    }

    [Fact]
    public void LegacyMiniMaxAddressIsRecognizedWithoutChangingTheSavedConnection()
    {
        var existing=new AiProviderSettings{Type="MiniMax",BaseUrl="https://api.minimaxi.com/v1",Model="MiniMax-M3",Name="School API",CredentialId="existing-credential"};
        Assert.Equal("MiniMax",ProviderPresetPolicy.Detect(existing).Id);
        Assert.Equal("https://api.minimaxi.com/v1",existing.BaseUrl);
        Assert.Equal("School API",existing.Name);
        Assert.Equal("existing-credential",existing.CredentialId);
        Assert.Equal("Custom",ProviderPresetPolicy.Detect(new AiProviderSettings{Type="MiniMax",BaseUrl="https://api.minimax.cn.attacker.test/v1"}).Id);
    }

    [Fact]
    public void OnlyUntouchedAutomaticallyCreatedDraftsMayBeDiscarded()
    {
        var draft = ProviderPresetPolicy.Create(ProviderPresetPolicy.All.Single(p => p.Id == "Custom"));
        Assert.True(ProviderPresetPolicy.IsUntouchedDraft(draft, false));
        Assert.False(ProviderPresetPolicy.IsUntouchedDraft(draft, true));
        draft.Model = "chosen-model";
        Assert.False(ProviderPresetPolicy.IsUntouchedDraft(draft, false));
        draft.Model = "";
        draft.CustomHeaders["X-Tenant"] = "tenant";
        Assert.False(ProviderPresetPolicy.IsUntouchedDraft(draft, false));
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
