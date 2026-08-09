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
    private const string DiagnosticEventName = "qyl.agent.diagnostic.snapshot";
    private const string DiagnosticExtensionId = "qyl.agent.diagnostic.extension.id";
    private const string DiagnosticFormatVersion = "qyl.agent.diagnostic.format.version";
    private const string DiagnosticSnapshotId = "qyl.agent.diagnostic.snapshot.id";
    private const string DiagnosticProbeId = "qyl.agent.diagnostic.probe.id";
    private const string DiagnosticPhase = "qyl.agent.diagnostic.phase";
    private const string DiagnosticOutcome = "qyl.agent.diagnostic.outcome";
    private const string DiagnosticVariableCount = "qyl.agent.diagnostic.variable.count";
    private const string DiagnosticCheckCount = "qyl.agent.diagnostic.check.count";
    private const string DiagnosticFailedCheckCount = "qyl.agent.diagnostic.check.failed_count";
    private const string WorkflowRunId = "qyl.workflow.run.id";
    private const string WorkflowEventId = "qyl.workflow.event.id";
    private const string WorkflowAttemptId = "qyl.workflow.attempt.id";
    private const string WorkflowAgentId = "qyl.workflow.agent.id";
    private const string WorkflowToolCallId = "qyl.workflow.tool_call.id";
    private static readonly Action<ILogger, WorkflowJournalEventKind, string, Exception?>
        s_logJournalEvent = LoggerMessage.Define<WorkflowJournalEventKind, string>(
            LogLevel.Information,
            new EventId(1, "WorkflowJournalEvent"),
            "Workflow journal event {EventKind} {EventId}");

    private readonly ActivitySource _source = new(SourceName, BuildVersion.ProductVersion);
    private readonly string _runId;
    private readonly Dictionary<string, Activity> _activities = new(StringComparer.Ordinal);
    private readonly TracerProvider? _traces;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger? _logger;

    private WorkflowTelemetryProjection(string runId, string? apiKey)
    {
        _runId = runId;
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

    public static WorkflowTelemetryProjection Create(string runId, string? apiKey) =>
        new(runId, apiKey);

    public void Record(WorkflowEventAppend workflowEvent)
    {
        if (_logger is not null)
            s_logJournalEvent(_logger, workflowEvent.Kind, workflowEvent.EventId.Value, null);

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
            case WorkflowJournalEventKind.ContentCaptured:
                RecordDiagnosticSnapshot(workflowEvent);
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

    private void RecordDiagnosticSnapshot(WorkflowEventAppend workflowEvent)
    {
        if (DataString(workflowEvent, "extension_id") != DiagnosticSnapshotCapture.ExtensionId ||
            DataInt64(workflowEvent, "format_version") != DiagnosticSnapshotCapture.FormatVersion)
        {
            return;
        }
        var activity = ActiveActivity(workflowEvent);
        if (activity is null)
            return;

        var tags = new ActivityTagsCollection
        {
            [DiagnosticExtensionId] = DataString(workflowEvent, "extension_id"),
            [DiagnosticFormatVersion] = DataInt64(workflowEvent, "format_version"),
            [DiagnosticSnapshotId] = DataString(workflowEvent, "snapshot_id"),
            [DiagnosticProbeId] = DataString(workflowEvent, "probe_id"),
            [DiagnosticPhase] = DataString(workflowEvent, "phase"),
            [DiagnosticOutcome] = DataString(workflowEvent, "outcome"),
            [DiagnosticVariableCount] = DataInt64(workflowEvent, "variable_count"),
            [DiagnosticCheckCount] = DataInt64(workflowEvent, "check_count"),
            [DiagnosticFailedCheckCount] = DataInt64(workflowEvent, "failed_check_count"),
            [WorkflowRunId] = _runId,
            [WorkflowEventId] = workflowEvent.EventId.Value
        };
        if (workflowEvent.AttemptId is not null)
            tags[WorkflowAttemptId] = workflowEvent.AttemptId.Value.Value;
        if (workflowEvent.AgentId is not null)
            tags[WorkflowAgentId] = workflowEvent.AgentId.Value.Value;
        if (workflowEvent.ToolCallId is not null)
            tags[WorkflowToolCallId] = workflowEvent.ToolCallId.Value.Value;
        activity.AddEvent(new ActivityEvent(DiagnosticEventName, workflowEvent.Timestamp, tags));
    }

    private Activity? ActiveActivity(WorkflowEventAppend workflowEvent)
    {
        foreach (var key in new[]
                 {
                     AgentKey(workflowEvent.AgentId),
                     TurnKey(workflowEvent),
                     AttemptKey(workflowEvent),
                     "run"
                 })
        {
            if (key is not null && _activities.TryGetValue(key, out var activity))
                return activity;
        }
        return null;
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
        yield return new("workflow.event.id", workflowEvent.EventId.Value);
        yield return new("workflow.event.kind", workflowEvent.Kind.ToString());
        if (workflowEvent.ThreadId is not null)
            yield return new("thread.id", workflowEvent.ThreadId);
        if (workflowEvent.TurnId is not null)
            yield return new("turn.id", workflowEvent.TurnId);
        if (workflowEvent.AttemptId is not null)
            yield return new("workflow.attempt.id", workflowEvent.AttemptId.Value.Value);
        if (workflowEvent.AgentId is not null)
            yield return new("workflow.agent.id", workflowEvent.AgentId.Value.Value);
        if (workflowEvent.ToolCallId is not null)
            yield return new("workflow.tool_call.id", workflowEvent.ToolCallId.Value.Value);
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

    private static string? DataString(WorkflowEventAppend workflowEvent, string key)
    {
        if (workflowEvent.Data is null || !workflowEvent.Data.TryGetValue(key, out var value))
            return null;
        return value is JsonElement { ValueKind: JsonValueKind.String } element
            ? element.GetString()
            : value?.ToString();
    }

    private static long? DataInt64(WorkflowEventAppend workflowEvent, string key)
    {
        if (workflowEvent.Data is null || !workflowEvent.Data.TryGetValue(key, out var value))
            return null;
        if (value is JsonElement { ValueKind: JsonValueKind.Number } element &&
            element.TryGetInt64(out var jsonValue))
        {
            return jsonValue;
        }
        return value switch
        {
            byte item => item,
            short item => item,
            int item => item,
            long item => item,
            _ => null
        };
    }

    private static string? AttemptKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.AttemptId is null ? null : $"attempt:{workflowEvent.AttemptId.Value.Value}";

    private static string? TurnKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.TurnId is null ? null : $"turn:{workflowEvent.ThreadId}:{workflowEvent.TurnId}";

    private static string? AgentKey(WorkflowAgentId? agentId) =>
        agentId is null ? null : $"agent:{agentId.Value.Value}";

    private static string? ToolKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.ToolCallId is null ? null : $"tool:{workflowEvent.ToolCallId.Value.Value}";

    private static string? WaitKey(WorkflowEventAppend workflowEvent) =>
        workflowEvent.ToolCallId is null ? null : $"wait:{workflowEvent.ToolCallId.Value.Value}";
}
