// =============================================================================
// qyl.copilot - OpenTelemetry Instrumentation
// qyl-specific metrics for workflow execution
// gen_ai.* metrics are handled by SDK's UseOpenTelemetry()
// =============================================================================

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace qyl.copilot.Instrumentation;

/// <summary>
///     OpenTelemetry instrumentation for qyl Copilot workflows.
///     SDK handles gen_ai.* metrics via UseOpenTelemetry(); this provides qyl-specific metrics.
/// </summary>
public static class CopilotInstrumentation
{
    /// <summary>
    ///     Provider name for GitHub Copilot (OTel 1.39 gen_ai.provider.name).
    /// </summary>
    public const string ProviderName = "github_copilot";

    /// <summary>
    ///     ActivitySource name for Copilot instrumentation.
    ///     Used by SDK's UseOpenTelemetry() for gen_ai.* spans.
    /// </summary>
    public const string SourceName = "qyl.copilot";

    /// <summary>
    ///     Meter name for qyl-specific Copilot metrics.
    /// </summary>
    public const string MeterName = "qyl.copilot";

    /// <summary>
    ///     ActivitySource for creating spans.
    ///     SDK's UseOpenTelemetry() creates gen_ai.* spans; this is for qyl-specific spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    /// <summary>
    ///     Meter for creating qyl-specific metrics.
    /// </summary>
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // =========================================================================
    // qyl-Specific Workflow Metrics
    // SDK handles gen_ai.client.token.usage and gen_ai.client.operation.duration
    // =========================================================================

    /// <summary>
    ///     qyl.copilot.workflow.duration - Workflow execution duration.
    ///     Distinct from gen_ai.client.operation.duration which SDK handles.
    /// </summary>
    public static readonly Histogram<double> WorkflowDuration = Meter.CreateHistogram<double>(
        "qyl.copilot.workflow.duration",
        unit: "s",
        description: "Duration of qyl workflow executions in seconds");

    /// <summary>
    ///     qyl.copilot.workflow.executions - Workflow execution count.
    /// </summary>
    public static readonly Counter<long> WorkflowExecutions = Meter.CreateCounter<long>(
        "qyl.copilot.workflow.executions",
        unit: "{execution}",
        description: "Number of qyl workflow executions");
}
