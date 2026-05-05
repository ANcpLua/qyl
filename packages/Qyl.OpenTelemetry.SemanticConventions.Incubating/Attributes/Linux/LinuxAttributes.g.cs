

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Linux;

public static class LinuxAttributes
{
    [global::System.Obsolete("Replaced by system.memory.linux.slab.state.", false)]
    public const string MemorySlabState = "linux.memory.slab.state";

    public static class MemorySlabStateValues
    {
        public const string Reclaimable = "reclaimable";

        public const string Unreclaimable = "unreclaimable";
    }
}
