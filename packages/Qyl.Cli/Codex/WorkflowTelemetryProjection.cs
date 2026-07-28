using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Qyl.Api.Contracts.Workflow;

namespace Qyl.Cli.Codex;

internal sealed class WorkflowTelemetryProjection : IDisposable
{
    private const string SourceName = "qyl.codex.observer";
    private static readonly Action<ILogger, WorkflowJournalEventKind, string, Exception?>
        s_logJournalEvent = LoggerMessage.Define<WorkflowJournalEventKind, string>(
            LogLevel.Information,
            new EventId(1, "WorkflowJournalEvent"),
            "Workflow journal event {EventKind} {EventId}");

    private readonly ActivitySource _source = new(SourceName, BuildVersion.ProductVersion);
    private readonly Dictionary<string, Activity> _activities = new(StringComparer.Ordinal);
    private readonly TracerProvider? _traces;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger? _logger;

    private WorkflowTelemetryProjection(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var resource = ResourceBuilder.CreateDefault()
            .AddService("qyl-codex-observer", serviceVersion: BuildVersion.ProductVersion);
        var headers = $"x-otlp-api-key={Uri.EscapeDataString(apiKey)}";
        _traces = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(SourceName)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("https://api.qyl.at/v1/traces");
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Headers = headers;
            })
            .Build();

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resource);
                options.IncludeFormattedMessage = true;
                options.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri("https://api.qyl.at/v1/logs");
                    exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                    exporter.Headers = headers;
                });
            });
        });
        _logger = _loggerFactory.CreateLogger(SourceName);
    }

    public static WorkflowTelemetryProjection Create(string? apiKey) => new(apiKey);

    public void Record(WorkflowEventAppend workflowEvent)
    {
        if (_logger is not null)
            s_logJournalEvent(_logger, workflowEvent.Kind, workflowEvent.EventId, null);

        switch (workflowEvent.Kind)
        {
            case WorkflowJournalEventKind.RunCreated:
                Start("run", "codex.workflow.run", workflowEvent, default);
                break;
            case WorkflowJournalEventKind.AttemptStarted:
                Start(
                    AttemptKey(workflowEvent),
                    "codex.workflow.attempt",
                    workflowEvent,
                    Context("run"));
                break;
            case WorkflowJournalEventKind.TurnStarted:
                Start(
                    TurnKey(workflowEvent),
                    "codex.workflow.turn",
                    workflowEvent,
                    AttemptContext(workflowEvent));
                break;
            case WorkflowJournalEventKind.AgentSpawned:
            case WorkflowJournalEventKind.AgentStarted:
                Start(
                    AgentKey(workflowEvent.AgentId),
                    "codex.workflow.agent",
                    workflowEvent,
                    AgentParentContext(workflowEvent));
                break;
            case WorkflowJournalEventKind.ToolStarted:
                Start(
                    ToolKey(workflowEvent),
                    "codex.workflow.tool",
                    workflowEvent,
                    AgentOrAttemptContext(workflowEvent));
                break;
            case WorkflowJournalEventKind.WaitStarted:
                Start(
                    WaitKey(workflowEvent),
                    "codex.workflow.wait",
                    workflowEvent,
                    AgentOrAttemptContext(workflowEvent));
                break;
            case WorkflowJournalEventKind.Joined:
                RecordJoin(workflowEvent);
                break;
            case WorkflowJournalEventKind.ToolCompleted:
                Stop(ToolKey(workflowEvent), workflowEvent);
                break;
            case WorkflowJournalEventKind.WaitCompleted:
                Stop(WaitKey(workflowEvent), workflowEvent);
                break;
            case WorkflowJournalEventKind.AgentCompleted:
                Stop(AgentKey(workflowEvent.AgentId), workflowEvent);
                break;
            case WorkflowJournalEventKind.TurnCompleted:
            case WorkflowJournalEventKind.TurnInterrupted:
                Stop(TurnKey(workflowEvent), workflowEvent);
                break;
            case WorkflowJournalEventKind.AttemptCompleted:
                Stop(AttemptKey(workflowEvent), workflowEvent);
                break;
            case WorkflowJournalEventKind.RunCompleted:
                Stop("run", workflowEvent);
                break;
        }
    }

    public void Dispose()
    {
        foreach (var activity in _activities.Values)
        {
            activity.SetStatus(ActivityStatusCode.Error, "Observer stopped before completion.");
            activity.Dispose();
        }
        _activities.Clear();
        _traces?.ForceFlush(5_000);
        _loggerFactory?.Dispose();
        _traces?.Dispose();
        _source.Dispose();
    }

    private void Start(
        string? key,
        string name,
        WorkflowEventAppend workflowEvent,
        ActivityContext parent)
    {
        if (key is null || _activities.ContainsKey(key))
            return;

        var activity = _source.StartActivity(
            name,
            ActivityKind.Internal,
            parent,
            tags: Tags(workflowEvent),
            links: null,
            startTime: workflowEvent.Timestamp.UtcDateTime);
        if (activity is not null)
            _activities.Add(key, activity);
    }

    private void Stop(string? key, WorkflowEventAppend workflowEvent)
    {
        if (key is null || !_activities.Remove(key, out var activity))
            return;
        var status = Status(workflowEvent);
        if (status is "failed" or "rejected" or "interrupted" or "cancelled")
            activity.SetStatus(ActivityStatusCode.Error, status);
        else
            activity.SetStatus(ActivityStatusCode.Ok);
        activity.SetEndTime(workflowEvent.Timestamp.UtcDateTime);
        activity.Dispose();
    }

    private void RecordJoin(WorkflowEventAppend workflowEvent)
    {
        var links = new List<ActivityLink>(1);
        var receiver = Context(AgentKey(workflowEvent.ReceiverAgentId));
        if (receiver != default)
            links.Add(new ActivityLink(receiver));

        using var activity = _source.StartActivity(
            "codex.workflow.join",
            ActivityKind.Internal,
            AgentOrAttemptContext(workflowEvent),
            Tags(workflowEvent),
            links,
            workflowEvent.Timestamp.UtcDateTime);
        activity?.SetEndTime(workflowEvent.Timestamp.UtcDateTime);
    }

    private ActivityContext AgentParentContext(WorkflowEventAppend workflowEvent)
    {
        var parent = Context(AgentKey(workflowEvent.ParentAgentId));
        return parent != default ? parent : AttemptContext(workflowEvent);
    }

    private ActivityContext AgentOrAttemptContext(WorkflowEventAppend workflowEvent)
    {
        var agent = Context(AgentKey(workflowEvent.AgentId));
        return agent != default ? agent : AttemptContext(workflowEvent);
    }

    private ActivityContext AttemptContext(WorkflowEventAppend workflowEvent)
    {
        var attempt = Context(AttemptKey(workflowEvent));
        return attempt != default ? attempt : Context("run");
    }

    private ActivityContext Context(string? key) =>
        key is not null && _activities.TryGetValue(key, out var activity)
            ? activity.Context
            : default;

    private static IEnumerable<KeyValuePair<string, object?>> Tags(
        WorkflowEventAppend workflowEvent)
    {
        yield return new("workflow.event.id", workflowEvent.EventId);
        yield return new("workflow.event.kind", workflowEvent.Kind.ToString());
        if (workflowEvent.ThreadId is not null)
            yield return new("thread.id", workflowEvent.ThreadId);
        if (workflowEvent.TurnId is not null)
            yield return new("turn.id", workflowEvent.TurnId);
        if (workflowEvent.AttemptId is not null)
            yield return new("workflow.attempt.id", workflowEvent.AttemptId);
        if (workflowEvent.AgentId is not null)
            yield return new("workflow.agent.id", workflowEvent.AgentId);
        if (workflowEvent.ToolCallId is not null)
            yield return new("workflow.tool_call.id", workflowEvent.ToolCallId);
    }

    private static string? Status(WorkflowEventAppend workflowEvent)
    {
        if (workflowEvent.Data is null ||
            !workflowEvent.Data.TryGetValue("status", out var value) ||
            value is null)
        {
            return null;
        }

        return value is JsonElement { ValueKind: JsonValueKind.String } element
            ? element.GetString()
            : value.ToString();
    }

    private static string? AttemptKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.AttemptId is null ? null : $"attempt:{workflowEvent.AttemptId}";

    private static string? TurnKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.TurnId is null ? null : $"turn:{workflowEvent.ThreadId}:{workflowEvent.TurnId}";

    private static string? AgentKey(string? agentId) =>
        agentId is null ? null : $"agent:{agentId}";

    private static string? ToolKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.ToolCallId is null ? null : $"tool:{workflowEvent.ToolCallId}";

    private static string? WaitKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.ToolCallId is null ? null : $"wait:{workflowEvent.ToolCallId}";
}
