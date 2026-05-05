

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Host;

public static class HostAttributes
{
    public const string Arch = "host.arch";

    public static class ArchValues
    {
        public const string Amd64 = "amd64";

        public const string Arm32 = "arm32";

        public const string Arm64 = "arm64";

        public const string Ia64 = "ia64";

        public const string Ppc32 = "ppc32";

        public const string Ppc64 = "ppc64";

        public const string S390x = "s390x";

        public const string X86 = "x86";
    }

    public const string CpuCacheL2Size = "host.cpu.cache.l2.size";

    public const string CpuFamily = "host.cpu.family";

    public const string CpuModelId = "host.cpu.model.id";

    public const string CpuModelName = "host.cpu.model.name";

    public const string CpuStepping = "host.cpu.stepping";

    public const string CpuVendorId = "host.cpu.vendor.id";

    public const string Id = "host.id";

    public const string ImageId = "host.image.id";

    public const string ImageName = "host.image.name";

    public const string ImageVersion = "host.image.version";

    public const string Ip = "host.ip";

    public const string Mac = "host.mac";

    public const string Name = "host.name";

    public const string Type = "host.type";
}
