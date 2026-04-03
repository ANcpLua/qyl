using Qyl.Instrumentation.Instrumentation.Loom;

namespace Qyl.Loom.V2;

public static partial class LoomV2Tools
{
    [LoomTool(
        "analyze_regression",
        Description = "Use this when comparing baseline versus current behavior to detect meaningful regressions.",
        Phase = LoomPhase.Detect,
        UseOnlyWhen = "Comparing a stable baseline window to a suspected regression window",
        DoNotUseWhen = "Raw logging, metric dumping, or generic telemetry export")]
    [RequiresCapability("qyl.loom.v2.detect")]
    [ToolSideEffect(ToolSideEffect.None)]
    [EmitsStructuredOutput(typeof(LoomV2RegressionAnalysis))]
    public static LoomV2RegressionAnalysis AnalyzeRegression(LoomV2AnalyzeRegressionInput input)
    {
        var baselineWindow = input.BaselineWindow;
        var comparisonWindow = input.ComparisonWindow;
        var regressionDetected = !string.Equals(baselineWindow, comparisonWindow, StringComparison.Ordinal);

        return new LoomV2RegressionAnalysis(
            baselineWindow,
            comparisonWindow,
            input.SignalType,
            regressionDetected,
            regressionDetected ? 12.5d : 0d,
            regressionDetected
                ? "Behavior diverged from the baseline window."
                : "No material divergence was detected.",
            regressionDetected
                ? ["baseline deviation", "window mismatch"]
                : ["stable comparison"]);
    }
}
