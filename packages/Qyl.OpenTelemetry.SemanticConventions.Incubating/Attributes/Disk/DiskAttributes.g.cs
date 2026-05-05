

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Disk;

public static class DiskAttributes
{
    public const string IoDirection = "disk.io.direction";

    public static class IoDirectionValues
    {
        public const string Read = "read";

        public const string Write = "write";
    }
}
