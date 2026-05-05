

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Cassandra;

public static class CassandraAttributes
{
    public const string ConsistencyLevel = "cassandra.consistency.level";

    public static class ConsistencyLevelValues
    {
        public const string All = "all";

        public const string Any = "any";

        public const string EachQuorum = "each_quorum";

        public const string LocalOne = "local_one";

        public const string LocalQuorum = "local_quorum";

        public const string LocalSerial = "local_serial";

        public const string One = "one";

        public const string Quorum = "quorum";

        public const string Serial = "serial";

        public const string Three = "three";

        public const string Two = "two";
    }

    public const string CoordinatorDc = "cassandra.coordinator.dc";

    public const string CoordinatorId = "cassandra.coordinator.id";

    public const string PageSize = "cassandra.page.size";

    public const string QueryIdempotent = "cassandra.query.idempotent";

    public const string SpeculativeExecutionCount = "cassandra.speculative_execution.count";
}
