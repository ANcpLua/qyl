namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
/// Allocation-free, reflection-free trace-id sampling primitive used by
/// <see cref="QylAotSampler"/>.
/// The ratio algorithm mirrors the OpenTelemetry <c>TraceIdRatioBased</c> sampler so that a single
/// trace is decided consistently — the decision depends only on the trace id, never on wall-clock,
/// randomness, or process-local state, which is exactly what makes it NativeAOT-safe (no JIT,
/// no reflection, no shared mutable state).
/// </summary>
public static class QylTraceSampling
{
    /// <summary>
    /// Deterministic ratio test over a trace id: the same trace id always yields the same result, so
    /// every span in a trace agrees. Reads the upper 64 bits of the trace id as a positive 63-bit
    /// value and compares against <c>ratio · long.MaxValue</c>. Uses a stack buffer — zero heap
    /// allocation, zero reflection.
    /// </summary>
    public static bool IsSampledByRatio(ActivityTraceId traceId, double ratio)
    {
        if (ratio >= 1.0)
            return true;
        if (ratio <= 0.0)
            return false;

        Span<byte> bytes = stackalloc byte[16];
        traceId.CopyTo(bytes);

        long value = 0;
        for (var i = 0; i < 8; i++)
            value = (value << 8) | bytes[i];

        // Clear the sign bit -> [0, long.MaxValue]. Equivalent in intent to OpenTelemetry's
        // Math.Abs(...) but avoids the Math.Abs(long.MinValue) overflow on a pathological trace id.
        value &= long.MaxValue;

        return value < (long)(ratio * long.MaxValue);
    }
}
