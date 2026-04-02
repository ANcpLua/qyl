// =============================================================================
// qyl.instrumentation - ChatClient Extension Methods
// Zero-config instrumentation via Microsoft.Extensions.AI.OpenTelemetryChatClient
// =============================================================================

using Microsoft.Extensions.AI;

namespace Qyl.Instrumentation.Instrumentation.GenAi;

/// <summary>
///     Extension methods for adding qyl instrumentation to <see cref="IChatClient" />.
/// </summary>
public static class ChatClientExtensions
{
    /// <summary>
    ///     Adds qyl instrumentation to the chat client pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    ///     <para>Zero-config instrumentation via M.E.AI.OpenTelemetryChatClient includes:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <term>OTel 1.37+ GenAI Semconv</term><description>Full attribute compliance</description>
    ///         </item>
    ///         <item>
    ///             <term>Token Usage Metrics</term><description>gen_ai.client.token.usage histogram</description>
    ///         </item>
    ///         <item>
    ///             <term>Operation Duration</term><description>gen_ai.client.operation.duration histogram</description>
    ///         </item>
    ///         <item>
    ///             <term>Sensitive Data Control</term>
    ///             <description>Via OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         Example:
    ///         <code>
    /// builder.Services.AddChatClient(new OpenAIClient(key).AsChatClient("gpt-4o"))
    ///     .UseQylInstrumentation();
    /// </code>
    ///     </para>
    /// </remarks>
    public static ChatClientBuilder UseQylInstrumentation(this ChatClientBuilder builder)
        => builder.UseQylInstrumentation(null);

    /// <summary>
    ///     Adds qyl instrumentation to the chat client pipeline with configuration.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="configure">Optional configuration for the OpenTelemetryChatClient.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ChatClientBuilder UseQylInstrumentation(
        this ChatClientBuilder builder,
        Action<OpenTelemetryChatClient>? configure)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseOpenTelemetry(
            sourceName: GenAiConstants.SourceName,
            configure: configure);
        builder.Use(static inner => new ToolInstrumentingChatClient(inner));
        return builder;
    }

    /// <summary>
    ///     Adds qyl instrumentation with sensitive data capture enabled.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="enableSensitiveData">Whether to capture message content.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ChatClientBuilder UseQylInstrumentation(
        this ChatClientBuilder builder,
        bool enableSensitiveData) =>
        builder.UseQylInstrumentation(otel =>
        {
            otel.EnableSensitiveData = enableSensitiveData;
        });

    /// <summary>
    ///     Wraps <paramref name="client" /> with qyl OTel instrumentation via
    ///     <see cref="InstrumentedChatClient" />.
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
    ///     <c>execute_tool</c> span. Already-wrapped functions are skipped (idempotent).
    /// </summary>
    /// <param name="options">The <see cref="ChatOptions" /> whose tool list to instrument.</param>
    /// <returns>The same <paramref name="options" /> instance, mutated in place.</returns>
    /// <remarks>
    ///     qyl-instrumented chat clients call this automatically. Keep this helper for explicit
    ///     one-off tool preparation when the chat client itself is not instrumented by qyl.
    /// </remarks>
    public static ChatOptions AddInstrumentedTools(this ChatOptions options)
    {
        if (options.Tools is not { Count: > 0 })
            return options;

        for (var i = 0; i < options.Tools.Count; i++)
        {
            if (options.Tools[i] is AIFunction fn and not InstrumentedAIFunction)
                options.Tools[i] = new InstrumentedAIFunction(fn);
        }

        return options;
    }
}
