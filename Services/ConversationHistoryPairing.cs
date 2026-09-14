// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using mewu_ai_Assistant.Models;

namespace mewu_ai_Assistant.Services;

internal readonly record struct ConversationHistoryPair(string Prompt,string Answer)
{
    internal AiMessage? ContinuationMessage { get; init; }
}

internal static class ConversationHistoryPairing
{
    internal static IReadOnlyList<ConversationHistoryPair> Pair(IEnumerable<AiMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var pairs=new List<ConversationHistoryPair>();
        AiMessage? pendingPrompt=null;
        foreach(var message in messages)
        {
            if(message is null)continue;
            if(string.Equals(message.Role,"user",StringComparison.OrdinalIgnoreCase))
            {
                pendingPrompt=message;
                continue;
            }
            if(!string.Equals(message.Role,"assistant",StringComparison.OrdinalIgnoreCase)||pendingPrompt is null)continue;
            pairs.Add(new ConversationHistoryPair(pendingPrompt.Text,message.Text)
            {
                ContinuationMessage=message.ProviderContent is null?null:message
            });
            pendingPrompt=null;
        }
        return pairs;
    }

    internal static IReadOnlyList<AiMessage> MergeEntries(IEnumerable<ConversationHistoryEntry> entries,IReadOnlyList<AiMessage> existing)
    {
        ArgumentNullException.ThrowIfNull(entries);ArgumentNullException.ThrowIfNull(existing);
        var existingPairs=Pair(existing);
        var existingKeys=existingPairs.Select(pair=>(pair.Prompt,pair.Answer)).ToHashSet();
        var incoming=new Dictionary<(string Prompt,string Answer),ConversationHistoryPair>();
        foreach(var entry in entries)
        {
            if(string.IsNullOrWhiteSpace(entry.Prompt)||string.IsNullOrWhiteSpace(entry.Answer))continue;
            var key=(entry.Prompt,entry.Answer);
            var pair=new ConversationHistoryPair(entry.Prompt,entry.Answer){ContinuationMessage=entry.ContinuationMessage};
            if(!incoming.TryGetValue(key,out var previous)||previous.ContinuationMessage?.ProviderContent is null)
                incoming[key]=pair;
        }
        var result=new List<AiMessage>();
        var system=existing.FirstOrDefault(message=>message is not null&&string.Equals(message.Role,"system",StringComparison.OrdinalIgnoreCase));
        if(system is not null)result.Add(system);
        foreach(var entry in incoming)
            if(!existingKeys.Contains(entry.Key))Append(entry.Value);
        foreach(var pair in existingPairs)
        {
            var chosen=pair;
            if(pair.ContinuationMessage?.ProviderContent is null&&incoming.TryGetValue((pair.Prompt,pair.Answer),out var candidate))chosen=candidate;
            Append(chosen);
        }
        return result;

        void Append(ConversationHistoryPair pair)
        {
            result.Add(new AiMessage("user",pair.Prompt));
            var continuation=pair.ContinuationMessage;
            result.Add(continuation is {ProviderContent:not null}&&string.Equals(continuation.Role,"assistant",StringComparison.OrdinalIgnoreCase)
                ?continuation with {Text=pair.Answer}:new AiMessage("assistant",pair.Answer));
        }
    }
}
