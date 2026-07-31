using DuckDB.NET.Data;
using DuckDB.NET.Native;

namespace Qyl.Collector.Storage;

internal enum DuckDbFailureKind
{
    RetryableTransient,
    Concurrency,
    Cancellation,
    Corruption,
    InvalidData,
    SchemaIncompatibility,
    ResourceExhaustion,
    ProgrammerError
}

// Closed classification for the native 1.5.5 error surface. New upstream enum
// values intentionally throw until their retry semantics are reviewed.
internal static class DuckDbFailures
{
    public static DuckDbFailureKind Classify(Exception error) => error switch
    {
        OperationCanceledException => DuckDbFailureKind.Cancellation,
        DuckDBException duckDb => Classify(duckDb.ErrorType),
        QylStoreUnavailableException => DuckDbFailureKind.ResourceExhaustion,
        WorkflowCheckpointIncompatibleException => DuckDbFailureKind.SchemaIncompatibility,
        InvalidDataException or JsonException => DuckDbFailureKind.Corruption,
        IOException => DuckDbFailureKind.RetryableTransient,
        _ => DuckDbFailureKind.ProgrammerError
    };

    public static DuckDbFailureKind Classify(DuckDBErrorType errorType) => errorType switch
    {
        DuckDBErrorType.Connection or
        DuckDBErrorType.Io or
        DuckDBErrorType.Network or
        DuckDBErrorType.Http => DuckDbFailureKind.RetryableTransient,

        DuckDBErrorType.Transaction or
        DuckDBErrorType.Serialization => DuckDbFailureKind.Concurrency,

        DuckDBErrorType.Interrupt => DuckDbFailureKind.Cancellation,

        DuckDBErrorType.Fatal or
        DuckDBErrorType.Internal or
        DuckDBErrorType.NullPointer or
        DuckDBErrorType.Index or
        DuckDBErrorType.Stat => DuckDbFailureKind.Corruption,

        DuckDBErrorType.OutOfRange or
        DuckDBErrorType.Conversion or
        DuckDBErrorType.UnknownType or
        DuckDBErrorType.Decimal or
        DuckDBErrorType.MismatchType or
        DuckDBErrorType.DivideByZero or
        DuckDBErrorType.InvalidType or
        DuckDBErrorType.Expression or
        DuckDBErrorType.Constraint or
        DuckDBErrorType.InvalidInput or
        DuckDBErrorType.Sequence => DuckDbFailureKind.InvalidData,

        DuckDBErrorType.Catalog or
        DuckDBErrorType.Parser or
        DuckDBErrorType.Planner or
        DuckDBErrorType.Syntax or
        DuckDBErrorType.Settings or
        DuckDBErrorType.Binder or
        DuckDBErrorType.Permission or
        DuckDBErrorType.ParameterNotResolved or
        DuckDBErrorType.ParameterNotAllowed or
        DuckDBErrorType.Dependency or
        DuckDBErrorType.MissingExtension or
        DuckDBErrorType.Autoload => DuckDbFailureKind.SchemaIncompatibility,

        DuckDBErrorType.ObjectSize or
        DuckDBErrorType.OutOfMemory => DuckDbFailureKind.ResourceExhaustion,

        DuckDBErrorType.Invalid or
        DuckDBErrorType.NotImplemented or
        DuckDBErrorType.Scheduler or
        DuckDBErrorType.Executor or
        DuckDBErrorType.Optimizer => DuckDbFailureKind.ProgrammerError,

        _ => throw new ArgumentOutOfRangeException(
            nameof(errorType),
            errorType,
            "DuckDB introduced an unclassified native error type.")
    };

    public static bool IsRetryable(Exception error) => IsRetryable(Classify(error));

    public static bool IsRetryable(DuckDbFailureKind classification) => classification is
        DuckDbFailureKind.RetryableTransient or
        DuckDbFailureKind.Concurrency or
        DuckDbFailureKind.ResourceExhaustion;
}
