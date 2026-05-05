
#nullable disable

using System;

namespace Qyl.Domains.Issues
{
    internal static partial class IssuePriorityExtensions
    {
        public static string ToSerialString(this IssuePriority value) => value switch
        {
            IssuePriority.Critical => "critical",
            IssuePriority.High => "high",
            IssuePriority.Medium => "medium",
            IssuePriority.Low => "low",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown IssuePriority value.")
        };

        public static IssuePriority ToIssuePriority(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "critical"))
            {
                return IssuePriority.Critical;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "high"))
            {
                return IssuePriority.High;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "medium"))
            {
                return IssuePriority.Medium;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "low"))
            {
                return IssuePriority.Low;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown IssuePriority value.");
        }
    }
}
