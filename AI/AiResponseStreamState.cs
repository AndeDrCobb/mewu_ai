// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using mewu_ai_Assistant.Models;

namespace mewu_ai_Assistant.AI;

/// <summary>
/// Describes the meaningful part of a streaming response that has just
/// arrived. A reasoning-only delta is still an in-progress response; it is
/// not the same as the terminal result that contains reasoning but no answer.
/// </summary>
internal enum AiResponseStreamState
{
    Waiting,
    Thinking,
    Answering
}

internal static class AiResponseStreamStatePolicy
{
    internal static AiResponseStreamState Classify(AiStreamDelta delta)
    {
        if (delta is null) throw new ArgumentNullException(nameof(delta));
        if (delta.Content.Length > 0) return AiResponseStreamState.Answering;
        if (delta.ReasoningContent.Length > 0) return AiResponseStreamState.Thinking;
        return AiResponseStreamState.Waiting;
    }
}
