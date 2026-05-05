

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Az;

public static class AzAttributes
{
    [global::System.Obsolete("Replaced by azure.resource_provider.namespace.", false)]
    public const string Namespace = "az.namespace";

    [global::System.Obsolete("Replaced by azure.service.request.id.", false)]
    public const string ServiceRequestId = "az.service_request_id";
}
