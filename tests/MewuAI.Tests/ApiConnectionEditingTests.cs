// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Text.Json;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ApiConnectionEditingTests
{
    [Fact]
    public void CapturedDraftDoesNotFollowLaterChangesToProviderDictionaries()
    {
        var provider = Provider("first");
        var draft = ApiConnectionDraft.FromProvider(provider);

        provider.CustomHeaders["X-Workspace"] = "changed-after-capture";
        provider.RequestParameters["temperature"] = JsonSerializer.SerializeToElement(1.8);
        var target = Provider("target");
        draft.ApplyTo(target);

        Assert.Equal("workspace-first", target.CustomHeaders["X-Workspace"]);
        Assert.Equal(0.4, target.RequestParameters["temperature"].GetDouble());
        Assert.Equal("first-model", target.Model);
        Assert.Equal("target", target.Id);
    }

    [Theory]
    [InlineData("{\n  \"X-Workspace\": \"unfinished", "{\n  \"temperature\": ")]
    [InlineData("  { \"X-Workspace\": \"draft\" }  \n", "{\n  \"temperature\": 0.6\n}\n")]
    public void DraftKeepsExactEditorTextWithoutEagerJsonValidation(string headers, string parameters)
    {
        var draft = new ApiConnectionDraft("https://example.invalid/v1/", "  unsaved-model  ", headers, parameters);

        Assert.Equal(headers, draft.HeadersJson);
        Assert.Equal(parameters, draft.ParametersJson);
        Assert.Equal("https://example.invalid/v1/", draft.BaseUrl);
        Assert.Equal("  unsaved-model  ", draft.Model);
    }

    [Theory]
    [InlineData("{", "{\"temperature\":0.8}")]
    [InlineData("{\"X-Workspace\":null}", "{\"temperature\":0.8}")]
    [InlineData("{\"Host\":\"other.invalid\"}", "{\"temperature\":0.8}")]
    [InlineData("{\"X-Workspace\":\"first\",\"x-workspace\":\"second\"}", "{\"temperature\":0.8}")]
    [InlineData("{\"X-Workspace\":\"replacement\"}", "{")]
    [InlineData("{\"X-Workspace\":\"replacement\"}", "{\"model\":\"forbidden\"}")]
    [InlineData("{\"X-Workspace\":\"replacement\"}", "{\"temperature\":0.5,\"temperature\":0.9}")]
    public void InvalidDraftDoesNotPartiallyMutateConnection(string headers, string parameters)
    {
        var provider = Provider("first");
        var originalHeaders = provider.CustomHeaders;
        var originalParameters = provider.RequestParameters;
        var credentialMappings = provider.SensitiveHeaderCredentialIds;
        var draft = new ApiConnectionDraft("https://changed.invalid/v1", "changed-model", headers, parameters);

        var error = Record.Exception(() => draft.ApplyTo(provider));

        Assert.True(error is JsonException or InvalidOperationException);
        Assert.Equal("https://first.invalid/v1", provider.BaseUrl);
        Assert.Equal("first-model", provider.Model);
        Assert.Same(originalHeaders, provider.CustomHeaders);
        Assert.Same(originalParameters, provider.RequestParameters);
        Assert.Equal("workspace-first", provider.CustomHeaders["X-Workspace"]);
        Assert.Equal(0.4, provider.RequestParameters["temperature"].GetDouble());
        AssertIdentityAndCredentials(provider, "first", credentialMappings);
    }

    [Fact]
    public void SuccessfulApplyChangesOnlyEditableFieldsAndPreservesCredentialIdentity()
    {
        var provider = Provider("first");
        var credentialMappings = provider.SensitiveHeaderCredentialIds;
        var draft = new ApiConnectionDraft("https://changed.invalid/v1///", "  changed-model  ",
            "{\"X-Workspace\":\"replacement\",\"Authorization\":\"synthetic-edit-only\"}",
            "{\"temperature\":0.8,\"service_tier\":\"priority\"}");

        draft.ApplyTo(provider);

        Assert.Equal("https://changed.invalid/v1", provider.BaseUrl);
        Assert.Equal("changed-model", provider.Model);
        Assert.Equal("replacement", provider.CustomHeaders["X-Workspace"]);
        Assert.Equal("synthetic-edit-only", provider.CustomHeaders["Authorization"]);
        Assert.Equal(0.8, provider.RequestParameters["temperature"].GetDouble());
        Assert.Equal("priority", provider.RequestParameters["service_tier"].GetString());
        AssertIdentityAndCredentials(provider, "first", credentialMappings);
    }

    [Fact]
    public void ApplyingOneConnectionsDraftCannotChangeAnotherConnectionsValuesOrCredentials()
    {
        var first = Provider("first");
        var second = Provider("second");
        var secondDraft = ApiConnectionDraft.FromProvider(second);
        var firstDraft = new ApiConnectionDraft("https://shared.invalid/v1", "first-edited-model",
            "{\"Authorization\":\"first-synthetic-value\"}", "{\"top_p\":0.7}");

        firstDraft.ApplyTo(first);

        Assert.Equal("second-model", second.Model);
        Assert.Equal("workspace-second", second.CustomHeaders["X-Workspace"]);
        Assert.Equal("second-api-credential", second.CredentialId);
        Assert.Equal("second-header-credential", second.SensitiveHeaderCredentialIds["Authorization"]);
        Assert.DoesNotContain("Authorization", second.CustomHeaders.Keys);
        Assert.Equal(secondDraft, ApiConnectionDraft.FromProvider(second));

        secondDraft.ApplyTo(second);

        Assert.Equal("first-edited-model", first.Model);
        Assert.Equal("first-synthetic-value", first.CustomHeaders["Authorization"]);
        Assert.Equal("first-api-credential", first.CredentialId);
        Assert.Equal("first-header-credential", first.SensitiveHeaderCredentialIds["Authorization"]);
        Assert.Equal(0.7, first.RequestParameters["top_p"].GetDouble());
    }

    [Fact]
    public void EmptyJsonEditorsClearOptionalValuesWithoutChangingCredentials()
    {
        var provider = Provider("first");
        var credentialMappings = provider.SensitiveHeaderCredentialIds;

        new ApiConnectionDraft(provider.BaseUrl, provider.Model, " \n ", "\t").ApplyTo(provider);

        Assert.Empty(provider.CustomHeaders);
        Assert.Empty(provider.RequestParameters);
        AssertIdentityAndCredentials(provider, "first", credentialMappings);
    }

    private static AiProviderSettings Provider(string id) => new()
    {
        Id = id,
        Name = $"{id}-connection",
        Type = "OpenAICompatible",
        BaseUrl = $"https://{id}.invalid/v1",
        Model = $"{id}-model",
        CredentialId = $"{id}-api-credential",
        CustomHeaders = new Dictionary<string, string> { ["X-Workspace"] = $"workspace-{id}" },
        RequestParameters = new Dictionary<string, JsonElement> { ["temperature"] = JsonSerializer.SerializeToElement(0.4) },
        SensitiveHeaderCredentialIds = new Dictionary<string, string> { ["Authorization"] = $"{id}-header-credential" }
    };

    private static void AssertIdentityAndCredentials(AiProviderSettings provider, string id, Dictionary<string, string> credentialMappings)
    {
        Assert.Equal(id, provider.Id);
        Assert.Equal($"{id}-connection", provider.Name);
        Assert.Equal("OpenAICompatible", provider.Type);
        Assert.Equal($"{id}-api-credential", provider.CredentialId);
        Assert.Same(credentialMappings, provider.SensitiveHeaderCredentialIds);
        Assert.Equal($"{id}-header-credential", Assert.Single(provider.SensitiveHeaderCredentialIds).Value);
    }
}
