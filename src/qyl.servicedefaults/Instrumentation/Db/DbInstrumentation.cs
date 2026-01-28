using System.Collections.Concurrent;
using System.Data.Common;

namespace Qyl.ServiceDefaults.Instrumentation.Db;

/// <summary>
/// Instrumentation helpers for ADO.NET database calls.
/// </summary>
/// <remarks>
/// <para>
/// Called by generated interceptors to wrap DbCommand methods with OpenTelemetry spans.
/// </para>
/// <para>
/// Note: Activity covers command execution, not reader iteration.
/// This matches OTel semantic conventions for db.query spans.
/// </para>
/// </remarks>
public static class DbInstrumentation
{
    private static readonly ConcurrentDictionary<Type, string> s_dbSystemCache = new();

    /// <summary>
    /// Executes <see cref="DbCommand.ExecuteReaderAsync(CancellationToken)"/> with instrumentation.
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
    /// Executes <see cref="DbCommand.ExecuteReader()"/> with instrumentation.
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
    /// Executes <see cref="DbCommand.ExecuteNonQueryAsync(CancellationToken)"/> with instrumentation.
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
    /// Executes <see cref="DbCommand.ExecuteNonQuery()"/> with instrumentation.
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
    /// Executes <see cref="DbCommand.ExecuteScalarAsync(CancellationToken)"/> with instrumentation.
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
    /// Executes <see cref="DbCommand.ExecuteScalar()"/> with instrumentation.
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

    private static Activity? StartDbActivity(DbCommand command, string operationName)
    {
        var activity = ActivitySources.Db.StartActivity("db.query", ActivityKind.Client);

        if (activity is null)
            return null;

        var dbSystem = GetDbSystem(command.Connection);

        activity.SetTag(DbSystemAttributes.Name, dbSystem);
        activity.SetTag(DbOperationAttributes.Name, operationName);

        if (command.CommandText is { Length: > 0 } sql)
            activity.SetTag(DbQueryAttributes.Text, sql);

        if (command.Connection?.Database is { Length: > 0 } dbName)
            activity.SetTag(DbNamespaceAttributes.Namespace, dbName);

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
    /// Maps a DbConnection type to its OTel db.system.name value.
    /// </summary>
    private static string GetDbSystem(DbConnection? connection)
    {
        if (connection is null)
            return "unknown";

        return s_dbSystemCache.GetOrAdd(connection.GetType(), static type =>
            MapTypeNameToDbSystem(type.FullName ?? type.Name));
    }

    /// <summary>
    /// Gets the database system name for a type name. Exposed for testing.
    /// </summary>
    internal static string GetDbSystemForTesting(string typeName) =>
        MapTypeNameToDbSystem(typeName);

    /// <summary>
    /// Maps a type name to the OTel db.system.name semantic convention value.
    /// </summary>
    private static string MapTypeNameToDbSystem(string typeName) =>
        typeName switch
        {
            _ when typeName.Contains("DuckDB", StringComparison.OrdinalIgnoreCase) => "duckdb",
            _ when typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) => "postgresql",
            _ when typeName.Contains("SqlClient", StringComparison.OrdinalIgnoreCase) => "mssql",
            _ when typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) => "sqlite",
            _ when typeName.Contains("Oracle", StringComparison.OrdinalIgnoreCase) => "oracle",
            _ when typeName.Contains("MySql", StringComparison.OrdinalIgnoreCase) => "mysql",
            _ when typeName.Contains("Firebird", StringComparison.OrdinalIgnoreCase) => "firebird",
            _ => "unknown"
        };
}
