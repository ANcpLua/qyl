using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Qyl.Observability.Evaluation.Models;

namespace Qyl.Observability.Evaluation.Evaluators;

public sealed class TraceCorrelationEvaluator(ObservabilityEvaluationRecord record) : IEvaluator
{
    public IReadOnlyCollection<string> EvaluationMetricNames => [ObservabilityMetricNames.TraceCorrelation];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        AnalysisResult analysis = Analyze(record);
        BooleanMetric metric = EvaluationMetricFactory.CreateBoolean(ObservabilityMetricNames.TraceCorrelation, analysis.Passed, analysis.Reason);
        return new ValueTask<EvaluationResult>(new EvaluationResult(metric));
    }

    public static AnalysisResult Analyze(ObservabilityEvaluationRecord record)
    {
        List<TelemetryEvidenceRecord> spans = [.. record.Telemetry.Where(static telemetry => IsSpan(telemetry) && telemetry.SpanId is not null)];
        HashSet<string> spanKeys = [.. spans.Select(static span => BuildSpanKey(span.TraceId, span.SpanId))];
        List<string> missingParents = [];

        foreach (TelemetryEvidenceRecord span in spans)
        {
            if (span.ParentSpanId is null)
            {
                continue;
            }

            string parentKey = BuildSpanKey(span.TraceId, span.ParentSpanId);
            if (!spanKeys.Contains(parentKey))
            {
                missingParents.Add($"{span.Id}:{span.ParentSpanId}");
            }
        }

        return missingParents.Count == 0
            ? AnalysisResult.Pass("All span parent references resolve within their trace.")
            : AnalysisResult.Fail($"missing parent spans: {string.Join(", ", missingParents)}");
    }

    private static bool IsSpan(TelemetryEvidenceRecord telemetry)
        => telemetry.SignalType.Equals("span", StringComparison.OrdinalIgnoreCase);

    private static string BuildSpanKey(string? traceId, string? spanId)
        => string.Concat(traceId ?? string.Empty, "/", spanId ?? string.Empty);
}
