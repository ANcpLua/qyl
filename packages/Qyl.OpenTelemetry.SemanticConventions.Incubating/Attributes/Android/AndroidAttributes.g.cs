

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Android;

public static class AndroidAttributes
{
    public const string AppState = "android.app.state";

    public static class AppStateValues
    {
        public const string Background = "background";

        public const string Created = "created";

        public const string Foreground = "foreground";
    }

    public const string OsApiLevel = "android.os.api_level";

    [global::System.Obsolete("Replaced by android.app.state.", false)]
    public const string State = "android.state";

    public static class StateValues
    {
        public const string Background = "background";

        public const string Created = "created";

        public const string Foreground = "foreground";
    }
}
