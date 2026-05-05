

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Graphql;

public static class GraphqlAttributes
{
    public const string Document = "graphql.document";

    public const string OperationName = "graphql.operation.name";

    public const string OperationType = "graphql.operation.type";

    public static class OperationTypeValues
    {
        public const string Mutation = "mutation";

        public const string Query = "query";

        public const string Subscription = "subscription";
    }
}
