
#nullable disable

using System;

namespace Qyl.Domains.Observe.Log
{
    internal static partial class LogPatternTrendExtensions
    {
        public static string ToSerialString(this LogPatternTrend value) => value switch
        {
            LogPatternTrend.Increasing => "increasing",
            LogPatternTrend.Decreasing => "decreasing",
            LogPatternTrend.Stable => "stable",
            LogPatternTrend.New => "new",
            LogPatternTrend.Spike => "spike",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown LogPatternTrend value.")
        };

        public static LogPatternTrend ToLogPatternTrend(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "increasing"))
            {
                return LogPatternTrend.Increasing;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "decreasing"))
            {
                return LogPatternTrend.Decreasing;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "stable"))
            {
                return LogPatternTrend.Stable;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "new"))
            {
                return LogPatternTrend.New;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "spike"))
            {
                return LogPatternTrend.Spike;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown LogPatternTrend value.");
        }
    }
}
