
#nullable disable

using System;

namespace Qyl.Common.Pagination
{
    internal static partial class TimeBucketExtensions
    {
        public static string ToSerialString(this TimeBucket value) => value switch
        {
            TimeBucket.Minute => "1m",
            TimeBucket.FiveMinutes => "5m",
            TimeBucket.FifteenMinutes => "15m",
            TimeBucket.Hour => "1h",
            TimeBucket.Day => "1d",
            TimeBucket.Week => "1w",
            TimeBucket.Auto => "auto",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown TimeBucket value.")
        };

        public static TimeBucket ToTimeBucket(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "1m"))
            {
                return TimeBucket.Minute;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "5m"))
            {
                return TimeBucket.FiveMinutes;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "15m"))
            {
                return TimeBucket.FifteenMinutes;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "1h"))
            {
                return TimeBucket.Hour;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "1d"))
            {
                return TimeBucket.Day;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "1w"))
            {
                return TimeBucket.Week;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "auto"))
            {
                return TimeBucket.Auto;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown TimeBucket value.");
        }
    }
}
