

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Service;

public static class ServiceAttributes
{
    public const string Criticality = "service.criticality";

    public static class CriticalityValues
    {
        public const string Critical = "critical";

        public const string High = "high";

        public const string Low = "low";

        public const string Medium = "medium";
    }

    public const string PeerName = "service.peer.name";

    public const string PeerNamespace = "service.peer.namespace";
}
