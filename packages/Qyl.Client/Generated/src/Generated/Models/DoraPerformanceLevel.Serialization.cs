
#nullable disable

using System;

namespace Qyl.Api
{
    internal static partial class DoraPerformanceLevelExtensions
    {
        public static string ToSerialString(this DoraPerformanceLevel value) => value switch
        {
            DoraPerformanceLevel.Elite => "elite",
            DoraPerformanceLevel.High => "high",
            DoraPerformanceLevel.Medium => "medium",
            DoraPerformanceLevel.Low => "low",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown DoraPerformanceLevel value.")
        };

        public static DoraPerformanceLevel ToDoraPerformanceLevel(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "elite"))
            {
                return DoraPerformanceLevel.Elite;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "high"))
            {
                return DoraPerformanceLevel.High;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "medium"))
            {
                return DoraPerformanceLevel.Medium;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "low"))
            {
                return DoraPerformanceLevel.Low;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown DoraPerformanceLevel value.");
        }
    }
}
