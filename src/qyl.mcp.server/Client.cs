// qyl.mcp.server - A2A Client with OpenTelemetry v1.38 Observability
// Demonstrates: remote agent discovery, telemetry wrapper, GenAI semantic conventions

using System.Diagnostics;
using System.Text.Json;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Trace;
using qyl.agents.telemetry;

namespace qyl.mcp.server;

/// <summary>
/// A2A client for connecting to remote agents and using them as tools.
/// </summary>
public sealed partial class HostClientAgent
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory? _loggerFactory;

    public HostClientAgent(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<HostClientAgent>() ?? NullLogger<HostClientAgent>.Instance;
    }

    /// <summary>
    /// The configured AI agent that uses remote agents as tools.
    /// </summary>
    public AIAgent? Agent { get; private set; }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing Agent Framework agent with model: {ModelId}")]
    private partial void LogInitializing(string modelId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully initialized agent with {ToolCount} remote agent tools")]
    private partial void LogInitialized(int toolCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize HostClientAgent")]
    private partial void LogInitializationFailed(Exception ex);

    /// <summary>
    /// Initializes the host agent with remote A2A agents as tools.
    /// </summary>
    /// <param name="modelId">The model ID to use (e.g., "gemini-pro").</param>
    /// <param name="chatClient">The chat client to use for the host agent.</param>
    /// <param name="agentUrls">URLs of remote A2A agents to connect to.</param>
    public async Task InitializeAgentAsync(string modelId, IChatClient chatClient, string[] agentUrls)
    {
        try
        {
            LogInitializing(modelId);

            // Connect to remote agents via A2A
            var createAgentTasks = agentUrls.Select(url => GetRemoteAgentAsync(new Uri(url)));
            var agents = await Task.WhenAll(createAgentTasks);
            var tools = agents.Select(agent => (AITool)agent.AsAIFunction()).ToList();

            // Create the agent using the provided chat client
            Agent = chatClient.CreateAIAgent(
                instructions: "You specialize in handling queries for users and using your tools to provide answers.",
                name: "HostClient",
                tools: tools);

            LogInitialized(tools.Count);
        }
        catch (Exception ex)
        {
            LogInitializationFailed(ex);
            throw;
        }
    }

    private async Task<AIAgent> GetRemoteAgentAsync(Uri baseUrl)
    {
        var agentCard = await A2ACardResolver.GetAgentCardAsync(baseUrl);
        // Use the extension method from A2A package
        return agentCard.GetAIAgent(loggerFactory: _loggerFactory);
    }
}

/// <summary>
/// Resolves Agent Card information from an A2A-compatible endpoint.
/// </summary>
public static class A2ACardResolver
{
    private static readonly HttpClient s_sharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    /// <summary>
    /// Gets the agent card from a remote A2A endpoint.
    /// </summary>
    public static async Task<AgentCard> GetAgentCardAsync(
        Uri baseUrl,
        string agentCardPath = "/.well-known/agent-card.json",
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        var client = httpClient ?? s_sharedClient;
        var cardUrl = new Uri(baseUrl, agentCardPath.TrimStart('/'));

        try
        {
            using var response = await client
                .GetAsync(cardUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            // Use A2A's DefaultOptions which includes their source-generated context
            return await JsonSerializer
                       .DeserializeAsync<AgentCard>(responseStream, A2A.A2AJsonUtilities.DefaultOptions, cancellationToken)
                       .ConfigureAwait(false)
                   ?? throw new A2AException("Failed to parse agent card JSON.");
        }
        catch (JsonException ex)
        {
            throw new A2AException($"Failed to parse JSON: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new A2AException("HTTP request failed", ex);
        }
    }
}

/// <summary>
/// Telemetry collector interface for tracking agent and tool invocations.
/// Implementations export telemetry to OpenTelemetry-compatible backends.
/// </summary>
public interface ITelemetryCollector
{
    void TrackAgentInvocation(string agentName, string operation, TimeSpan duration);
    void TrackToolCall(string toolName, string agentName, bool success, TimeSpan duration);
    void TrackTokenUsage(string agentName, long inputTokens, long outputTokens);
    void TrackError(string agentName, Exception exception);
}

/// <summary>
/// OpenTelemetry-based telemetry collector using v1.38 GenAI semantic conventions.
/// Creates Activities with proper span attributes for agent and tool invocations.
/// </summary>
public sealed class OpenTelemetryCollector : ITelemetryCollector
{
    private static readonly ActivitySource s_activitySource = new(GenAiSemanticConventions.SourceName);

    public static readonly OpenTelemetryCollector Instance = new();

    public void TrackAgentInvocation(string agentName, string operation, TimeSpan duration)
    {
        using var activity = s_activitySource.StartActivity(
            $"{GenAiSemanticConventions.InvokeAgent} {agentName}",
            ActivityKind.Internal);

        if (activity is null) return;

        activity.SetTag(GenAiSemanticConventions.Operation.Name, GenAiSemanticConventions.Operation.Values.InvokeAgent);
        activity.SetTag(GenAiSemanticConventions.Agent.Name, agentName);
        activity.SetTag("duration_ms", duration.TotalMilliseconds);
    }

    public void TrackToolCall(string toolName, string agentName, bool success, TimeSpan duration)
    {
        using var activity = s_activitySource.StartActivity(
            $"{GenAiSemanticConventions.ExecuteTool} {toolName}",
            ActivityKind.Internal);

        if (activity is null) return;

        activity.SetTag(GenAiSemanticConventions.Operation.Name, GenAiSemanticConventions.Operation.Values.ExecuteTool);
        activity.SetTag(GenAiSemanticConventions.Tool.Name, toolName);
        activity.SetTag(GenAiSemanticConventions.Agent.Name, agentName);
        activity.SetTag("success", success);
        activity.SetTag("duration_ms", duration.TotalMilliseconds);

        if (!success)
        {
            activity.SetStatus(ActivityStatusCode.Error);
        }
    }

    public void TrackTokenUsage(string agentName, long inputTokens, long outputTokens)
    {
        using var activity = s_activitySource.StartActivity(
            "token_usage",
            ActivityKind.Internal);

        if (activity is null) return;

        activity.SetTag(GenAiSemanticConventions.Agent.Name, agentName);
        activity.SetTag(GenAiSemanticConventions.Usage.InputTokens, inputTokens);
        activity.SetTag(GenAiSemanticConventions.Usage.OutputTokens, outputTokens);
    }

    public void TrackError(string agentName, Exception exception)
    {
        using var activity = s_activitySource.StartActivity(
            "error",
            ActivityKind.Internal);

        if (activity is null) return;

        activity.SetTag(GenAiSemanticConventions.Agent.Name, agentName);
        activity.SetTag(GenAiSemanticConventions.Error.Type, exception.GetType().Name);
        activity.SetTag(GenAiSemanticConventions.Error.Message, exception.Message);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }
}

/// <summary>
/// Telemetry-enabled delegating agent that wraps another agent and tracks invocations.
/// Uses OpenTelemetry v1.38 GenAI semantic conventions.
/// </summary>
public sealed class TelemetryAgent : DelegatingAIAgent
{
    private static readonly ActivitySource s_activitySource = new(GenAiSemanticConventions.SourceName);
    private readonly ITelemetryCollector _collector;
    private readonly string _agentName;

    public TelemetryAgent(AIAgent innerAgent, ITelemetryCollector? collector = null, string? agentName = null)
        : base(innerAgent)
    {
        _collector = collector ?? OpenTelemetryCollector.Instance;
        _agentName = agentName ?? GetAgentName(innerAgent);
    }

    private static string GetAgentName(AIAgent agent)
    {
        var metadata = agent.GetService<AIAgentMetadata>();
        return metadata?.ProviderName ?? "UnknownAgent";
    }

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity(
            $"{GenAiSemanticConventions.InvokeAgent} {_agentName}",
            ActivityKind.Client);

        // Set v1.38 semantic convention attributes
        activity?.SetTag(GenAiSemanticConventions.Operation.Name, GenAiSemanticConventions.Operation.Values.InvokeAgent);
        activity?.SetTag(GenAiSemanticConventions.Agent.Name, _agentName);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.RunAsync(messages, thread, options, cancellationToken);
            sw.Stop();

            _collector.TrackAgentInvocation(_agentName, "RunAsync", sw.Elapsed);

            // Track token usage if available
            if (response.Usage is { } usage)
            {
                activity?.SetTag(GenAiSemanticConventions.Usage.InputTokens, usage.InputTokenCount ?? 0);
                activity?.SetTag(GenAiSemanticConventions.Usage.OutputTokens, usage.OutputTokenCount ?? 0);
                _collector.TrackTokenUsage(_agentName, usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _collector.TrackError(_agentName, ex);
            throw;
        }
    }

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity(
            $"{GenAiSemanticConventions.InvokeAgent} {_agentName} (streaming)",
            ActivityKind.Client);

        activity?.SetTag(GenAiSemanticConventions.Operation.Name, GenAiSemanticConventions.Operation.Values.InvokeAgent);
        activity?.SetTag(GenAiSemanticConventions.Agent.Name, _agentName);

        var sw = Stopwatch.StartNew();

        await foreach (var update in base.RunStreamingAsync(messages, thread, options, cancellationToken))
        {
            yield return update;
        }

        sw.Stop();
        _collector.TrackAgentInvocation(_agentName, "RunStreamingAsync", sw.Elapsed);
    }
}

/// <summary>
/// Extension methods for adding telemetry to agents.
/// </summary>
public static class TelemetryAgentExtensions
{
    /// <summary>
    /// Wraps an agent with telemetry tracking using v1.38 GenAI semantic conventions.
    /// </summary>
    public static AIAgent WithTelemetry(
        this AIAgent agent,
        ITelemetryCollector? collector = null,
        string? agentName = null)
    {
        return new TelemetryAgent(agent, collector, agentName);
    }

    /// <summary>
    /// Adds telemetry to the agent pipeline using v1.38 GenAI semantic conventions.
    /// </summary>
    public static AIAgentBuilder UseTelemetry(
        this AIAgentBuilder builder,
        ITelemetryCollector? collector = null,
        string? agentName = null)
    {
        return builder.Use((innerAgent, _) => new TelemetryAgent(innerAgent, collector, agentName));
    }
}
