// =============================================================================
// qyl.servicedefaults - ChatClient Extension Methods
// Zero-config instrumentation via Microsoft.Extensions.AI.OpenTelemetryChatClient
// =============================================================================

using Microsoft.Extensions.AI;

namespace Qyl.ServiceDefaults.Instrumentation.GenAi;

/// <summary>
/// Extension methods for adding qyl instrumentation to <see cref="IChatClient"/>.
/// </summary>
public static class ChatClientExtensions
{
    /// <summary>
    /// Adds qyl instrumentation to the chat client pipeline.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>Zero-config instrumentation via M.E.AI.OpenTelemetryChatClient includes:</para>
    /// <list type="bullet">
    ///   <item><term>OTel 1.37+ GenAI Semconv</term><description>Full attribute compliance</description></item>
    ///   <item><term>Token Usage Metrics</term><description>gen_ai.client.token.usage histogram</description></item>
    ///   <item><term>Operation Duration</term><description>gen_ai.client.operation.duration histogram</description></item>
    ///   <item><term>Sensitive Data Control</term><description>Via OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT</description></item>
    /// </list>
    /// <para>
    /// Example:
    /// <code>
    /// builder.Services.AddChatClient(new OpenAIClient(key).AsChatClient("gpt-4o"))
    ///     .UseQylInstrumentation();
    /// </code>
    /// </para>
    /// </remarks>
    public static ChatClientBuilder UseQylInstrumentation(this ChatClientBuilder builder)
        => builder.UseQylInstrumentation(configure: null);

    /// <summary>
    /// Adds qyl instrumentation to the chat client pipeline with configuration.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="configure">Optional configuration for the OpenTelemetryChatClient.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ChatClientBuilder UseQylInstrumentation(
        this ChatClientBuilder builder,
        Action<OpenTelemetryChatClient>? configure)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseOpenTelemetry(
            sourceName: GenAiConstants.SourceName,
            configure: configure);
    }

    /// <summary>
    /// Adds qyl instrumentation with sensitive data capture enabled.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="enableSensitiveData">Whether to capture message content.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static ChatClientBuilder UseQylInstrumentation(
        this ChatClientBuilder builder,
        bool enableSensitiveData)
    {
        return builder.UseQylInstrumentation(otel =>
        {
            otel.EnableSensitiveData = enableSensitiveData;
        });
    }
}
