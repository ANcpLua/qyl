// =============================================================================
// qyl.protocol - Database Semantic Convention Attributes
// OTel 1.39+ db.* attribute constants
// 100% OTel semconv adherent - no custom extensions
// Owner: qyl.protocol | Consumers: servicedefaults, collector
// =============================================================================

namespace qyl.protocol.Attributes;

/// <summary>
///     OTel 1.39+ Database semantic convention attribute keys.
///     Status: Stable
///     https://opentelemetry.io/docs/specs/semconv/database/
/// </summary>
public static class DbAttributes
{
    // ═══════════════════════════════════════════════════════════════════════
    // Core database attributes (Required/Recommended)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>db.system.name - The database management system (DBMS) product as identified by the client instrumentation.</summary>
    public const string SystemName = "db.system.name";

    /// <summary>db.operation.name - The name of the operation or command being executed.</summary>
    public const string OperationName = "db.operation.name";

    /// <summary>db.query.text - The database query being executed.</summary>
    public const string QueryText = "db.query.text";

    /// <summary>db.query.summary - A parameterized query string used for grouping similar queries.</summary>
    public const string QuerySummary = "db.query.summary";

    /// <summary>db.namespace - The name of the database, fully qualified within the server address and port.</summary>
    public const string Namespace = "db.namespace";

    /// <summary>db.collection.name - The name of a collection (table, container) within the database.</summary>
    public const string CollectionName = "db.collection.name";

    // ═══════════════════════════════════════════════════════════════════════
    // Response attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>db.response.status_code - Database response status code.</summary>
    public const string ResponseStatusCode = "db.response.status_code";

    /// <summary>db.response.returned_rows - Number of rows returned by the operation.</summary>
    public const string ResponseReturnedRows = "db.response.returned_rows";

    // ═══════════════════════════════════════════════════════════════════════
    // Connection attributes
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>db.client.connection.pool.name - The name of the connection pool.</summary>
    public const string ConnectionPoolName = "db.client.connection.pool.name";

    /// <summary>db.client.connection.state - The state of a connection in the pool.</summary>
    public const string ConnectionState = "db.client.connection.state";

    // ═══════════════════════════════════════════════════════════════════════
    // Batch operations
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>db.operation.batch.size - The number of queries included in a batch operation.</summary>
    public const string OperationBatchSize = "db.operation.batch.size";

    // ═══════════════════════════════════════════════════════════════════════
    // Stored procedures
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>db.stored_procedure.name - The name of a stored procedure being called.</summary>
    public const string StoredProcedureName = "db.stored_procedure.name";

    // ═══════════════════════════════════════════════════════════════════════
    // Well-known db.system.name values
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Well-known database system name values.</summary>
    public static class Systems
    {
        /// <summary>duckdb - DuckDB.</summary>
        public const string DuckDb = "duckdb";

        /// <summary>postgresql - PostgreSQL.</summary>
        public const string PostgreSql = "postgresql";

        /// <summary>mssql - Microsoft SQL Server.</summary>
        public const string MsSql = "mssql";

        /// <summary>sqlite - SQLite.</summary>
        public const string Sqlite = "sqlite";

        /// <summary>mysql - MySQL.</summary>
        public const string MySql = "mysql";

        /// <summary>mariadb - MariaDB.</summary>
        public const string MariaDb = "mariadb";

        /// <summary>oracle - Oracle Database.</summary>
        public const string Oracle = "oracle";

        /// <summary>firebird - Firebird.</summary>
        public const string Firebird = "firebird";

        /// <summary>redis - Redis.</summary>
        public const string Redis = "redis";

        /// <summary>mongodb - MongoDB.</summary>
        public const string MongoDb = "mongodb";

        /// <summary>elasticsearch - Elasticsearch.</summary>
        public const string Elasticsearch = "elasticsearch";

        /// <summary>cosmosdb - Azure Cosmos DB.</summary>
        public const string CosmosDb = "cosmosdb";

        /// <summary>cassandra - Apache Cassandra.</summary>
        public const string Cassandra = "cassandra";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Common operation names
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Well-known database operation name values.</summary>
    public static class Operations
    {
        /// <summary>SELECT - Query operation.</summary>
        public const string Select = "SELECT";

        /// <summary>INSERT - Insert operation.</summary>
        public const string Insert = "INSERT";

        /// <summary>UPDATE - Update operation.</summary>
        public const string Update = "UPDATE";

        /// <summary>DELETE - Delete operation.</summary>
        public const string Delete = "DELETE";

        /// <summary>CREATE - DDL create operation.</summary>
        public const string Create = "CREATE";

        /// <summary>DROP - DDL drop operation.</summary>
        public const string Drop = "DROP";

        /// <summary>ExecuteReader - ADO.NET ExecuteReader.</summary>
        public const string ExecuteReader = "ExecuteReader";

        /// <summary>ExecuteNonQuery - ADO.NET ExecuteNonQuery.</summary>
        public const string ExecuteNonQuery = "ExecuteNonQuery";

        /// <summary>ExecuteScalar - ADO.NET ExecuteScalar.</summary>
        public const string ExecuteScalar = "ExecuteScalar";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Deprecated attributes (for migration)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Deprecated attribute names for backward compatibility.</summary>
    public static class Deprecated
    {
        /// <summary>db.system - Deprecated, use db.system.name.</summary>
        public const string System = "db.system";

        /// <summary>db.name - Deprecated, use db.namespace.</summary>
        public const string Name = "db.name";

        /// <summary>db.statement - Deprecated, use db.query.text.</summary>
        public const string Statement = "db.statement";

        /// <summary>db.operation - Deprecated, use db.operation.name.</summary>
        public const string Operation = "db.operation";

        /// <summary>db.user - Deprecated, use enduser.id or db.client.user.</summary>
        public const string User = "db.user";
    }
}
