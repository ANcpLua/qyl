

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Event;

public static class EventAttributes
{
    [global::System.Obsolete("The value of this attribute MUST now be set as the value of the EventName field on the LogRecord to indicate that the LogRecord represents an Event.", false)]
    public const string Name = "event.name";
}
