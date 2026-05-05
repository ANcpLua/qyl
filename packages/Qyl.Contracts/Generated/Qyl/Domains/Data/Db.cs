#nullable enable

namespace Qyl.Domains.Data.Db;

public sealed class DbAttributes
{
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public string? Namespace { get; init; }
    public Qyl.Domains.Data.Db.DbOperationName? OperationName { get; init; }
    public int? BatchSize { get; init; }
    public string? QueryText { get; init; }
    public string? QuerySummary { get; init; }
    public IReadOnlyDictionary<string, string>? QueryParameters { get; init; }
    public string? CollectionName { get; init; }
    public long? ReturnedRows { get; init; }
    public string? ResponseStatusCode { get; init; }
    public string? ServerAddress { get; init; }
    public int? ServerPort { get; init; }
    public string? ConnectionPoolName { get; init; }
    public Qyl.Domains.Data.Db.DbConnectionState? ConnectionState { get; init; }
    public string? DbName { get; init; }
    public string? Statement { get; init; }
    public string? Operation { get; init; }
    public string? User { get; init; }
}

public sealed class DbClientOperationDurationMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public string? Namespace { get; init; }
    public required Qyl.Domains.Data.Db.DbOperationName OperationName { get; init; }
    public string? CollectionName { get; init; }
    public string? ErrorType { get; init; }
}

public sealed class DbConnectionPoolSizeMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public required string PoolName { get; init; }
    public required Qyl.Domains.Data.Db.DbConnectionState State { get; init; }
}

public sealed class DbConnectionCreateTimeMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public required string PoolName { get; init; }
}

public sealed class DbConnectionWaitTimeMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public required string PoolName { get; init; }
}

public sealed class DbConnectionUseTimeMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public required string PoolName { get; init; }
}

public sealed class DbConnectionPendingRequestsMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public required string PoolName { get; init; }
}

public sealed class DbConnectionTimeoutsMetric
{
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem SystemName { get; init; }
    public required string PoolName { get; init; }
}

public sealed class DbStats
{
    public required long TotalQueries { get; init; }
    public required double TotalDurationS { get; init; }
    public required double AvgDurationMs { get; init; }
    public required double ErrorRate { get; init; }
    public IReadOnlyList<Qyl.Domains.Data.Db.DbSystemStats>? BySystem { get; init; }
    public IReadOnlyList<Qyl.Domains.Data.Db.DbOperationStats>? ByOperation { get; init; }
    public IReadOnlyList<Qyl.Domains.Data.Db.DbSlowQuery>? SlowestQueries { get; init; }
}

public sealed class DbSystemStats
{
    public required Qyl.Domains.Data.Db.DbSystem System { get; init; }
    public required long QueryCount { get; init; }
    public required double AvgDurationMs { get; init; }
    public required double ErrorRate { get; init; }
}

public sealed class DbOperationStats
{
    public required Qyl.Domains.Data.Db.DbOperationName Operation { get; init; }
    public required long QueryCount { get; init; }
    public required double AvgDurationMs { get; init; }
}

public sealed class DbSlowQuery
{
    public required string QueryText { get; init; }
    public required double DurationMs { get; init; }
    public required Qyl.Domains.Data.Db.DbSystem System { get; init; }
    public string? Namespace { get; init; }
    public required Qyl.Domains.Data.Db.DbOperationName Operation { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public enum DbSystem
{
    Postgresql,
    Mysql,
    Mariadb,
    Mssql,
    Oracle,
    Db2,
    Sqlite,
    Hana,
    Maxdb,
    Sqlanywhere,
    Teradata,
    Sybase,
    Informix,
    Firebird,
    Interbase,
    H2,
    Hsqldb,
    Derby,
    Cockroachdb,
    Tidb,
    Yugabyte,
    Vitess,
    Clickhouse,
    Snowflake,
    Redshift,
    Bigquery,
    Synapse,
    Databricks,
    Presto,
    Trino,
    Hive,
    Sparksql,
    Impala,
    Drill,
    Duckdb,
    Vertica,
    Greenplum,
    Netezza,
    Mongodb,
    Couchbase,
    Couchdb,
    Documentdb,
    Cosmosdb,
    Firestore,
    Ravendb,
    Arangodb,
    Redis,
    Valkey,
    Keydb,
    Memcached,
    Dynamodb,
    Azuretable,
    Hazelcast,
    Etcd,
    Ignite,
    Rocksdb,
    Leveldb,
    Berkeleydb,
    Cassandra,
    Scylla,
    Hbase,
    Bigtable,
    Neo4j,
    Neptune,
    ArangodbGraph,
    Janusgraph,
    Tigergraph,
    Dgraph,
    Influxdb,
    Timescaledb,
    Prometheus,
    Questdb,
    Timestream,
    Druid,
    Victoriametrics,
    Elasticsearch,
    Opensearch,
    Solr,
    Splunk,
    Meilisearch,
    Typesense,
    Algolia,
    Pinecone,
    Milvus,
    Weaviate,
    Qdrant,
    Chroma,
    Pgvector,
    Msaccess,
    Filemaker,
    Foxpro,
    Dbase,
    Odbc,
    Jdbc,
    Other
}

public enum DbOperationName
{
    Select,
    Insert,
    Update,
    Delete,
    Create,
    Drop,
    Alter,
    Truncate,
    Execute,
    Call,
    Begin,
    Commit,
    Rollback,
    Savepoint,
    Find,
    FindOne,
    InsertOne,
    InsertMany,
    UpdateOne,
    UpdateMany,
    DeleteOne,
    DeleteMany,
    Aggregate,
    Count,
    Get,
    Set,
    Del,
    Ping
}

public enum DbConnectionState
{
    Idle,
    Used
}

public enum DbVersions
{
    V1_20,
    V1_24,
    V1_38,
    V1_39
}
