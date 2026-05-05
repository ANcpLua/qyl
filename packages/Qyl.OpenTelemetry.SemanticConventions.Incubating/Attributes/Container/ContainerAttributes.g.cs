

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Container;

public static class ContainerAttributes
{
    public const string Command = "container.command";

    public const string CommandArgs = "container.command_args";

    public const string CommandLine = "container.command_line";

    [global::System.Obsolete("Replaced by cpu.mode.", false)]
    public const string CpuState = "container.cpu.state";

    public static class CpuStateValues
    {
        public const string Kernel = "kernel";

        public const string System = "system";

        public const string User = "user";
    }

    public const string CsiPluginName = "container.csi.plugin.name";

    public const string CsiVolumeId = "container.csi.volume.id";

    public const string Id = "container.id";

    public const string ImageId = "container.image.id";

    public const string ImageName = "container.image.name";

    public const string ImageRepoDigests = "container.image.repo_digests";

    public const string ImageTags = "container.image.tags";

    public const string Label = "container.label";

    [global::System.Obsolete("Replaced by container.label.", false)]
    public const string Labels = "container.labels";

    public const string Name = "container.name";

    [global::System.Obsolete("Replaced by container.runtime.name.", false)]
    public const string Runtime = "container.runtime";

    public const string RuntimeDescription = "container.runtime.description";

    public const string RuntimeName = "container.runtime.name";

    public const string RuntimeVersion = "container.runtime.version";
}
