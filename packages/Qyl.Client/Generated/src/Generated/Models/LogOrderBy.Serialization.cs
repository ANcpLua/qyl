
#nullable disable

using System;

namespace Qyl.Domains.Observe.Log
{
    internal static partial class LogOrderByExtensions
    {
        public static string ToSerialString(this LogOrderBy value) => value switch
        {
            LogOrderBy.TimestampAsc => "timestamp_asc",
            LogOrderBy.TimestampDesc => "timestamp_desc",
            LogOrderBy.SeverityAsc => "severity_asc",
            LogOrderBy.SeverityDesc => "severity_desc",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown LogOrderBy value.")
        };

        public static LogOrderBy ToLogOrderBy(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "timestamp_asc"))
            {
                return LogOrderBy.TimestampAsc;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "timestamp_desc"))
            {
                return LogOrderBy.TimestampDesc;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "severity_asc"))
            {
                return LogOrderBy.SeverityAsc;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "severity_desc"))
            {
                return LogOrderBy.SeverityDesc;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown LogOrderBy value.");
        }
    }
}
