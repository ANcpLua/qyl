

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Db;

public static class DbAttributes
{
    [global::System.Obsolete("Replaced by cassandra.consistency.level.", false)]
    public const string CassandraConsistencyLevel = "db.cassandra.consistency_level";

    public static class CassandraConsistencyLevelValues
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

    [global::System.Obsolete("Replaced by cassandra.coordinator.dc.", false)]
    public const string CassandraCoordinatorDc = "db.cassandra.coordinator.dc";

    [global::System.Obsolete("Replaced by cassandra.coordinator.id.", false)]
    public const string CassandraCoordinatorId = "db.cassandra.coordinator.id";

    [global::System.Obsolete("Replaced by cassandra.query.idempotent.", false)]
    public const string CassandraIdempotence = "db.cassandra.idempotence";

    [global::System.Obsolete("Replaced by cassandra.page.size.", false)]
    public const string CassandraPageSize = "db.cassandra.page_size";

    [global::System.Obsolete("Replaced by cassandra.speculative_execution.count.", false)]
    public const string CassandraSpeculativeExecutionCount = "db.cassandra.speculative_execution_count";

    [global::System.Obsolete("Replaced by db.collection.name.", false)]
    public const string CassandraTable = "db.cassandra.table";

    public const string ClientConnectionPoolName = "db.client.connection.pool.name";

    public const string ClientConnectionState = "db.client.connection.state";

    public static class ClientConnectionStateValues
    {
        public const string Idle = "idle";

        public const string Used = "used";
    }

    [global::System.Obsolete("Replaced by db.client.connection.pool.name.", false)]
    public const string ClientConnectionsPoolName = "db.client.connections.pool.name";

    [global::System.Obsolete("Replaced by db.client.connection.state.", false)]
    public const string ClientConnectionsState = "db.client.connections.state";

    public static class ClientConnectionsStateValues
    {
        public const string Idle = "idle";

        public const string Used = "used";
    }

    [global::System.Obsolete("Replaced by `server.address` and `server.port`.", false)]
    public const string ConnectionString = "db.connection_string";

    [global::System.Obsolete("Replaced by azure.client.id.", false)]
    public const string CosmosdbClientId = "db.cosmosdb.client_id";

    [global::System.Obsolete("Replaced by azure.cosmosdb.connection.mode.", false)]
    public const string CosmosdbConnectionMode = "db.cosmosdb.connection_mode";

    public static class CosmosdbConnectionModeValues
    {
        public const string Direct = "direct";

        public const string Gateway = "gateway";
    }

    [global::System.Obsolete("Replaced by azure.cosmosdb.consistency.level.", false)]
    public const string CosmosdbConsistencyLevel = "db.cosmosdb.consistency_level";

    public static class CosmosdbConsistencyLevelValues
    {
        public const string BoundedStaleness = "BoundedStaleness";

        public const string ConsistentPrefix = "ConsistentPrefix";

        public const string Eventual = "Eventual";

        public const string Session = "Session";

        public const string Strong = "Strong";
    }

    [global::System.Obsolete("Replaced by db.collection.name.", false)]
    public const string CosmosdbContainer = "db.cosmosdb.container";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string CosmosdbOperationType = "db.cosmosdb.operation_type";

    public static class CosmosdbOperationTypeValues
    {
        public const string Batch = "batch";

        public const string Create = "create";

        public const string Delete = "delete";

        public const string Execute = "execute";

        public const string ExecuteJavascript = "execute_javascript";

        public const string Head = "head";

        public const string HeadFeed = "head_feed";

        public const string Invalid = "invalid";

        public const string Patch = "patch";

        public const string Query = "query";

        public const string QueryPlan = "query_plan";

        public const string Read = "read";

        public const string ReadFeed = "read_feed";

        public const string Replace = "replace";

        public const string Upsert = "upsert";
    }

    [global::System.Obsolete("Replaced by azure.cosmosdb.operation.contacted_regions.", false)]
    public const string CosmosdbRegionsContacted = "db.cosmosdb.regions_contacted";

    [global::System.Obsolete("Replaced by azure.cosmosdb.operation.request_charge.", false)]
    public const string CosmosdbRequestCharge = "db.cosmosdb.request_charge";

    [global::System.Obsolete("Replaced by azure.cosmosdb.request.body.size.", false)]
    public const string CosmosdbRequestContentLength = "db.cosmosdb.request_content_length";

    [global::System.Obsolete("Use `db.response.status_code` instead.", false)]
    public const string CosmosdbStatusCode = "db.cosmosdb.status_code";

    [global::System.Obsolete("Replaced by azure.cosmosdb.response.sub_status_code.", false)]
    public const string CosmosdbSubStatusCode = "db.cosmosdb.sub_status_code";

