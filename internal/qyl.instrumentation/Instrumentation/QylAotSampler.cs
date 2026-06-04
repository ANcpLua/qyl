using System.Collections.Frozen;
using OpenTelemetry.Trace;

namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
/// Maximally AOT-native trace sampler. It is <c>sealed</c> (so <see cref="ShouldSample"/> is
/// devirtualized), allocation-free and reflection-free on the hot path, and does <b>not</b> delegate
/// to a nested <c>ParentBasedSampler</c>/<c>AlwaysOnSampler</c> pair — the parent-based behaviour is
/// implemented inline. Every field is captured once at construction; <see cref="ShouldSample"/>
/// returns a stack-only <see cref="SamplingResult"/> (a readonly struct), so a sampling decision
/// allocates nothing and runs no <see cref="System.Reflection"/> code. This is what the official
/// CLR-profiler-based OpenTelemetry auto-instrumentation cannot offer under NativeAOT.
/// </summary>
/// <remarks>
/// Behaviour by <see cref="ObservabilityMode"/> (matching the samplers it replaces):
/// <list type="bullet">
/// <item><see cref="ObservabilityMode.OnDemand"/> — drop everything, ignore the parent (was <c>AlwaysOffSampler</c>).</item>
/// <item><see cref="ObservabilityMode.Warm"/> — roots dropped, children follow the parent flag (was <c>ParentBasedSampler(AlwaysOffSampler)</c>).</item>
/// <item><see cref="ObservabilityMode.AlwaysOn"/> — roots sampled at <c>rootRatio</c>, children follow the parent flag (was <c>ParentBasedSampler(AlwaysOnSampler)</c>).</item>
/// </list>
/// Per-operation ratios apply only to <b>root</b> spans (a child must follow its parent's
/// head-sampling decision or the trace tree breaks) and override the mode's root default in both
/// <see cref="ObservabilityMode.AlwaysOn"/> and <see cref="ObservabilityMode.Warm"/> —
/// <see cref="ObservabilityMode.OnDemand"/> drops everything first, so they are ignored there.
/// To suppress a noisy <i>child</i> span use the compile-time
/// <see cref="QylNoTraceAttribute"/>/<see cref="QylSampleAttribute"/> lever instead.
/// </remarks>
public sealed class QylAotSampler : Sampler
{
    private readonly bool _dropAll;
    private readonly double _rootRatio;
    private readonly FrozenDictionary<string, double>? _rootOperationRatios;

    /// <summary>Creates the sampler for the given observability mode.</summary>
    /// <param name="mode">Selects the parent/root policy.</param>
    /// <param name="rootRatio">Ratio applied to root spans in <see cref="ObservabilityMode.AlwaysOn"/> (default 1.0 = always).</param>
    /// <param name="rootOperationRatios">
    /// Optional per-operation ratio overrides for <b>root</b> spans, keyed by activity/operation name
    /// (e.g. <c>"SELECT"</c>), matched case-insensitively. Projected once into a
    /// <see cref="FrozenDictionary{TKey,TValue}"/> for a branch-free hot-path lookup.
    /// </param>
    public QylAotSampler(
        ObservabilityMode mode,
        double rootRatio = 1.0,
        IReadOnlyDictionary<string, double>? rootOperationRatios = null)
    {
        _dropAll = mode == ObservabilityMode.OnDemand;
        _rootRatio = mode switch
        {
            ObservabilityMode.OnDemand => 0.0,
            ObservabilityMode.Warm => 0.0,
            _ => rootRatio
        };
        _rootOperationRatios = rootOperationRatios is { Count: > 0 }
            ? rootOperationRatios.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)
            : null;

        Description = $"QylAotSampler{{mode={mode}, rootRatio={_rootRatio}}}";
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (_dropAll)
            return new SamplingResult(SamplingDecision.Drop);

        // Parent-based: a valid parent's recorded flag wins, keeping the trace tree consistent
        // across processes (the W3C traceparent sampled bit is authoritative for children).
        var parent = samplingParameters.ParentContext;
        if (parent.TraceId != default)
        {
            return (parent.TraceFlags & ActivityTraceFlags.Recorded) is not 0
                ? new SamplingResult(SamplingDecision.RecordAndSample)
                : new SamplingResult(SamplingDecision.Drop);
        }

        // Root span: per-operation override (root only), else the mode's root ratio.
        var ratio = _rootRatio;
        if (_rootOperationRatios is not null && _rootOperationRatios.TryGetValue(samplingParameters.Name, out var overrideRatio))
            ratio = overrideRatio;

        return QylTraceSampling.IsSampledByRatio(samplingParameters.TraceId, ratio)
            ? new SamplingResult(SamplingDecision.RecordAndSample)
            : new SamplingResult(SamplingDecision.Drop);
    }
}
