using OpenTelemetry.Trace;

namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
/// Maximally AOT-native trace sampler. It is <c>sealed</c> (so <see cref="ShouldSample"/> is
/// devirtualized), allocation-free and reflection-free on the hot path, and does <b>not</b> delegate
/// to a nested <c>ParentBasedSampler</c>/<c>AlwaysOnSampler</c> pair — the parent-based behaviour is
/// implemented inline. <see cref="ShouldSample"/> returns a stack-only <see cref="SamplingResult"/>
/// (a readonly struct), so a sampling decision allocates nothing and runs no
/// <see cref="System.Reflection"/> code. This is what the official CLR-profiler-based OpenTelemetry
/// auto-instrumentation cannot offer under NativeAOT.
/// </summary>
/// <remarks>
/// Every root span is sampled; children follow their parent's head-sampling decision (the W3C
/// traceparent sampled bit is authoritative), so the trace tree stays consistent across processes.
/// </remarks>
public sealed class QylAotSampler : Sampler
{
    public QylAotSampler()
    {
        Description = "QylAotSampler{sampleAllRoots}";
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // Parent-based: a valid parent's recorded flag wins, keeping the trace tree consistent
        // across processes (the W3C traceparent sampled bit is authoritative for children).
        var parent = samplingParameters.ParentContext;
        if (parent.TraceId != default)
        {
            return (parent.TraceFlags & ActivityTraceFlags.Recorded) is not 0
                ? new SamplingResult(SamplingDecision.RecordAndSample)
                : new SamplingResult(SamplingDecision.Drop);
        }

        // Root span: always sampled.
        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
