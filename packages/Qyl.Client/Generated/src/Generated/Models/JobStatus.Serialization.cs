
#nullable disable

using System;

namespace Qyl.Domains.Configurator
{
    internal static partial class JobStatusExtensions
    {
        public static string ToSerialString(this JobStatus value) => value switch
        {
            JobStatus.Queued => "queued",
            JobStatus.Running => "running",
            JobStatus.Completed => "completed",
            JobStatus.Failed => "failed",
            JobStatus.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown JobStatus value.")
        };

        public static JobStatus ToJobStatus(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "queued"))
            {
                return JobStatus.Queued;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "running"))
            {
                return JobStatus.Running;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "completed"))
            {
                return JobStatus.Completed;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "failed"))
            {
                return JobStatus.Failed;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "cancelled"))
            {
                return JobStatus.Cancelled;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown JobStatus value.");
        }
    }
}
