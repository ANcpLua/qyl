// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Patterns.Clients;

/// <summary>
///     Provider-agnostic <see cref="IChatClient"/> factory. Mirrors Apex's
///     <c>IExtractorChatClientBuilder</c> — the composition root calls through this
///     seam so any concrete provider (OpenAI, Azure, Ollama, or — in this sample —
///     a fake) can be swapped without touching downstream agent code.
/// </summary>
public interface IQylLoomPatternsChatClientBuilder : IDisposable
{
    /// <summary>
    ///     Builds the <see cref="IChatClient"/> for a named stage. Each stage can be
    ///     scripted with a distinct canned response so the sample exercises the MAF
    ///     wiring without an LLM dependency.
    /// </summary>
    /// <param name="stage">Logical stage identifier — <c>"rca"</c>, <c>"solution"</c>, or <c>"verdict"</c>.</param>
    IChatClient BuildChatClient(string stage);
}
