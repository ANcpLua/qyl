using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    private static void MigrateUnknownLogTimes(DuckDBConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        if (!TableExists(connection, transaction, "logs")) return;
        var columns = ReadColumns(connection, transaction, "logs");
        var time = columns.SingleOrDefault(static column => column.Name == "time_unix_nano");
        // Only this known lossless schema relaxation precedes the normal schema check.
        // Never coerce an incompatible historical column into the current model.
        if (time is not { } column || NormalizeDuckDbType(column.Type) != "UBIGINT") return;
        if (!column.Nullable)
        {
            // DuckDB 1.5.6 can abort in SerializeIndex/WriteToWAL when dropped indexes are
            // recreated in the same transaction as a table alteration. Commit this atomic
            // data/nullable migration first; InitializeSchema restores declared nonunique
            // indexes in its next transaction, including recovery after an interrupted open.
            var declared = DuckDbGeneratedSchema.AuthoritativeTables.Single(static table => table.Name == "logs");
            var names = new List<string>();
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT index_name, is_unique FROM duckdb_indexes() WHERE schema_name = 'main' AND table_name = 'logs' ORDER BY index_name";
                using var reader = read.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    if (reader.GetBoolean(1) || !declared.Indexes.Any(index => index.Name == name && !index.Unique))
                        throw new QylSchemaMismatchException("The log time migration cannot alter an unknown or unique index: " + name);
                    names.Add(name);
                }
            }
            foreach (var name in names) ExecuteSchemaSql(connection, transaction, "DROP INDEX " + QuoteIdentifier(name));
            ExecuteSchemaSql(connection, transaction, "ALTER TABLE logs ALTER COLUMN time_unix_nano DROP NOT NULL");
        }
        // OTLP explicitly defines zero as unknown for both log timestamps.
        // https://github.com/open-telemetry/opentelemetry-proto/blob/v1.11.1/opentelemetry/proto/logs/v1/logs.proto
        ExecuteSchemaSql(connection, transaction, "UPDATE logs SET time_unix_nano = NULL WHERE time_unix_nano = 0");
        if (columns.Any(static item => item.Name == "observed_time_unix_nano"))
            ExecuteSchemaSql(connection, transaction, "UPDATE logs SET observed_time_unix_nano = NULL WHERE observed_time_unix_nano = 0");
        transaction.Commit();
    }
}
