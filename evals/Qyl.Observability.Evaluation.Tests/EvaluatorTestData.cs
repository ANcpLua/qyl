using System.Text.Json;
using Qyl.Observability.Evaluation.Models;

namespace Qyl.Observability.Evaluation.Tests;

internal static class EvaluatorTestData
{
    public static ObservabilityEvaluationRecord Record(
        string finalResponse = "",
        IReadOnlyList<ToolCallRecord>? toolCalls = null,
        IReadOnlyList<ExpectedToolCallRecord>? expectedToolCalls = null,
        IReadOnlyList<TelemetryEvidenceRecord>? telemetry = null,
        IReadOnlyList<string>? requiredEvidenceIds = null,
        IReadOnlyList<string>? forbiddenClaims = null,
        IReadOnlyList<string>? expectedFailedMetrics = null,
        bool shouldPass = true)
    {
        return new ObservabilityEvaluationRecord
        {
            Id = "record",
            Source = "tests",
            Scenario = "scenario",
            Agent = new AgentInfo
            {
                Name = "QylIncidentAgent",
                ModelProvider = "fixture",
                ModelName = "deterministic",
                Instructions = "Use qyl observability tools.",
                Tools = ["qyl.query.traces", "qyl.query.metrics", "qyl.query.logs"]
            },
            UserInput = "diagnose incident",
            ToolCalls = toolCalls ?? [],
            FinalResponse = finalResponse,
            Telemetry = telemetry ?? [],
            RequiredEvidenceIds = requiredEvidenceIds ?? [],
            ForbiddenClaims = forbiddenClaims ?? [],
            ExpectedToolCalls = expectedToolCalls ?? [],
            ExpectedFailedMetrics = expectedFailedMetrics ?? [],
            ShouldPass = shouldPass
        };
    }

    public static ToolCallRecord ToolCall(string name, string argumentsJson = "{}")
    {
        return new ToolCallRecord
        {
            Name = name,
            Arguments = ParseObject(argumentsJson),
            ResultSummary = "ok"
        };
    }

    public static ExpectedToolCallRecord ExpectedToolCall(string name, string argumentsJson = "{}")
    {
        return new ExpectedToolCallRecord
        {
            Name = name,
            Arguments = ParseObject(argumentsJson)
        };
    }

    public static TelemetryEvidenceRecord Span(
        string id,
        string? traceId,
        string? spanId,
        string? parentSpanId = null,
        string attributesJson = "{}")
    {
        return new TelemetryEvidenceRecord
        {
            Id = id,
            SignalType = "span",
            ServiceName = "checkout-api",
            Operation = "POST /checkout",
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            Attributes = ParseObject(attributesJson)
        };
    }

    public static TelemetryEvidenceRecord Metric(string id)
    {
        return new TelemetryEvidenceRecord
        {
            Id = id,
            SignalType = "metric",
            ServiceName = "checkout-api",
            Operation = "http.server.duration",
            Attributes = ParseObject("""{"metric":"http.server.duration"}""")
        };
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseObject(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(static property => property.Name, static property => property.Value.Clone(), StringComparer.Ordinal);
    }
}
