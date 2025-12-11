using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using qyl.agents.telemetry;
using qyl.Shared;

namespace qyl.mcp.server;

public sealed partial class HostClientAgent
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory? _loggerFactory;

    public HostClientAgent(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<HostClientAgent>() ?? NullLogger<HostClientAgent>.Instance;
    }

    public AIAgent? Agent { get; private set; }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing Agent Framework agent with model: {ModelId}")]
    private partial void LogInitializing(string modelId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Successfully initialized agent with {ToolCount} remote agent tools")]
    private partial void LogInitialized(int toolCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize HostClientAgent")]
    private partial void LogInitializationFailed(Exception ex);

    public async Task InitializeAgentAsync(string modelId, IChatClient chatClient, string[] agentUrls)
    {
        try
        {
            LogInitializing(modelId);

            IEnumerable<Task<AIAgent>> createAgentTasks = agentUrls.Select(url => GetRemoteAgentAsync(new Uri(url)));
            AIAgent[] agents = await Task.WhenAll(createAgentTasks);
            var tools = agents.Select(AITool (agent) => agent.AsAIFunction()).ToList();

            Agent = chatClient.CreateAIAgent(
                "You specialize in handling queries for users and using your tools to provide answers.",
                "HostClient",
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
        AgentCard agentCard = await A2ACardResolver.GetAgentCardAsync(baseUrl);
        return agentCard.GetAIAgent(loggerFactory: _loggerFactory);
    }
}

public static class A2ACardResolver
{
    private static readonly HttpClient s_sharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public static async Task<AgentCard> GetAgentCardAsync(
        Uri baseUrl,
        string agentCardPath = "/.well-known/agent-card.json",
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        Throw.IfNull(baseUrl);

        HttpClient client = httpClient ?? s_sharedClient;
        var cardUrl = new Uri(baseUrl, agentCardPath.TrimStart('/'));

        try
        {
            using HttpResponseMessage response = await client
                .GetAsync(cardUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using Stream responseStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            return await JsonSerializer
                       .DeserializeAsync<AgentCard>(responseStream, JsonSerializerOptions.Default, cancellationToken)
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

public interface ITelemetryCollector
{
    void TrackAgentInvocation(string agentName, string operation, TimeSpan duration);
    void TrackToolCall(string toolName, string agentName, bool success, TimeSpan duration);
    void TrackTokenUsage(string agentName, long inputTokens, long outputTokens);
    void TrackError(string agentName, Exception exception);
}

public sealed class OpenTelemetryCollector : ITelemetryCollector
{
    private static readonly ActivitySource s_activitySource = new(GenAiAttributes.SourceName);

    public static readonly OpenTelemetryCollector Instance = new();

    public void TrackAgentInvocation(string agentName, string operation, TimeSpan duration)
    {
        using Activity? activity = s_activitySource.StartActivity(
            $"{GenAiAttributes.InvokeAgent} {agentName}");

        if (activity is null) return;

        activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.InvokeAgent);
        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag("duration_ms", duration.TotalMilliseconds);
    }

    public void TrackToolCall(string toolName, string agentName, bool success, TimeSpan duration)
    {
        using Activity? activity = s_activitySource.StartActivity(
            $"{GenAiAttributes.ExecuteTool} {toolName}");

        if (activity is null) return;

        activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
        activity.SetTag(GenAiAttributes.ToolName, toolName);
        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag("success", success);
        activity.SetTag("duration_ms", duration.TotalMilliseconds);

        if (!success) activity.SetStatus(ActivityStatusCode.Error);
    }

    public void TrackTokenUsage(string agentName, long inputTokens, long outputTokens)
    {
        using Activity? activity = s_activitySource.StartActivity(
            "token_usage");

        if (activity is null) return;

        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag(GenAiAttributes.UsageInputTokens, inputTokens);
        activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
    }

    public void TrackError(string agentName, Exception exception)
    {
        using Activity? activity = s_activitySource.StartActivity(
            "error");

        if (activity is null) return;

        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag(GenAiAttributes.ErrorType, exception.GetType().Name);
        activity.SetTag(GenAiAttributes.ErrorMessage, exception.Message);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }
}

public sealed class TelemetryAgent : DelegatingAIAgent
{
    private static readonly ActivitySource s_activitySource = new(GenAiAttributes.SourceName);
    private readonly string _agentName;
    private readonly ITelemetryCollector _collector;

    public TelemetryAgent(AIAgent innerAgent, ITelemetryCollector? collector = null, string? agentName = null)
        : base(innerAgent)
    {
        _collector = collector ?? OpenTelemetryCollector.Instance;
        _agentName = agentName ?? GetAgentName(innerAgent);
    }

    private static string GetAgentName(AIAgent agent)
    {
        AIAgentMetadata? metadata = agent.GetService<AIAgentMetadata>();
        return metadata?.ProviderName ?? "UnknownAgent";
    }

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = s_activitySource.StartActivity(
            $"{GenAiAttributes.InvokeAgent} {_agentName}",
            ActivityKind.Client);

        activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.InvokeAgent);
        activity?.SetTag(GenAiAttributes.AgentName, _agentName);

        var sw = Stopwatch.StartNew();
        try
        {
            AgentRunResponse response = await base.RunAsync(messages, thread, options, cancellationToken);
            sw.Stop();

            _collector.TrackAgentInvocation(_agentName, "RunAsync", sw.Elapsed);

            if (response.Usage is { } usage)
            {
                activity?.SetTag(GenAiAttributes.UsageInputTokens, usage.InputTokenCount ?? 0);
                activity?.SetTag(GenAiAttributes.UsageOutputTokens, usage.OutputTokenCount ?? 0);
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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using Activity? activity = s_activitySource.StartActivity(
            $"{GenAiAttributes.InvokeAgent} {_agentName} (streaming)",
            ActivityKind.Client);

        activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.InvokeAgent);
        activity?.SetTag(GenAiAttributes.AgentName, _agentName);

        var sw = Stopwatch.StartNew();

        await foreach (AgentRunResponseUpdate update in base.RunStreamingAsync(messages, thread, options, cancellationToken))
            yield return update;

        sw.Stop();
        _collector.TrackAgentInvocation(_agentName, "RunStreamingAsync", sw.Elapsed);
    }
}

public static class TelemetryAgentExtensions
{
    public static AIAgent WithTelemetry(
        this AIAgent agent,
        ITelemetryCollector? collector = null,
        string? agentName = null) =>
        new TelemetryAgent(agent, collector, agentName);

    public static AIAgentBuilder UseTelemetry(
        this AIAgentBuilder builder,
        ITelemetryCollector? collector = null,
        string? agentName = null) =>
        builder.Use((innerAgent, _) => new TelemetryAgent(innerAgent, collector, agentName));
}
