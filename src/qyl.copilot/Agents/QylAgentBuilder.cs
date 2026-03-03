// =============================================================================
// qyl.copilot - QylAgentBuilder
// Factory for creating AIAgent instances ready for MapAGUI() endpoint exposure.
// Two paths:
//   1. GitHub Copilot   -> QylAgentBuilder.FromCopilotAdapter(adapter)
//   2. IChatClient      -> QylAgentBuilder.FromChatClient(chatClient, ...)
// Both paths return an AIAgent wired with InstrumentedChatClient for OTel spans.
// Note: qyl.servicedefaults.generator already intercepts AIAgent.RunAsync() at
// compile time, so UseOpenTelemetry() is not duplicated here.
// =============================================================================

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using qyl.copilot.Adapters;

namespace qyl.copilot.Agents;

/// <summary>
///     Factory that produces <see cref="AIAgent"/> instances ready to be served
///     via AG-UI (<c>MapAGUI()</c>). Handles both the GitHub Copilot and
///     provider-agnostic <see cref="IChatClient"/> code paths.
/// </summary>
public static class QylAgentBuilder
{
    /// <summary>
    ///     Exposes the already-initialized agent inside a
    ///     <see cref="QylCopilotAdapter"/> for AG-UI endpoint registration.
    ///     Call this after the adapter has been created (e.g., via
    ///     <see cref="CopilotAdapterFactory.GetAdapterAsync"/>).
    /// </summary>
    /// <param name="adapter">The live adapter whose inner agent to expose.</param>
    /// <returns>The underlying <see cref="AIAgent"/>.</returns>
    public static AIAgent FromCopilotAdapter(QylCopilotAdapter adapter)
    {
        Guard.NotNull(adapter);
        return adapter.GetInnerAgent();
    }

    /// <summary>
    ///     Wraps an <see cref="IChatClient"/> as an <see cref="AIAgent"/>,
    ///     adding qyl OTel instrumentation via <see cref="InstrumentedChatClient"/>.
    /// </summary>
    /// <param name="chatClient">The upstream chat client (Ollama, OpenAI, etc.).</param>
    /// <param name="agentName">Identifies the agent in OTel spans and metadata.</param>
    /// <param name="description">Short description shown in AG-UI clients.</param>
    /// <param name="instructions">System instructions injected at the start of each session.</param>
    /// <param name="tools">Optional tools the agent may call during a run.</param>
    /// <param name="timeProvider">Time provider for OTel timestamps.</param>
    /// <returns>A fully wired <see cref="AIAgent"/>.</returns>
    public static AIAgent FromChatClient(
        IChatClient chatClient,
        string agentName = "qyl-assistant",
        string description = "qyl AI assistant",
        string? instructions = null,
        IReadOnlyList<AITool>? tools = null,
        TimeProvider? timeProvider = null)
    {
        Guard.NotNull(chatClient);

        // Wrap with OTel instrumentation at IChatClient level (gen_ai.* spans + metrics)
        IChatClient instrumented = chatClient.UseQylInstrumentation(agentName, timeProvider);

        return instrumented.AsAIAgent(
            name: agentName,
            description: description,
            instructions: instructions,
            tools: tools is null ? null : [.. tools]);
    }
}
