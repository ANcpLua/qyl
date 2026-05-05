

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Cpu;

public static class CpuAttributes
{
    public const string LogicalNumber = "cpu.logical_number";

    public const string Mode = "cpu.mode";

    public static class ModeValues
    {
        public const string Idle = "idle";

        public const string Interrupt = "interrupt";

        public const string Iowait = "iowait";

        public const string Kernel = "kernel";

        public const string Nice = "nice";

        public const string Steal = "steal";

        public const string System = "system";

        public const string User = "user";
    }
}
