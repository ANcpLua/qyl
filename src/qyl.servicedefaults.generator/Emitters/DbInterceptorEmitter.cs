using System.Collections.Immutable;
using System.Text;
using Qyl.ServiceDefaults.Generator.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.ServiceDefaults.Generator.Emitters;

/// <summary>
///     Emits interceptor source code for ADO.NET DbCommand method invocations.
/// </summary>
internal static class DbInterceptorEmitter
{
    /// <summary>
    ///     Emits the interceptor source code for all DbCommand invocations.
    /// </summary>
    public static string Emit(ImmutableArray<DbCallSite> callSites)
    {
        if (callSites.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();

        EmitterHelpers.AppendFileHeader(sb, suppressWarnings: true);
        AppendUsings(sb);
        EmitterHelpers.AppendInterceptsLocationAttribute(sb);
        AppendClassOpen(sb);
        AppendInterceptorMethods(sb, callSites);
        EmitterHelpers.AppendClassClose(sb);

        return sb.ToString();
    }


    private static void AppendUsings(StringBuilder sb)
    {
        sb.AppendLine("using System.Data.Common;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Qyl.ServiceDefaults.Instrumentation.Db;");
        sb.AppendLine();
    }

    private static void AppendClassOpen(StringBuilder sb)
    {
        sb.AppendLine("""
                      namespace Qyl.ServiceDefaults.Generator
                      {
                          file static class DbInterceptors
                          {
                      """);
    }

    private static void AppendInterceptorMethods(
        StringBuilder sb,
        ImmutableArray<DbCallSite> callSites)
    {
        var orderedCallSites = callSites
            .OrderBy(static cs => cs.SortKey, StringComparer.Ordinal);

        var index = 0;
        foreach (var callSite in orderedCallSites)
        {
            AppendSingleInterceptor(sb, callSite, index);
            index++;
        }
    }

    private static void AppendSingleInterceptor(
        StringBuilder sb,
        DbCallSite callSite,
        int index)
    {
        var displayLocation = callSite.Location.GetDisplayLocation();
        var interceptAttribute = callSite.Location.GetInterceptsLocationAttributeSyntax();

        var methodName = $"Intercept_Db_{index}";

        var commandType = callSite.ConcreteCommandType is not null
            ? $"global::{callSite.ConcreteCommandType}"
            : "global::System.Data.Common.DbCommand";

        switch (callSite.Method)
        {
            case DbCommandMethod.ExecuteReader when callSite.IsAsync:
                EmitExecuteReaderAsync(sb, displayLocation, interceptAttribute, methodName, commandType);
                break;

            case DbCommandMethod.ExecuteReader:
                EmitExecuteReader(sb, displayLocation, interceptAttribute, methodName, commandType);
                break;

            case DbCommandMethod.ExecuteNonQuery when callSite.IsAsync:
                EmitExecuteNonQueryAsync(sb, displayLocation, interceptAttribute, methodName, commandType);
                break;

            case DbCommandMethod.ExecuteNonQuery:
                EmitExecuteNonQuery(sb, displayLocation, interceptAttribute, methodName, commandType);
                break;

            case DbCommandMethod.ExecuteScalar when callSite.IsAsync:
                EmitExecuteScalarAsync(sb, displayLocation, interceptAttribute, methodName, commandType);
                break;

            case DbCommandMethod.ExecuteScalar:
                EmitExecuteScalar(sb, displayLocation, interceptAttribute, methodName, commandType);
                break;
        }
    }

    private static void EmitExecuteReaderAsync(
        StringBuilder sb,
        string displayLocation,
        string interceptAttribute,
        string methodName,
        string commandType)
    {
        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::System.Threading.Tasks.Task<global::System.Data.Common.DbDataReader> {{methodName}}(
                                    this {{commandType}} command,
                                    global::System.Threading.CancellationToken cancellationToken = default)
                                {
                                    return DbInstrumentation.ExecuteReaderAsync(command, cancellationToken);
                                }

                        """);
    }

    private static void EmitExecuteReader(
        StringBuilder sb,
        string displayLocation,
        string interceptAttribute,
        string methodName,
        string commandType)
    {
        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::System.Data.Common.DbDataReader {{methodName}}(
                                    this {{commandType}} command)
                                {
                                    return DbInstrumentation.ExecuteReader(command);
                                }

                        """);
    }

    private static void EmitExecuteNonQueryAsync(
        StringBuilder sb,
        string displayLocation,
        string interceptAttribute,
        string methodName,
        string commandType)
    {
        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::System.Threading.Tasks.Task<int> {{methodName}}(
                                    this {{commandType}} command,
                                    global::System.Threading.CancellationToken cancellationToken = default)
                                {
                                    return DbInstrumentation.ExecuteNonQueryAsync(command, cancellationToken);
                                }

                        """);
    }

    private static void EmitExecuteNonQuery(
        StringBuilder sb,
        string displayLocation,
        string interceptAttribute,
        string methodName,
        string commandType)
    {
        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static int {{methodName}}(
                                    this {{commandType}} command)
                                {
                                    return DbInstrumentation.ExecuteNonQuery(command);
                                }

                        """);
    }

    private static void EmitExecuteScalarAsync(
        StringBuilder sb,
        string displayLocation,
        string interceptAttribute,
        string methodName,
        string commandType)
    {
        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::System.Threading.Tasks.Task<object?> {{methodName}}(
                                    this {{commandType}} command,
                                    global::System.Threading.CancellationToken cancellationToken = default)
                                {
                                    return DbInstrumentation.ExecuteScalarAsync(command, cancellationToken);
                                }

                        """);
    }

    private static void EmitExecuteScalar(
        StringBuilder sb,
        string displayLocation,
        string interceptAttribute,
        string methodName,
        string commandType)
    {
        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static object? {{methodName}}(
                                    this {{commandType}} command)
                                {
                                    return DbInstrumentation.ExecuteScalar(command);
                                }

                        """);
    }

}
