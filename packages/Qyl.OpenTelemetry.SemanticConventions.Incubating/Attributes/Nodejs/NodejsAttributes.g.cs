

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Nodejs;

public static class NodejsAttributes
{
    public const string EventloopState = "nodejs.eventloop.state";

    public static class EventloopStateValues
    {
        public const string Active = "active";

        public const string Idle = "idle";
    }
}
