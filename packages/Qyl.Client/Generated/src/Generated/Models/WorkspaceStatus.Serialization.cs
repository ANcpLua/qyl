
#nullable disable

using System;

namespace Qyl.Domains.Workspace
{
    internal static partial class WorkspaceStatusExtensions
    {
        public static string ToSerialString(this WorkspaceStatus value) => value switch
        {
            WorkspaceStatus.Active => "active",
            WorkspaceStatus.Suspended => "suspended",
            WorkspaceStatus.Archived => "archived",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown WorkspaceStatus value.")
        };

        public static WorkspaceStatus ToWorkspaceStatus(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "active"))
            {
                return WorkspaceStatus.Active;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "suspended"))
            {
                return WorkspaceStatus.Suspended;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "archived"))
            {
                return WorkspaceStatus.Archived;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown WorkspaceStatus value.");
        }
    }
}
