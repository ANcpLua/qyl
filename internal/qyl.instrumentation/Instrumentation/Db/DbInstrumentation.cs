using System.Collections.Concurrent;
using System.Data.Common;
using DbAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Db.DbAttributes;

namespace Qyl.Instrumentation.Instrumentation.Db;

public static class DbInstrumentation
{
    private static readonly ConcurrentDictionary<Type, string> s_dbSystemCache = new();

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
        var operationName = SqlOperationParser.TryParse(command.CommandText) ?? fallbackOperationName;
        var collectionName = SqlOperationParser.TryParseCollectionName(command.CommandText);

        var spanName = collectionName is not null
            ? $"{operationName} {collectionName}"
            : operationName;

        if (ActivitySources.DbSource.StartActivity(spanName, ActivityKind.Client, default(ActivityContext)) is not
            { } activity)
            return null;

        var dbSystem = GetDbSystem(command.Connection);

        activity.SetTag(DbAttributes.SystemName, dbSystem);
        activity.SetTag(DbAttributes.OperationName, operationName);

        if (collectionName is not null)
            activity.SetTag(DbAttributes.CollectionName, collectionName);

        if (command.Connection?.Database is { Length: > 0 } dbName)
            activity.SetTag(DbAttributes.Namespace, dbName);

        return activity;
    }

    private static void SetErrorStatus(Activity? activity, Exception ex) =>
        ActivityExceptionTelemetry.Record(activity, ex);

    private static string GetDbSystem(DbConnection? connection)
    {
        if (connection is null)
            return DbAttributes.SystemNameValues.OtherSql;

        return s_dbSystemCache.GetOrAdd(connection.GetType(), static type =>
            MapTypeNameToDbSystem(type.FullName ?? type.Name));
    }

    internal static string GetDbSystemForTesting(string typeName) =>
        MapTypeNameToDbSystem(typeName);

    private static string MapTypeNameToDbSystem(string typeName) =>
        typeName switch
        {
            // DuckDB has no stable db.system.name value in the semconv package; use the official SQL fallback.
            _ when typeName.ContainsIgnoreCase("DuckDB") => DbAttributes.SystemNameValues.OtherSql,
            _ when typeName.ContainsIgnoreCase("Npgsql") => DbAttributes.SystemNameValues.Postgresql,
            _ when typeName.ContainsIgnoreCase("SqlClient") => DbAttributes.SystemNameValues.MicrosoftSqlServer,
            _ when typeName.ContainsIgnoreCase("Sqlite") => DbAttributes.SystemNameValues.Sqlite,
            _ when typeName.ContainsIgnoreCase("Oracle") => DbAttributes.SystemNameValues.OracleDb,
            _ when typeName.ContainsIgnoreCase("MySql") => DbAttributes.SystemNameValues.Mysql,
            _ when typeName.ContainsIgnoreCase("Firebird") => DbAttributes.SystemNameValues.Firebirdsql,
            _ => DbAttributes.SystemNameValues.OtherSql
        };
}
