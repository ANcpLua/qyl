using System.Runtime.CompilerServices;
using System.Text.Json;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using qyl.protocol.Attributes;

namespace qyl.mcp;

internal static class TelemetryConstants
{
    // .NET 10: ActivitySourceOptions with OTel 1.39 schema URL
    public static readonly ActivitySource ActivitySource = new(new ActivitySourceOptions(GenAiAttributes.SourceName)
    {
        Version = "1.0.0", TelemetrySchemaUrl = GenAiAttributes.SchemaUrl
    });
}

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

            var createAgentTasks = agentUrls.Select(url => GetRemoteAgentAsync(new Uri(url)));
            List<AITool> tools = [];

            await foreach (var completedTask in Task.WhenEach(createAgentTasks).ConfigureAwait(false))
            {
                var agent = await completedTask.ConfigureAwait(false);
                tools.Add(agent.AsAIFunction());
            }

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
        var agentCard = await A2ACardResolver.GetAgentCardAsync(baseUrl).ConfigureAwait(false);
        return agentCard.GetAIAgent(loggerFactory: _loggerFactory);
    }
}

public static class A2ACardResolver
{
    private static readonly HttpClient SSharedClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static async Task<AgentCard> GetAgentCardAsync(
        Uri baseUrl,
        string agentCardPath = "/.well-known/agent-card.json",
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        Throw.IfNull(baseUrl);

        var client = httpClient ?? SSharedClient;
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
    public static readonly OpenTelemetryCollector Instance = new();

    public void TrackAgentInvocation(string agentName, string operation, TimeSpan duration)
    {
        if (TelemetryConstants.ActivitySource.StartActivity($"{GenAiAttributes.InvokeAgent} {agentName}") is not { } activity) return;

        activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.InvokeAgent);
        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag("duration_ms", duration.TotalMilliseconds);
    }

    public void TrackToolCall(string toolName, string agentName, bool success, TimeSpan duration)
    {
        if (TelemetryConstants.ActivitySource.StartActivity($"{GenAiAttributes.ExecuteTool} {toolName}") is not { } activity) return;

        activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.ExecuteTool);
        activity.SetTag(GenAiAttributes.ToolName, toolName);
        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag("success", success);
        activity.SetTag("duration_ms", duration.TotalMilliseconds);

        if (!success) activity.SetStatus(ActivityStatusCode.Error);
    }

    public void TrackTokenUsage(string agentName, long inputTokens, long outputTokens)
    {
        if (TelemetryConstants.ActivitySource.StartActivity("token_usage") is not { } activity) return;

        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag(GenAiAttributes.UsageInputTokens, inputTokens);
        activity.SetTag(GenAiAttributes.UsageOutputTokens, outputTokens);
    }

    public void TrackError(string agentName, Exception exception)
    {
        if (TelemetryConstants.ActivitySource.StartActivity("error") is not { } activity) return;

        activity.SetTag(GenAiAttributes.AgentName, agentName);
        activity.SetTag(GenAiAttributes.ErrorType, exception.GetType().Name);
        activity.SetTag(GenAiAttributes.ErrorMessage, exception.Message);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }
}

public sealed class TelemetryAgent : DelegatingAIAgent
{
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
        var metadata = agent.GetService<AIAgentMetadata>();
        return metadata?.ProviderName ?? "UnknownAgent";
    }

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryConstants.ActivitySource.StartActivity(
            $"{GenAiAttributes.InvokeAgent} {_agentName}",
            ActivityKind.Client);

        activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.InvokeAgent);
        activity?.SetTag(GenAiAttributes.AgentName, _agentName);

        var startTime = TimeProvider.System.GetTimestamp();
        try
        {
            var response = await base.RunAsync(messages, thread, options, cancellationToken).ConfigureAwait(false);
            var elapsed = TimeProvider.System.GetElapsedTime(startTime);

            _collector.TrackAgentInvocation(_agentName, "RunAsync", elapsed);

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
            TimeProvider.System.GetElapsedTime(startTime);
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
        using var activity = TelemetryConstants.ActivitySource.StartActivity(
            $"{GenAiAttributes.InvokeAgent} {_agentName} (streaming)",
            ActivityKind.Client);

        activity?.SetTag(GenAiAttributes.OperationName, GenAiAttributes.Operations.InvokeAgent);
        activity?.SetTag(GenAiAttributes.AgentName, _agentName);

        var startTime = TimeProvider.System.GetTimestamp();

        await foreach (var update in base.RunStreamingAsync(messages, thread, options, cancellationToken))
            yield return update;

        var elapsed = TimeProvider.System.GetElapsedTime(startTime);
        _collector.TrackAgentInvocation(_agentName, "RunStreamingAsync", elapsed);
    }
}

public static class TelemetryAgentExtensions
{
    public static AIAgent WithTelemetry(this AIAgent agent, ITelemetryCollector? collector = null,
        string? agentName = null) =>
        new TelemetryAgent(agent, collector, agentName);

    public static AIAgentBuilder UseTelemetry(this AIAgentBuilder builder, ITelemetryCollector? collector = null,
        string? agentName = null) =>
        builder.Use((innerAgent, _) => new TelemetryAgent(innerAgent, collector, agentName));
}
