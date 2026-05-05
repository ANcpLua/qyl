

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Go;

public static class GoAttributes
{
    public const string CpuDetailedState = "go.cpu.detailed_state";

    public const string CpuState = "go.cpu.state";

    public static class CpuStateValues
    {
        public const string Gc = "gc";

        public const string Idle = "idle";

        public const string Scavenge = "scavenge";

        public const string User = "user";
    }

    public const string MemoryDetailedType = "go.memory.detailed_type";

    public const string MemoryType = "go.memory.type";

    public static class MemoryTypeValues
    {
        public const string Other = "other";

        public const string Stack = "stack";
    }
}
