using System.Runtime.CompilerServices;

namespace Qyl.Collector.Primitives;

internal static class TimeConversions
{
    private const long NanosPerMillisecond = 1_000_000L;
    private const ulong NanosPerTick = 100UL;

    public static long ToUnixNano(DateTimeOffset dto) =>
        dto.ToUnixTimeMilliseconds() * NanosPerMillisecond;

    public static ulong ToUnixNanoUnsigned(DateTimeOffset dto) =>
        TryToUnixNanoUnsigned(dto, out var unixNano)
            ? unixNano
            : throw new ArgumentOutOfRangeException(
                nameof(dto),
                dto,
                "Timestamp is outside the unsigned OTLP Unix-nanosecond range.");

    public static bool TryToUnixNanoUnsigned(DateTimeOffset dto, out ulong unixNano)
    {
        unixNano = 0;
        var utcTicks = dto.ToUniversalTime().Ticks;
        var unixEpochTicks = DateTimeOffset.UnixEpoch.Ticks;
        if (utcTicks < unixEpochTicks)
            return false;

        var elapsedTicks = (ulong)(utcTicks - unixEpochTicks);
        if (elapsedTicks > ulong.MaxValue / NanosPerTick)
            return false;

        unixNano = elapsedTicks * NanosPerTick;
        return true;
    }

    public static double NanosToMs(long nanos) => nanos / (double)NanosPerMillisecond;

    public static double NanosToMs(ulong nanos) => nanos / (double)NanosPerMillisecond;

    public static DateTimeOffset NanosToDateTimeOffset(long nanos) =>
        DateTimeOffset.FromUnixTimeMilliseconds(nanos / NanosPerMillisecond);

    public static DateTimeOffset NanosToDateTimeOffset(ulong nanos) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)(nanos / NanosPerMillisecond));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime UnixNanoToDateTime(ulong unixNano)
    {
        var ticks = (long)(unixNano / 100);
        return DateTime.UnixEpoch.AddTicks(ticks);
    }
}
