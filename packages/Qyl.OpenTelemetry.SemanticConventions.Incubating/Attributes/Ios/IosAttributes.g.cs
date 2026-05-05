

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Ios;

public static class IosAttributes
{
    public const string AppState = "ios.app.state";

    public static class AppStateValues
    {
        public const string Active = "active";

        public const string Background = "background";

        public const string Foreground = "foreground";

        public const string Inactive = "inactive";

        public const string Terminate = "terminate";
    }

    [global::System.Obsolete("Replaced by ios.app.state.", false)]
    public const string State = "ios.state";

    public static class StateValues
    {
        public const string Active = "active";

        public const string Background = "background";

        public const string Foreground = "foreground";

        public const string Inactive = "inactive";

        public const string Terminate = "terminate";
    }
}
