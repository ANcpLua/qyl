

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Profile;

public static class ProfileAttributes
{
    public const string FrameType = "profile.frame.type";

    public static class FrameTypeValues
    {
        public const string Beam = "beam";

        public const string Cpython = "cpython";

        public const string Dotnet = "dotnet";

        public const string Go = "go";

        public const string Jvm = "jvm";

        public const string Kernel = "kernel";

        public const string Native = "native";

        public const string Perl = "perl";

        public const string Php = "php";

        public const string Ruby = "ruby";

        public const string Rust = "rust";

        public const string V8js = "v8js";
    }
}
