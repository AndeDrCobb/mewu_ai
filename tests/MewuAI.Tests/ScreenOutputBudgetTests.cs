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

public sealed class ScreenOutputBudgetTests
{
    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public async Task M3ScreenAndTableRequestsUseOfficialMaximumWithoutDisablingThinking(bool stream,bool tableRecognition)
    {
        var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX2sAAAAASUVORK5CYII=");
        var table="| ID | Value |\n| --- | --- |\n"+string.Join("\n",Enumerable.Range(1,1000).Select(index=>$"| {index} | data-{index} |"));
        var provider=new OpenAiCompatibleProvider(new AiProviderSettings(),"test",async (message,completionOption,token)=>
        {
            using var body=JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
            Assert.Equal(524288,body.RootElement.GetProperty("max_completion_tokens").GetInt32());
            Assert.False(body.RootElement.TryGetProperty("max_tokens",out _));
            Assert.Equal("adaptive",body.RootElement.GetProperty("thinking").GetProperty("type").GetString());
            Assert.Equal(!tableRecognition,body.RootElement.GetProperty("messages").GetRawText().Contains("prior-conversation",StringComparison.Ordinal));
            Assert.Equal("image_url",body.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content")[1].GetProperty("type").GetString());
            var content=JsonSerializer.Serialize(new{answer=table,annotationMode="preserve",annotations=Array.Empty<object>()});
            var response=stream
                ?"data: "+JsonSerializer.Serialize(new{choices=new[]{new{delta=new{content,reasoning_content="checked"},finish_reason="stop"}}})+"\n\n"
                :JsonSerializer.Serialize(new{choices=new[]{new{message=new{content,reasoning_content="checked"},finish_reason="stop"}}});
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(response)};
        },_=>TimeSpan.FromSeconds(10));
        var request=CaptureOverlayPolicy.CreateScreenAiRequest("extract table",
            [new("user","prior-conversation"),new("assistant","prior-conversation")],
            [new(AiAttachmentType.Image,"image/png",image)],stream?new DiscardProgress():null,tableRecognition:tableRecognition);
        Assert.False(request.DisableReasoning);
        var result=await provider.SendAsync(request,TestContext.Current.CancellationToken);
        Assert.Equal(table,result.Answer);Assert.Equal("checked",result.Reasoning);
        Assert.All(image,value=>Assert.Equal(0,value));
    }

    [Theory]
    [InlineData(AiAttachmentType.Image, null, 524288)]
    [InlineData(AiAttachmentType.Image, 65536, 65536)]
    [InlineData(AiAttachmentType.Video, null, 524288)]
    [InlineData(AiAttachmentType.Video, 65536, 65536)]
    public async Task StructuredVisualRequestsPreserveLargeExplicitAndModelMaximumBudgets(AiAttachmentType type, int? budget, int expected)
    {
        byte[] media = [1, 2, 3, 4];
        var provider = new OpenAiCompatibleProvider(new AiProviderSettings(), "test", async (message, _, token) =>
        {
            using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
            Assert.Equal(expected, body.RootElement.GetProperty("max_completion_tokens").GetInt32());
            var content = body.RootElement.GetProperty("messages")[0].GetProperty("content");
            Assert.Equal(type == AiAttachmentType.Image ? "image_url" : "video_url", content[1].GetProperty("type").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "{\"answer\":\"OK\",\"annotations\":[]}" }, finish_reason = "stop" } }
                }))
            };
        }, _ => TimeSpan.FromSeconds(10));
        var result = await provider.SendAsync(new AiRequest
        {
            Prompt = "describe",
            ExpectStructuredResponse = true,
            UseModelMaximumOutputTokens = true,
            MaxOutputTokens = budget,
            Attachments = [new(type, type == AiAttachmentType.Image ? "image/png" : "video/mp4", media)]
        }, TestContext.Current.CancellationToken);
        Assert.Equal("OK", result.Answer);
        Assert.All(media, value => Assert.Equal(0, value));
    }

    [Fact] public async Task ExplicitSmallUtilityRequestBudgetIsPreserved()
    {
        var provider=new OpenAiCompatibleProvider(new AiProviderSettings(),"test",async (message,completionOption,token)=>
        {
            using var body=JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
            Assert.Equal(32,body.RootElement.GetProperty("max_completion_tokens").GetInt32());
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"choices\":[{\"message\":{\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}")};
        },_=>TimeSpan.FromSeconds(10));
        await provider.SendAsync(new AiRequest{Prompt="probe",MaxOutputTokens=32},TestContext.Current.CancellationToken);
    }

    [Fact] public async Task UnknownProviderDoesNotReceiveAnInventedMaximumOrThinkingOverride()
    {
        var provider=new OpenAiCompatibleProvider(new AiProviderSettings{Type="OpenAICompatible",BaseUrl="https://example.invalid/v1",Model="custom"},"test",async (message,completionOption,token)=>
        {
            using var body=JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));
            Assert.False(body.RootElement.TryGetProperty("max_tokens",out _));
            Assert.False(body.RootElement.TryGetProperty("max_completion_tokens",out _));
            Assert.False(body.RootElement.TryGetProperty("thinking",out _));
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"choices\":[{\"message\":{\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}")};
        },_=>TimeSpan.FromSeconds(10));
        await provider.SendAsync(CaptureOverlayPolicy.CreateScreenAiRequest("table",[],[],null,tableRecognition:true),TestContext.Current.CancellationToken);
    }

    private sealed class DiscardProgress:IProgress<AiStreamDelta>{public void Report(AiStreamDelta value){}}
}
