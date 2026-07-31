using DuckDB.NET.Data;
using DuckDB.NET.Native;

namespace Qyl.Collector.Storage;

// Retry classification for storage failures.
//
// Only categories that can plausibly succeed on an identical later attempt are
// transient. Everything else — constraint violations, catalog/parser/binder
// errors, conversion and invalid-input errors, internal errors — is a defect in
// the schema, the query, or the data, and retrying it hides an invariant
// violation behind an endless loop. The whitelist is deliberate: an unknown or
// newly added DuckDB error category fails fast rather than becoming retryable
// by default.
internal static class DuckDbTransientErrors
{
    public static bool IsTransient(Exception error) => error switch
    {
        DuckDBException duckDb => IsTransient(duckDb.ErrorType),
        QylStoreUnavailableException => true,
        IOException => true,
        _ => false
    };

    private static bool IsTransient(DuckDBErrorType errorType) => errorType is
        DuckDBErrorType.Transaction or
        DuckDBErrorType.Serialization or
        DuckDBErrorType.Connection or
        DuckDBErrorType.Io or
        DuckDBErrorType.Network or
        DuckDBErrorType.Http or
        DuckDBErrorType.OutOfMemory;
}
