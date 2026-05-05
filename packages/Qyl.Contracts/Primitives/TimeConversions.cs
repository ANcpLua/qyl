using System.Runtime.CompilerServices;

namespace Qyl.Contracts.Primitives;

public static class TimeConversions
{
    private const long NanosPerMillisecond = 1_000_000L;


    public static long ToUnixNano(DateTimeOffset dto) =>
        dto.ToUnixTimeMilliseconds() * NanosPerMillisecond;

    public static ulong ToUnixNanoUnsigned(DateTimeOffset dto) =>
        (ulong)(dto.ToUnixTimeMilliseconds() * NanosPerMillisecond);


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
