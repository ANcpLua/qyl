
#nullable disable

using System;

namespace Qyl.Domains.Issues
{
    internal static partial class IssueLevelExtensions
    {
        public static string ToSerialString(this IssueLevel value) => value switch
        {
            IssueLevel.Debug => "debug",
            IssueLevel.Info => "info",
            IssueLevel.Warning => "warning",
            IssueLevel.Error => "error",
            IssueLevel.Fatal => "fatal",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown IssueLevel value.")
        };

        public static IssueLevel ToIssueLevel(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "debug"))
            {
                return IssueLevel.Debug;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "info"))
            {
                return IssueLevel.Info;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "warning"))
            {
                return IssueLevel.Warning;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "error"))
            {
                return IssueLevel.Error;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "fatal"))
            {
                return IssueLevel.Fatal;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown IssueLevel value.");
        }
    }
}
