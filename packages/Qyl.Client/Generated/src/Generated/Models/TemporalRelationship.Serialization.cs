
#nullable disable

using System;

namespace Qyl.Domains.Observe.Error
{
    internal static partial class TemporalRelationshipExtensions
    {
        public static string ToSerialString(this TemporalRelationship value) => value switch
        {
            TemporalRelationship.Concurrent => "concurrent",
            TemporalRelationship.Precedes => "precedes",
            TemporalRelationship.Follows => "follows",
            TemporalRelationship.Unrelated => "unrelated",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown TemporalRelationship value.")
        };

        public static TemporalRelationship ToTemporalRelationship(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "concurrent"))
            {
                return TemporalRelationship.Concurrent;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "precedes"))
            {
                return TemporalRelationship.Precedes;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "follows"))
            {
                return TemporalRelationship.Follows;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "unrelated"))
            {
                return TemporalRelationship.Unrelated;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown TemporalRelationship value.");
        }
    }
}
