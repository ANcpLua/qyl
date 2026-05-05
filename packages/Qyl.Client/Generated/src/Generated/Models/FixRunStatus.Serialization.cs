
#nullable disable

using System;

namespace Qyl.Domains.Alerting
{
    internal static partial class FixRunStatusExtensions
    {
        public static string ToSerialString(this FixRunStatus value) => value switch
        {
            FixRunStatus.Pending => "pending",
            FixRunStatus.Running => "running",
            FixRunStatus.AwaitingApproval => "awaiting_approval",
            FixRunStatus.Applied => "applied",
            FixRunStatus.Rejected => "rejected",
            FixRunStatus.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown FixRunStatus value.")
        };

        public static FixRunStatus ToFixRunStatus(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "pending"))
            {
                return FixRunStatus.Pending;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "running"))
            {
                return FixRunStatus.Running;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "awaiting_approval"))
            {
                return FixRunStatus.AwaitingApproval;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "applied"))
            {
                return FixRunStatus.Applied;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "rejected"))
            {
                return FixRunStatus.Rejected;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "failed"))
            {
                return FixRunStatus.Failed;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown FixRunStatus value.");
        }
    }
}