    [global::System.Obsolete("Replaced by db.namespace.", false)]
    public const string ElasticsearchClusterName = "db.elasticsearch.cluster.name";

    [global::System.Obsolete("Replaced by elasticsearch.node.name.", false)]
    public const string ElasticsearchNodeName = "db.elasticsearch.node.name";

    [global::System.Obsolete("Replaced by db.operation.parameter.", false)]
    public const string ElasticsearchPathParts = "db.elasticsearch.path_parts";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string InstanceId = "db.instance.id";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string JdbcDriverClassname = "db.jdbc.driver_classname";

    [global::System.Obsolete("Replaced by db.collection.name.", false)]
    public const string MongodbCollection = "db.mongodb.collection";

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string MssqlInstanceName = "db.mssql.instance_name";

    [global::System.Obsolete("Replaced by db.namespace.", false)]
    public const string Name = "db.name";

    [global::System.Obsolete("Replaced by db.operation.name.", false)]
    public const string Operation = "db.operation";

    public const string OperationParameter = "db.operation.parameter";

    public const string QueryParameter = "db.query.parameter";

    [global::System.Obsolete("Uncategorized.", false)]
    public const string RedisDatabaseIndex = "db.redis.database_index";

    public const string ResponseReturnedRows = "db.response.returned_rows";

    [global::System.Obsolete("Replaced by `db.collection.name`, but only if not extracting the value from `db.query.text`.", false)]
    public const string SqlTable = "db.sql.table";

    [global::System.Obsolete("Replaced by db.query.text.", false)]
    public const string Statement = "db.statement";

    [global::System.Obsolete("Replaced by db.system.name.", false)]
    public const string System = "db.system";

    public static class SystemValues
    {
        public const string Adabas = "adabas";

        [global::System.Obsolete("{\"note\": \"Replaced by `intersystems_cache`.\", \"reason\": \"renamed\", \"renamed_to\": \"intersystems_cache\"}", false)]
        public const string Cache = "cache";

        public const string Cassandra = "cassandra";

        public const string Clickhouse = "clickhouse";

        [global::System.Obsolete("{\"note\": \"Replaced by `other_sql`.\", \"reason\": \"renamed\", \"renamed_to\": \"other_sql\"}", false)]
        public const string Cloudscape = "cloudscape";

        public const string Cockroachdb = "cockroachdb";

        [global::System.Obsolete("{\"note\": \"Obsoleted.\", \"reason\": \"obsoleted\"}", false)]
        public const string Coldfusion = "coldfusion";

        public const string Cosmosdb = "cosmosdb";

        public const string Couchbase = "couchbase";

        public const string Couchdb = "couchdb";

        public const string Db2 = "db2";

        public const string Derby = "derby";

        public const string Dynamodb = "dynamodb";

        public const string Edb = "edb";

        public const string Elasticsearch = "elasticsearch";

        public const string Filemaker = "filemaker";

        public const string Firebird = "firebird";

        [global::System.Obsolete("{\"note\": \"Replaced by `other_sql`.\", \"reason\": \"renamed\", \"renamed_to\": \"other_sql\"}", false)]
        public const string Firstsql = "firstsql";

        public const string Geode = "geode";

        public const string H2 = "h2";

        public const string Hanadb = "hanadb";

        public const string Hbase = "hbase";

        public const string Hive = "hive";

        public const string Hsqldb = "hsqldb";

        public const string Influxdb = "influxdb";

        public const string Informix = "informix";

        public const string Ingres = "ingres";

        public const string Instantdb = "instantdb";

        public const string Interbase = "interbase";

        public const string IntersystemsCache = "intersystems_cache";

        public const string Mariadb = "mariadb";

        public const string Maxdb = "maxdb";

        public const string Memcached = "memcached";

        public const string Mongodb = "mongodb";

        public const string Mssql = "mssql";

        [global::System.Obsolete("{\"note\": \"Replaced by `other_sql`.\", \"reason\": \"renamed\", \"renamed_to\": \"other_sql\"}", false)]
        public const string Mssqlcompact = "mssqlcompact";

        public const string Mysql = "mysql";

        public const string Neo4j = "neo4j";

        public const string Netezza = "netezza";

        public const string Opensearch = "opensearch";

        public const string Oracle = "oracle";

        public const string OtherSql = "other_sql";

        public const string Pervasive = "pervasive";

        public const string Pointbase = "pointbase";

        public const string Postgresql = "postgresql";

        public const string Progress = "progress";

        public const string Redis = "redis";

        public const string Redshift = "redshift";

        public const string Spanner = "spanner";

        public const string Sqlite = "sqlite";

        public const string Sybase = "sybase";

        public const string Teradata = "teradata";

        public const string Trino = "trino";

        public const string Vertica = "vertica";
    }

    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string User = "db.user";
}
