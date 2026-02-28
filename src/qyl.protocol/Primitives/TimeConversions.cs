using System.Runtime.CompilerServices;

namespace Qyl.Common;

/// <summary>
///     Nanosecond / millisecond / DateTimeOffset conversions used across the platform.
///     All OTLP timestamps are stored as Unix nanoseconds (uint64).
/// </summary>
/// <remarks>
///     <para>
///         The qyl codebase uses two UnixNano types:
///         - Protocol (qyl.protocol): <c>long</c> for JSON serialization (signed for JavaScript safety)
///         - Collector (qyl.collector): <c>ulong</c> for OTel fixed64 wire format and DuckDB UBIGINT
///     </para>
///     <para>Valid until year ~2554 (ulong max / 1e9 seconds).</para>
/// </remarks>
public static class TimeConversions
{
    private const long NanosPerMillisecond = 1_000_000L;

    // ── DateTimeOffset → UnixNano ────────────────────────────────────────────

    /// <summary>Converts a <see cref="DateTimeOffset"/> to Unix nanoseconds (signed).</summary>
    public static long ToUnixNano(DateTimeOffset dto) =>
        dto.ToUnixTimeMilliseconds() * NanosPerMillisecond;

    /// <summary>Converts a <see cref="DateTimeOffset"/> to Unix nanoseconds (unsigned).</summary>
    public static ulong ToUnixNanoUnsigned(DateTimeOffset dto) =>
        (ulong)(dto.ToUnixTimeMilliseconds() * NanosPerMillisecond);

    // ── Nanoseconds → Milliseconds ──────────────────────────────────────────

    /// <summary>Converts nanoseconds to milliseconds.</summary>
    public static double NanosToMs(long nanos) => nanos / (double)NanosPerMillisecond;

    /// <summary>Converts unsigned nanoseconds to milliseconds.</summary>
    public static double NanosToMs(ulong nanos) => nanos / (double)NanosPerMillisecond;

    // ── UnixNano → DateTimeOffset / DateTime ────────────────────────────────

    /// <summary>Converts signed Unix nanoseconds to a <see cref="DateTimeOffset"/>.</summary>
    public static DateTimeOffset NanosToDateTimeOffset(long nanos) =>
        DateTimeOffset.FromUnixTimeMilliseconds(nanos / NanosPerMillisecond);

    /// <summary>Converts unsigned Unix nanoseconds to a <see cref="DateTimeOffset"/>.</summary>
    public static DateTimeOffset NanosToDateTimeOffset(ulong nanos) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)(nanos / (ulong)NanosPerMillisecond));

    /// <summary>
    ///     Converts Unix nanoseconds (ulong) to DateTime (UTC) via ticks (100ns precision).
    /// </summary>
    /// <remarks>
    ///     Uses tick-based conversion (nanos/100) for higher precision than millisecond rounding.
    ///     Any ulong nanosecond value is safe: ulong.MaxValue / 100 = ~184 quintillion ticks ≈ year 2554.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixNanoToDateTime(ulong unixNano)
    {
        var ticks = (long)(unixNano / 100);
        return DateTime.UnixEpoch.AddTicks(ticks);
    }
}
