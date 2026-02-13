// =============================================================================
// TimeConversions - Unified time conversion utilities
// Single source of truth for UnixNano <-> DateTime conversions
// =============================================================================

namespace qyl.collector.Core;

/// <summary>
///     Unified time conversion utilities for Unix nanosecond timestamps.
///     OTel uses fixed64 (ulong) for timestamps representing nanoseconds since Unix epoch.
/// </summary>
/// <remarks>
///     <para>
///         ARCHITECTURAL NOTE: The qyl codebase uses two UnixNano types:
///         - Protocol (qyl.protocol): <c>UnixNano(long)</c> for JSON serialization (signed for JavaScript safety)
///         - Collector (qyl.collector): <c>UnixNano(ulong)</c> for OTel fixed64 wire format and DuckDB UBIGINT
///     </para>
///     <para>
///         This class provides conversions using <c>ulong</c> (collector internal representation).
///         Valid until year ~2554 (ulong max / 1e9 seconds).
///     </para>
/// </remarks>
public static class TimeConversions
{
    /// <summary>
    ///     Converts Unix nanoseconds (ulong) to DateTime (UTC).
    ///     1 tick = 100 nanoseconds, so divide by 100 to get ticks.
    /// </summary>
    /// <remarks>
    ///     Analysis shows any ulong nanosecond value is safe:
    ///     - ulong.MaxValue / 100 = 184,467,440,737,095,516 ticks
    ///     - DateTime.UnixEpoch + max_ticks = year ~2554 (well within DateTime.MaxValue)
    /// </remarks>
    /// <param name="unixNano">Nanoseconds since 1970-01-01T00:00:00Z.</param>
    /// <returns>DateTime in UTC.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixNanoToDateTime(ulong unixNano)
    {
        // Division by 100 ensures result fits in long (max ~184 quintillion ticks)
        var ticks = (long)(unixNano / 100);
        return DateTime.UnixEpoch.AddTicks(ticks);
    }

}
