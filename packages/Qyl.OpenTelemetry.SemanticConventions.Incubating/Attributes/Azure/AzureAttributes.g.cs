

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Azure;

public static class AzureAttributes
{
    public const string ClientId = "azure.client.id";

    public const string CosmosdbConnectionMode = "azure.cosmosdb.connection.mode";

    public static class CosmosdbConnectionModeValues
    {
        public const string Direct = "direct";

        public const string Gateway = "gateway";
    }

    public const string CosmosdbConsistencyLevel = "azure.cosmosdb.consistency.level";

    public static class CosmosdbConsistencyLevelValues
    {
        public const string BoundedStaleness = "BoundedStaleness";

        public const string ConsistentPrefix = "ConsistentPrefix";

        public const string Eventual = "Eventual";

        public const string Session = "Session";

        public const string Strong = "Strong";
    }

    public const string CosmosdbOperationContactedRegions = "azure.cosmosdb.operation.contacted_regions";

    public const string CosmosdbOperationRequestCharge = "azure.cosmosdb.operation.request_charge";

    public const string CosmosdbRequestBodySize = "azure.cosmosdb.request.body.size";

    public const string CosmosdbResponseSubStatusCode = "azure.cosmosdb.response.sub_status_code";

    public const string ResourceProviderNamespace = "azure.resource_provider.namespace";

    public const string ServiceRequestId = "azure.service.request.id";
}
