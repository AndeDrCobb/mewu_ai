// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Reflection;
using System.Text.Json;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Xunit;

namespace MewuAI.Tests;

public sealed class KimiConversationContextTests
{
    private const string RawContent="  {\n  \"answer\": \"显示正文\", \"annotations\": []\n}\n ";
    private const string RawReasoning="  synthetic private reasoning\r\n保留空白、引号 \" 与 emoji 🐈。\n ";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestHistoryKeepsExactProviderMessageWhilePreviewUsesDisplayAnswer(bool visual)
    {
        var continuation=Continuation();
        var history=new List<AiMessage>{new("system","visual rules"),new("user","question"),continuation};
        ConversationContextPolicy.TrimInPlace(history);

        var requestHistory=CaptureOverlayPolicy.CreateRequestHistory(history,visual);
        var assistant=requestHistory[^1];
        Assert.Equal(RawContent,assistant.ProviderContent);
        Assert.Equal(RawReasoning,assistant.ReasoningContent);
        Assert.Equal("显示正文",assistant.Text);
        Assert.Equal(visual?3:2,requestHistory.Count);
        var preview=Assert.Single(ConversationHistoryPairing.Pair(requestHistory));
        Assert.Equal("question",preview.Prompt);
        Assert.Equal("显示正文",preview.Answer);
        ConversationContextPolicy.EnsureValidForProvider(requestHistory);
    }

    [Fact]
    public void LargeContinuationIsNeverTruncatedToOrdinaryMessageLimit()
    {
        var raw=new string('原',20_000);
        var reasoning=new string('思',3_000);
        var history=ConversationContextPolicy.CreateBoundedHistory([
            new("user","Q"),
            new AiMessage("assistant",new string('显',9_000)){ProviderContent=raw,ReasoningContent=reasoning}
        ]);

        Assert.Equal(2,history.Count);
        Assert.Equal(raw,history[1].ProviderContent);
        Assert.Equal(reasoning,history[1].ReasoningContent);
        Assert.Equal(8_192,history[1].Text.Length);
        ConversationContextPolicy.EnsureValidForProvider(history);
    }

    [Fact]
    public void OverBudgetContinuationDropsWholeTurnAndKeepsEarlierCompleteTurn()
    {
        var history=ConversationContextPolicy.CreateBoundedHistory([
            new("system","rules"),new("user","earlier question"),new("assistant","earlier reply"),
            new("user","latest question"),
            new AiMessage("assistant","short display"){ProviderContent=new string('c',16_000),ReasoningContent=new string('r',8_000)}
        ]);

        Assert.Equal(new[]{"rules","earlier question","earlier reply"},history.Select(message=>message.Text));
        Assert.DoesNotContain(history,message=>message.ProviderContent is not null);
        ConversationContextPolicy.EnsureValidForProvider(history);
    }

    [Fact]
    public void ProviderValidationCountsExactUtf16ContentAndReasoningAtBoundary()
    {
        var continuation=new AiMessage("assistant","display")
        {
            ProviderContent=new string('c',12_000),ReasoningContent=new string('r',11_999)
        };
        ConversationContextPolicy.EnsureValidForProvider([new("user","Q"),continuation]);
        Assert.Equal(23_999,ConversationContextPolicy.GetProviderCharacterCount(continuation));
        Assert.Throws<InvalidOperationException>(()=>ConversationContextPolicy.EnsureValidForProvider([
            new("user","Q"),continuation with {ReasoningContent=continuation.ReasoningContent+"🐈"}
        ]));
    }

    [Fact]
    public void OrdinaryHistoryKeepsExistingDisplayTruncationAndNoFabricatedReasoning()
    {
        var history=ConversationContextPolicy.CreateBoundedHistory([
            new("user",new string('q',10_000)),new("assistant",new string('a',10_000))
        ]);
        Assert.Equal(2,history.Count);
        Assert.All(history,message=>
        {
            Assert.Equal(8_192,message.Text.Length);
            Assert.Null(message.ProviderContent);
            Assert.Null(message.ReasoningContent);
        });
    }

