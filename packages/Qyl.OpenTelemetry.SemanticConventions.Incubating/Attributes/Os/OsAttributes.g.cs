

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Os;

public static class OsAttributes
{
    public const string BuildId = "os.build_id";

    public const string Description = "os.description";

    public const string Name = "os.name";

    public const string Type = "os.type";

    public static class TypeValues
    {
        public const string Aix = "aix";

        public const string Darwin = "darwin";

        public const string Dragonflybsd = "dragonflybsd";

        public const string Freebsd = "freebsd";

        public const string Hpux = "hpux";

        public const string Linux = "linux";

        public const string Netbsd = "netbsd";

        public const string Openbsd = "openbsd";

        public const string Solaris = "solaris";

        public const string Windows = "windows";

        [global::System.Obsolete("{\"note\": \"Replaced by `zos`.\", \"reason\": \"renamed\", \"renamed_to\": \"zos\"}", false)]
        public const string ZOs = "z_os";

        public const string Zos = "zos";
    }

    public const string Version = "os.version";
}
