
#nullable disable

using System;

namespace Qyl.Domains.Observe.Error
{
    internal static partial class ErrorTrendExtensions
    {
        public static string ToSerialString(this ErrorTrend value) => value switch
        {
            ErrorTrend.Increasing => "increasing",
            ErrorTrend.Decreasing => "decreasing",
            ErrorTrend.Stable => "stable",
            ErrorTrend.Spike => "spike",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ErrorTrend value.")
        };

        public static ErrorTrend ToErrorTrend(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "increasing"))
            {
                return ErrorTrend.Increasing;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "decreasing"))
            {
                return ErrorTrend.Decreasing;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "stable"))
            {
                return ErrorTrend.Stable;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "spike"))
            {
                return ErrorTrend.Spike;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ErrorTrend value.");
        }
    }
}
