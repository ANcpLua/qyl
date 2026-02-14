// =============================================================================
// qyl.copilot - Composable Agent Pipeline
// Builds instrumented agent chains using Microsoft.Agents.AI primitives
// Auto-instruments with OTel 1.39 GenAI semantic conventions
// =============================================================================

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using qyl.copilot.Instrumentation;

namespace qyl.copilot.Adapters;

/// <summary>
///     Builds composable agent pipelines with OTel instrumentation baked in.
///     Chains: OpenTelemetryAgent → LoggingAgent → user agent
///     Sets gen_ai.agent.name and gen_ai.agent.id on all spans automatically.
/// </summary>
public static class QylAgentPipeline
{
    /// <summary>
    ///     Creates an instrumented agent pipeline builder using a factory for deferred agent resolution.
    ///     The factory receives the <see cref="IServiceProvider"/> at <see cref="AIAgentBuilder.Build"/> time.
    /// </summary>
    /// <param name="agentName">Logical agent name (set as gen_ai.agent.name).</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An <see cref="AIAgentBuilder"/> configured with OTel and logging middleware.</returns>
    public static AIAgentBuilder CreateInstrumented(string agentName, IServiceProvider services)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(services);

        var loggerFactory = services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;

        var builder = new AIAgentBuilder(sp =>
            {
                // Resolve IChatClient from DI for deferred agent creation
                var chatClient = (IChatClient?)sp.GetService(typeof(IChatClient));
                if (chatClient is null)
                    throw new InvalidOperationException($"No IChatClient registered for agent '{agentName}'.");
                return new ChatClientAgent(chatClient, name: agentName);
            })
            .UseOpenTelemetry(CopilotInstrumentation.SourceName);

        if (loggerFactory is not null)
        {
            builder = builder.UseLogging(loggerFactory);
        }

        return builder;
    }

    /// <summary>
    ///     Creates an instrumented agent pipeline builder wrapping an existing agent.
    /// </summary>
    /// <param name="agentName">Logical agent name (set as gen_ai.agent.name).</param>
    /// <param name="innerAgent">The inner agent to wrap with instrumentation.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An <see cref="AIAgentBuilder"/> configured with OTel and logging middleware.</returns>
    public static AIAgentBuilder CreateInstrumented(string agentName, AIAgent innerAgent, IServiceProvider services)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(innerAgent);
        ArgumentNullException.ThrowIfNull(services);

        var loggerFactory = services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;

        var builder = new AIAgentBuilder(innerAgent)
            .UseOpenTelemetry(CopilotInstrumentation.SourceName);

        if (loggerFactory is not null)
        {
            builder = builder.UseLogging(loggerFactory);
        }

        return builder;
    }

    /// <summary>
    ///     Creates a fully instrumented agent from a <see cref="ChatClientAgent"/>.
    ///     Wraps the agent in OTel + Logging middleware layers.
    /// </summary>
    /// <param name="agentName">Logical agent name.</param>
    /// <param name="chatClient">The chat client to back the agent.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <param name="instructions">Optional system instructions.</param>
    /// <param name="tools">Optional tools the agent can invoke.</param>
    /// <returns>An instrumented <see cref="AIAgent"/>.</returns>
    public static AIAgent CreateChatAgent(
        string agentName,
        IChatClient chatClient,
        IServiceProvider services,
        string? instructions = null,
        IReadOnlyList<AITool>? tools = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(services);

        var innerAgent = new ChatClientAgent(
            chatClient,
            name: agentName,
            instructions: instructions,
            tools: tools?.ToList());

        return CreateInstrumented(agentName, innerAgent, services)
            .Build(services);
    }

    /// <summary>
    ///     Creates a delegating agent that wraps an existing agent with OTel instrumentation.
    ///     Useful for adding instrumentation to agents received from external sources.
    /// </summary>
    /// <param name="agentName">Logical agent name for telemetry.</param>
    /// <param name="innerAgent">The agent to wrap.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An instrumented <see cref="AIAgent"/>.</returns>
    public static AIAgent WrapWithInstrumentation(
        string agentName,
        AIAgent innerAgent,
        IServiceProvider services)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(innerAgent);
        ArgumentNullException.ThrowIfNull(services);

        return CreateInstrumented(agentName, innerAgent, services)
            .Build(services);
    }
}
