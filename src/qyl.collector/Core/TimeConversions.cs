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

    /// <summary>
    ///     Converts DateTime to Unix nanoseconds (ulong).
    ///     Inverse of <see cref="UnixNanoToDateTime" />.
    ///     Cast to ulong BEFORE multiplication to use unsigned arithmetic.
    /// </summary>
    /// <param name="dateTime">DateTime to convert (converted to UTC internally).</param>
    /// <returns>Nanoseconds since 1970-01-01T00:00:00Z.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong DateTimeToUnixNano(DateTime dateTime)
    {
        var utc = dateTime.ToUniversalTime();
        var ticks = (utc - DateTime.UnixEpoch).Ticks;
        // Cast to ulong before multiplication to avoid signed overflow
        return (ulong)ticks * 100UL;
    }

    /// <summary>
    ///     Converts DateTimeOffset to Unix nanoseconds (ulong).
    ///     Cast to ulong BEFORE multiplication to use unsigned arithmetic.
    /// </summary>
    /// <param name="dateTimeOffset">DateTimeOffset to convert.</param>
    /// <returns>Nanoseconds since 1970-01-01T00:00:00Z.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong DateTimeOffsetToUnixNano(DateTimeOffset dateTimeOffset)
    {
        var ticks = (dateTimeOffset.UtcDateTime - DateTime.UnixEpoch).Ticks;
        // Cast to ulong before multiplication to avoid signed overflow
        return (ulong)ticks * 100UL;
    }

    /// <summary>
    ///     Converts Unix nanoseconds (ulong) to DateTimeOffset (UTC).
    /// </summary>
    /// <param name="unixNano">Nanoseconds since 1970-01-01T00:00:00Z.</param>
    /// <returns>DateTimeOffset with zero UTC offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTimeOffset UnixNanoToDateTimeOffset(ulong unixNano)
    {
        // Division by 100 ensures result fits in long (max ~184 quintillion ticks)
        var ticks = (long)(unixNano / 100);
        return new DateTimeOffset(DateTime.UnixEpoch.AddTicks(ticks), TimeSpan.Zero);
    }

    /// <summary>
    ///     Gets current UTC time as Unix nanoseconds.
    /// </summary>
    /// <param name="provider">Optional TimeProvider (defaults to System).</param>
    /// <returns>Current nanoseconds since 1970-01-01T00:00:00Z.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetCurrentUnixNano(TimeProvider? provider = null)
    {
        var now = (provider ?? TimeProvider.System).GetUtcNow();
        return DateTimeOffsetToUnixNano(now);
    }
}