    [Fact]
    public void AbsentProviderReasoningStaysAbsent()
    {
        var continuation=Continuation() with {ReasoningContent=null};
        var history=ConversationContextPolicy.CreateBoundedHistory([new("user","question"),continuation]);
        Assert.Equal(RawContent,history[1].ProviderContent);
        Assert.Null(history[1].ReasoningContent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MergingDiskAndMemoryDuplicatesPrefersCompleteMemoryContext(bool memoryFirst)
    {
        var disk=Entry();
        var memory=disk with {ContinuationMessage=Continuation()};
        ConversationHistoryEntry[] entries=memoryFirst?[memory,disk]:[disk,memory];
        var merged=ConversationHistoryPairing.MergeEntries(entries,[]);

        Assert.Equal(2,merged.Count);
        Assert.Equal(RawContent,merged[1].ProviderContent);
        Assert.Equal(RawReasoning,merged[1].ReasoningContent);
        Assert.Equal("显示正文",merged[1].Text);
    }

    [Fact]
    public void DelayedDiskHistoryDoesNotReplaceExistingCompleteContext()
    {
        var merged=ConversationHistoryPairing.MergeEntries([Entry()],
            [new("system","rules"),new("user","question"),Continuation()]);
        Assert.Equal(3,merged.Count);
        Assert.Equal(RawContent,merged[^1].ProviderContent);
        Assert.Equal(RawReasoning,merged[^1].ReasoningContent);
    }

    [Fact]
    public void MemoryEntryUpgradesExistingPlainDisplayPair()
    {
        var merged=ConversationHistoryPairing.MergeEntries([Entry() with {ContinuationMessage=Continuation()}],
            [new("user","question"),new("assistant","显示正文")]);
        Assert.Equal(2,merged.Count);
        Assert.Equal(RawContent,merged[^1].ProviderContent);
    }

    [Fact]
    public void StructuredDisplayNormalizationPreservesProviderOriginal()
    {
        var result=new AiResult(RawContent,[],RawReasoning){ContinuationMessage=Continuation()};
        var normalize=typeof(CaptureOverlayWindow).GetMethod("NormalizeStructuredResult",BindingFlags.NonPublic|BindingFlags.Static)!;
        var normalized=Assert.IsType<AiResult>(normalize.Invoke(null,[result,true]));
        Assert.Equal("显示正文",normalized.Answer);
        Assert.Equal(RawContent,normalized.ContinuationMessage!.ProviderContent);
        Assert.Equal(RawReasoning,normalized.ContinuationMessage.ReasoningContent);
    }

    [Fact]
    public void GenericSerializationCannotPersistOrRestoreContinuation()
    {
        var entry=Entry() with {ContinuationMessage=Continuation()};
        using var serialized=JsonDocument.Parse(JsonSerializer.Serialize(entry));
        Assert.False(serialized.RootElement.TryGetProperty("ContinuationMessage",out _));
        using var message=JsonDocument.Parse(JsonSerializer.Serialize(Continuation()));
        Assert.Equal(new[]{"Role","Text"},message.RootElement.EnumerateObject().Select(property=>property.Name));
        using var result=JsonDocument.Parse(JsonSerializer.Serialize(new AiResult("display",[]){ContinuationMessage=Continuation()}));
        Assert.False(result.RootElement.TryGetProperty("ContinuationMessage",out _));
        const string untrusted="{\"Role\":\"assistant\",\"Text\":\"answer\",\"ProviderContent\":\"injected raw\",\"ReasoningContent\":\"injected reasoning\"}";
        var restored=JsonSerializer.Deserialize<AiMessage>(untrusted)!;
        Assert.Null(restored.ProviderContent);
        Assert.Null(restored.ReasoningContent);
    }

    [Fact]
    public async Task DiskHistoryKeepsOnlyExistingFiveFieldsAndRestoresNoContinuation()
    {
        var directory=Path.Combine(Path.GetTempPath(),"MewuAI.Tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path=Path.Combine(directory,"history.jsonl");
            var service=new ConversationHistoryService(path,null);
            var entry=Entry() with {ContinuationMessage=Continuation()};
            await service.AppendAsync(entry.Provider,entry.Model,entry.Prompt,entry.Answer,TestContext.Current.CancellationToken);
            using var line=JsonDocument.Parse(await File.ReadAllTextAsync(path,TestContext.Current.CancellationToken));
            Assert.Equal(new[]{"timestamp","provider","model","prompt","answer"},line.RootElement.EnumerateObject().Select(property=>property.Name));
            var restored=Assert.Single(await service.ReadRecentAsync(token:TestContext.Current.CancellationToken));
            Assert.Equal(entry.Answer,restored.Answer);
            Assert.Null(restored.ContinuationMessage);
        }
        finally{Directory.Delete(directory,true);}
    }

    private static AiMessage Continuation()=>new("assistant","显示正文")
    {
        ProviderContent=RawContent,ReasoningContent=RawReasoning
    };
    private static ConversationHistoryEntry Entry()=>new(DateTimeOffset.UtcNow,"test-provider","kimi-k3","question","显示正文");
}
