

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Db;

public static class DbAttributes
{
    public const string CollectionName = "db.collection.name";

    public const string Namespace = "db.namespace";

    public const string OperationBatchSize = "db.operation.batch.size";

    public const string OperationName = "db.operation.name";

    public const string QuerySummary = "db.query.summary";

    public const string QueryText = "db.query.text";

    public const string ResponseStatusCode = "db.response.status_code";

    public const string StoredProcedureName = "db.stored_procedure.name";

    public const string SystemName = "db.system.name";

    public static class SystemNameValues
    {
        public const string ActianIngres = "actian.ingres";

        public const string AwsDynamodb = "aws.dynamodb";

        public const string AwsRedshift = "aws.redshift";

        public const string AzureCosmosdb = "azure.cosmosdb";

        public const string Cassandra = "cassandra";

        public const string Clickhouse = "clickhouse";

        public const string Cockroachdb = "cockroachdb";

        public const string Couchbase = "couchbase";

        public const string Couchdb = "couchdb";

        public const string Derby = "derby";

        public const string Elasticsearch = "elasticsearch";

        public const string Firebirdsql = "firebirdsql";

        public const string GcpSpanner = "gcp.spanner";

        public const string Geode = "geode";

        public const string H2database = "h2database";

        public const string Hbase = "hbase";

        public const string Hive = "hive";

        public const string Hsqldb = "hsqldb";

        public const string IbmDb2 = "ibm.db2";

        public const string IbmInformix = "ibm.informix";

        public const string IbmNetezza = "ibm.netezza";

        public const string Influxdb = "influxdb";

        public const string Instantdb = "instantdb";

        public const string IntersystemsCache = "intersystems.cache";

        public const string Mariadb = "mariadb";

        public const string Memcached = "memcached";

        public const string MicrosoftSqlServer = "microsoft.sql_server";

        public const string Mongodb = "mongodb";

        public const string Mysql = "mysql";

        public const string Neo4j = "neo4j";

        public const string Opensearch = "opensearch";

        public const string OracleDb = "oracle.db";

        public const string OtherSql = "other_sql";

        public const string Postgresql = "postgresql";

        public const string Redis = "redis";

        public const string SapHana = "sap.hana";

        public const string SapMaxdb = "sap.maxdb";

        public const string SoftwareagAdabas = "softwareag.adabas";

        public const string Sqlite = "sqlite";

        public const string Teradata = "teradata";

        public const string Trino = "trino";
    }
}
