// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.IO;
using System.Text.Json;
using mewu_ai_Assistant.Services;

/// <summary>Opt-in read-only check of a public model catalog; no settings, keys or model requests.</summary>
internal static class PublicModelCatalogReplay
{
    internal static async Task RunAsync()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var models=await new ProviderModelCatalogService().GetModelsAsync("https://openrouter.ai/api/v1",string.Empty,
            new Dictionary<string,string>(),timeout.Token);
        if(models.Count==0||models.Any(id=>id.EndsWith(":batch",StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The public catalog did not return a usable filtered model list.");
        var sample=new[]{"openai/gpt-6-astra","google/gemini-3.8-flash","anthropic/claude-opus-5","x-ai/grok-4.6"};
        Directory.CreateDirectory(".codex-build");
        await File.WriteAllTextAsync(".codex-build/public-model-catalog-result.json",JsonSerializer.Serialize(new
        {
            checkedAt=DateTimeOffset.UtcNow,
            endpoint="https://openrouter.ai/api/v1/models",
            modelCount=models.Count,
            currentExamples=sample.ToDictionary(id=>id,id=>models.Contains(id,StringComparer.Ordinal)),
            sentCredentials=false,
            sentConversation=false
        }),new System.Text.UTF8Encoding(false),timeout.Token);
    }
}
