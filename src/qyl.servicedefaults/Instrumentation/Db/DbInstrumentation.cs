using System.Collections.Concurrent;
using System.Data.Common;
using ANcpLua.Roslyn.Utilities;
using qyl.protocol.Attributes;

namespace Qyl.ServiceDefaults.Instrumentation.Db;

/// <summary>
///     Instrumentation helpers for ADO.NET database calls.
/// </summary>
/// <remarks>
///     <para>
///         Called by generated interceptors to wrap DbCommand methods with OpenTelemetry spans.
///     </para>
///     <para>
///         Note: Activity covers command execution, not reader iteration.
///         This matches OTel semantic conventions for db.query spans.
///     </para>
/// </remarks>
public static class DbInstrumentation
{
    private static readonly ConcurrentDictionary<Type, string> SDbSystemCache = new();

    /// <summary>
    ///     Executes <see cref="DbCommand.ExecuteReaderAsync(CancellationToken)" /> with instrumentation.
    /// </summary>
    public static async Task<DbDataReader> ExecuteReaderAsync(
        DbCommand command,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartDbActivity(command, "ExecuteReader");

        try
        {
            return await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    /// <summary>
    ///     Executes <see cref="DbCommand.ExecuteReader()" /> with instrumentation.
    /// </summary>
    public static DbDataReader ExecuteReader(DbCommand command)
    {
        using var activity = StartDbActivity(command, "ExecuteReader");

        try
        {
            return command.ExecuteReader();
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    /// <summary>
    ///     Executes <see cref="DbCommand.ExecuteNonQueryAsync(CancellationToken)" /> with instrumentation.
    /// </summary>
    public static async Task<int> ExecuteNonQueryAsync(
        DbCommand command,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartDbActivity(command, "ExecuteNonQuery");

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    /// <summary>
    ///     Executes <see cref="DbCommand.ExecuteNonQuery()" /> with instrumentation.
    /// </summary>
    public static int ExecuteNonQuery(DbCommand command)
    {
        using var activity = StartDbActivity(command, "ExecuteNonQuery");

        try
        {
            return command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    /// <summary>
    ///     Executes <see cref="DbCommand.ExecuteScalarAsync(CancellationToken)" /> with instrumentation.
    /// </summary>
    public static async Task<object?> ExecuteScalarAsync(
        DbCommand command,
        CancellationToken cancellationToken = default)
    {
        using var activity = StartDbActivity(command, "ExecuteScalar");

        try
        {
            return await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    /// <summary>
    ///     Executes <see cref="DbCommand.ExecuteScalar()" /> with instrumentation.
    /// </summary>
    public static object? ExecuteScalar(DbCommand command)
    {
        using var activity = StartDbActivity(command, "ExecuteScalar");

        try
        {
            return command.ExecuteScalar();
        }
        catch (Exception ex)
        {
            SetErrorStatus(activity, ex);
            throw;
        }
    }

    private static Activity? StartDbActivity(DbCommand command, string fallbackOperationName)
    {
        // Parse SQL to extract operation type per OTel semconv, fallback to ADO.NET method name
        var operationName = SqlOperationParser.TryParse(command.CommandText) ?? fallbackOperationName;
        var collectionName = SqlOperationParser.TryParseCollectionName(command.CommandText);

        // OTel semconv: span name = "{db.operation.name} {db.collection.name}" or just "{db.operation.name}"
        var spanName = collectionName is not null
            ? $"{operationName} {collectionName}"
            : operationName;

        var activity = ActivitySources.DbSource.StartActivity(spanName, ActivityKind.Client, default(ActivityContext));

        if (activity is null)
            return null;

        var dbSystem = GetDbSystem(command.Connection);

        activity.SetTag(DbAttributes.SystemName, dbSystem);
        activity.SetTag(DbAttributes.OperationName, operationName);

        if (collectionName is not null)
            activity.SetTag(DbAttributes.CollectionName, collectionName);

        if (command.CommandText is { Length: > 0 } sql)
            activity.SetTag(DbAttributes.QueryText, sql);

        if (command.Connection?.Database is { Length: > 0 } dbName)
            activity.SetTag(DbAttributes.Namespace, dbName);

        return activity;
    }

    private static void SetErrorStatus(Activity? activity, Exception ex)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddException(ex);
    }

    /// <summary>
    ///     Maps a DbConnection type to its OTel db.system.name value.
    /// </summary>
    private static string GetDbSystem(DbConnection? connection)
    {
        if (connection is null)
            return "unknown";

        return SDbSystemCache.GetOrAdd(connection.GetType(), static type =>
            MapTypeNameToDbSystem(type.FullName ?? type.Name));
    }

    /// <summary>
    ///     Gets the database system name for a type name. Exposed for testing.
    /// </summary>
    internal static string GetDbSystemForTesting(string typeName) =>
        MapTypeNameToDbSystem(typeName);

    /// <summary>
    ///     Maps a type name to the OTel db.system.name semantic convention value.
    /// </summary>
    private static string MapTypeNameToDbSystem(string typeName) =>
        typeName switch
        {
            _ when typeName.ContainsIgnoreCase("DuckDB") => DbAttributes.Systems.DuckDb,
            _ when typeName.ContainsIgnoreCase("Npgsql") => DbAttributes.Systems.PostgreSql,
            _ when typeName.ContainsIgnoreCase("SqlClient") => DbAttributes.Systems.MsSql,
            _ when typeName.ContainsIgnoreCase("Sqlite") => DbAttributes.Systems.Sqlite,
            _ when typeName.ContainsIgnoreCase("Oracle") => DbAttributes.Systems.Oracle,
            _ when typeName.ContainsIgnoreCase("MySql") => DbAttributes.Systems.MySql,
            _ when typeName.ContainsIgnoreCase("Firebird") => DbAttributes.Systems.Firebird,
            _ => "unknown"
        };
}
