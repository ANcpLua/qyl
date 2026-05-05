
#nullable disable

using System;

namespace Qyl.Domains.Alerting
{
    internal static partial class FixTriggerTypeExtensions
    {
        public static string ToSerialString(this FixTriggerType value) => value switch
        {
            FixTriggerType.Alert => "alert",
            FixTriggerType.Manual => "manual",
            FixTriggerType.Mcp => "mcp",
            FixTriggerType.Scheduled => "scheduled",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown FixTriggerType value.")
        };

        public static FixTriggerType ToFixTriggerType(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "alert"))
            {
                return FixTriggerType.Alert;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "manual"))
            {
                return FixTriggerType.Manual;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "mcp"))
            {
                return FixTriggerType.Mcp;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "scheduled"))
            {
                return FixTriggerType.Scheduled;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown FixTriggerType value.");
        }
    }
}
