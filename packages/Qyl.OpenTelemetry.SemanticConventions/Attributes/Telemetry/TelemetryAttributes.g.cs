

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Telemetry;

public static class TelemetryAttributes
{
    public const string DistroName = "telemetry.distro.name";

    public const string DistroVersion = "telemetry.distro.version";

    public const string SdkLanguage = "telemetry.sdk.language";

    public static class SdkLanguageValues
    {
        public const string Cpp = "cpp";

        public const string Dotnet = "dotnet";

        public const string Erlang = "erlang";

        public const string Go = "go";

        public const string Java = "java";

        public const string Nodejs = "nodejs";

        public const string Php = "php";

        public const string Python = "python";

        public const string Ruby = "ruby";

        public const string Rust = "rust";

        public const string Swift = "swift";

        public const string Webjs = "webjs";
    }

    public const string SdkName = "telemetry.sdk.name";

    public const string SdkVersion = "telemetry.sdk.version";
}
