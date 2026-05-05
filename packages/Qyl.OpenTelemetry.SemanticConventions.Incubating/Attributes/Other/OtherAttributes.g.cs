

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Other;

public static class OtherAttributes
{
    [global::System.Obsolete("Replaced by db.client.connection.state.", false)]
    public const string State = "state";

    public static class StateValues
    {
        public const string Idle = "idle";

        public const string Used = "used";
    }
}
