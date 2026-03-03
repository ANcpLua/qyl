// =============================================================================
// qyl.copilot - IChatClient / ChatOptions Extension Methods
// Fluent helpers to add qyl OTel instrumentation to any IChatClient or
// ChatOptions tool list without touching the underlying client directly.
// =============================================================================

using qyl.copilot.Adapters;

// Intentionally in the Microsoft.Extensions.AI namespace so the extensions
// are discoverable alongside the types they extend, with no extra using needed.
namespace Microsoft.Extensions.AI;

/// <summary>
///     Extension methods for adding qyl OpenTelemetry instrumentation to
///     <see cref="IChatClient" /> pipelines and <see cref="ChatOptions" /> tool lists.
/// </summary>
public static class ChatClientExtensions
{
    /// <summary>
    ///     Wraps <paramref name="client" /> with qyl OTel instrumentation.
    ///     Records OTel 1.40 GenAI semantic convention attributes on every chat request.
    /// </summary>
    /// <param name="client">The <see cref="IChatClient" /> to instrument.</param>
    /// <param name="agentName">Optional agent name recorded as <c>gen_ai.agent.name</c>.</param>
    /// <param name="timeProvider">Time provider for duration measurement. Defaults to <c>TimeProvider.System</c>.</param>
    /// <returns>An <see cref="InstrumentedChatClient" /> wrapping <paramref name="client" />.</returns>
    public static IChatClient UseQylInstrumentation(
        this IChatClient client,
        string? agentName = null,
        TimeProvider? timeProvider = null)
        => new InstrumentedChatClient(client, agentName, timeProvider);

    /// <summary>
    ///     Wraps each <see cref="AIFunction" /> in <see cref="ChatOptions.Tools" /> with
    ///     <see cref="InstrumentedAIFunction" /> so every tool invocation gets an OTel
    ///     <c>execute_tool</c> span.
    /// </summary>
    /// <remarks>
    ///     Already-wrapped functions are skipped (idempotent).
    ///     Non-<see cref="AIFunction" /> tools (e.g. custom <see cref="AITool" /> subtypes) are left unchanged.
    /// </remarks>
    /// <param name="options">The <see cref="ChatOptions" /> whose tool list to instrument.</param>
    /// <returns>The same <paramref name="options" /> instance, mutated in place.</returns>
    public static ChatOptions AddInstrumentedTools(this ChatOptions options)
    {
        if (options.Tools is not { Count: > 0 })
            return options;

        for (int i = 0; i < options.Tools.Count; i++)
        {
            if (options.Tools[i] is AIFunction fn and not InstrumentedAIFunction)
                options.Tools[i] = new InstrumentedAIFunction(fn);
        }

        return options;
    }
}
