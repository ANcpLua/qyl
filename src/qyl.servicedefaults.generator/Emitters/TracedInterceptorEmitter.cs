using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Emitters;

/// <summary>
///     Emits interceptor source code for methods decorated with [Traced] attribute.
/// </summary>
internal static class TracedInterceptorEmitter
{
    // Note: Field name mapping is now computed per-Emit call instead of using ThreadStatic,
    // which avoids potential race conditions during parallel compilation.

    /// <summary>
    ///     Emits the interceptor source code for all traced invocations.
    /// </summary>
    public static string Emit(ImmutableArray<TracedCallSite> invocations)
    {
        if (invocations.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();

        // Build ActivitySource name mapping for this emit operation
        var activitySourceFieldNames = BuildActivitySourceFieldNameMapping(invocations);

        EmitterHelpers.AppendFileHeader(sb, true);
        AppendUsings(sb);
        EmitterHelpers.AppendInterceptsLocationAttribute(sb);
        AppendActivitySourcesClass(sb, activitySourceFieldNames);
        AppendClassOpen(sb);
        AppendInterceptorMethods(sb, invocations, activitySourceFieldNames);
        EmitterHelpers.AppendClassClose(sb);

        return sb.ToString();
    }

    /// <summary>
    ///     Builds a mapping from ActivitySource names to unique field names.
    ///     This is computed per-Emit call to avoid thread-safety issues.
    /// </summary>
    private static Dictionary<string, string> BuildActivitySourceFieldNameMapping(
        ImmutableArray<TracedCallSite> invocations)
    {
        var activitySourceNames = invocations
            .Select(static i => i.ActivitySourceName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();

        var fieldNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in activitySourceNames)
        {
            var baseFieldName = SanitizeFieldName(name);
            var fieldName = baseFieldName;
            var counter = 1;

            // Ensure uniqueness
            while (!usedFieldNames.Add(fieldName))
                fieldName = $"{baseFieldName}_{counter++}";

            fieldNames[name] = fieldName;
        }

        return fieldNames;
    }


    private static void AppendUsings(StringBuilder sb)
    {
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine();
    }

    private static void AppendActivitySourcesClass(
        StringBuilder sb,
        Dictionary<string, string> activitySourceFieldNames)
    {
        var activitySourceNames = activitySourceFieldNames.Keys
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();

        sb.AppendLine("namespace Qyl.ServiceDefaults.Generator");
        sb.AppendLine("{");
        sb.AppendLine("    file static class TracedActivitySources");
        sb.AppendLine("    {");

        foreach (var name in activitySourceNames)
        {
            var fieldName = activitySourceFieldNames[name];
            sb.AppendLine(
                $"        internal static readonly global::System.Diagnostics.ActivitySource {fieldName} = new(\"{name}\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string GetActivitySourceFieldName(
        string activitySourceName,
        Dictionary<string, string> activitySourceFieldNames)
    {
        return activitySourceFieldNames.TryGetValue(activitySourceName, out var fieldName)
            ? fieldName
            : SanitizeFieldName(activitySourceName);
    }

    private static string SanitizeFieldName(string activitySourceName)
    {
        // Convert "MyApp.Orders" to "MyApp_Orders"
        var sanitized = new StringBuilder();
        foreach (var c in activitySourceName) sanitized.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sanitized.ToString();
    }

    private static void AppendClassOpen(StringBuilder sb)
    {
        sb.AppendLine("""
                      namespace Qyl.ServiceDefaults.Generator
                      {
                          file static class TracedInterceptors
                          {
                      """);
    }

    private static void AppendInterceptorMethods(
        StringBuilder sb,
        ImmutableArray<TracedCallSite> invocations,
        Dictionary<string, string> activitySourceFieldNames)
    {
        var orderedCallSites = invocations
            .OrderBy(static cs => cs.SortKey, StringComparer.Ordinal);

        var index = 0;
        foreach (var callSite in orderedCallSites)
        {
            AppendSingleInterceptor(sb, callSite, index, activitySourceFieldNames);
            index++;
        }
    }

    private static void AppendSingleInterceptor(
        StringBuilder sb,
        TracedCallSite callSite,
        int index,
        Dictionary<string, string> activitySourceFieldNames)
    {
        var displayLocation = callSite.Location.GetDisplayLocation();
        var interceptAttribute = callSite.Location.GetInterceptsLocationAttributeSyntax();

        var methodName = $"Intercept_Traced_{index}";
        var typeParamNames = GetTypeParameterNames(callSite);
        var returnType = ToGlobalTypeName(callSite.ReturnTypeName, typeParamNames);
        var containingType = callSite.ContainingTypeName;
        var originalMethod = callSite.MethodName;
        var activitySourceField = GetActivitySourceFieldName(callSite.ActivitySourceName, activitySourceFieldNames);

        var typeParams = BuildTypeParameterList(callSite);
        var constraints = BuildConstraintClauses(callSite);
        var parameters = BuildParameterList(callSite, containingType, typeParamNames);
        var arguments = BuildArgumentList(callSite);
        var tagSetters = BuildTagSetters(callSite);
        var methodCall = callSite.IsStatic
            ? $"global::{containingType}.{originalMethod}{typeParams}({arguments})"
            : $"@this.{originalMethod}{typeParams}({arguments})";

        if (callSite.IsAsync)
            EmitAsyncInterceptor(sb, callSite, methodName, typeParams, constraints, returnType, parameters,
                displayLocation, interceptAttribute, activitySourceField, tagSetters, methodCall);
        else
            EmitSyncInterceptor(sb, callSite, methodName, typeParams, constraints, returnType, parameters,
                displayLocation, interceptAttribute, activitySourceField, tagSetters, methodCall);
    }

    private static void EmitAsyncInterceptor(
        StringBuilder sb,
        TracedCallSite callSite,
        string methodName,
        string typeParams,
        string constraints,
        string returnType,
        string parameters,
        string displayLocation,
        string interceptAttribute,
        string activitySourceField,
        string tagSetters,
        string methodCall)
    {
        var hasReturnValue = !returnType.EndsWith("Task", StringComparison.Ordinal) &&
                             !returnType.EndsWith("ValueTask", StringComparison.Ordinal);

        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static async {{returnType}} {{methodName}}{{typeParams}}({{parameters}}){{constraints}}
                                {
                                    using var activity = TracedActivitySources.{{activitySourceField}}.StartActivity(
                                        "{{callSite.SpanName}}",
                                        global::System.Diagnostics.ActivityKind.{{callSite.SpanKind}});
                        """);

        if (!string.IsNullOrEmpty(tagSetters))
        {
            sb.AppendLine();
            sb.AppendLine("            if (activity is not null)");
            sb.AppendLine("            {");
            sb.Append(tagSetters);
            sb.AppendLine("            }");
        }

        sb.AppendLine();

        sb.AppendLine(hasReturnValue
            ? $$"""
                            try
                            {
                                var result = await {{methodCall}};
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                                return result;
                            }
                            catch (global::System.Exception ex)
                            {
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                activity?.AddEvent(new global::System.Diagnostics.ActivityEvent("exception", tags: new global::System.Diagnostics.ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));
                                throw;
                            }
                        }

                """
            : $$"""
                            try
                            {
                                await {{methodCall}};
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                            }
                            catch (global::System.Exception ex)
                            {
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                activity?.AddEvent(new global::System.Diagnostics.ActivityEvent("exception", tags: new global::System.Diagnostics.ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));
                                throw;
                            }
                        }

                """);
    }

    private static void EmitSyncInterceptor(
        StringBuilder sb,
        TracedCallSite callSite,
        string methodName,
        string typeParams,
        string constraints,
        string returnType,
        string parameters,
        string displayLocation,
        string interceptAttribute,
        string activitySourceField,
        string tagSetters,
        string methodCall)
    {
        var hasReturnValue = returnType != "void";

        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static {{returnType}} {{methodName}}{{typeParams}}({{parameters}}){{constraints}}
                                {
                                    using var activity = TracedActivitySources.{{activitySourceField}}.StartActivity(
                                        "{{callSite.SpanName}}",
                                        global::System.Diagnostics.ActivityKind.{{callSite.SpanKind}});
                        """);

        if (!string.IsNullOrEmpty(tagSetters))
        {
            sb.AppendLine();
            sb.AppendLine("            if (activity is not null)");
            sb.AppendLine("            {");
            sb.Append(tagSetters);
            sb.AppendLine("            }");
        }

        sb.AppendLine();

        sb.AppendLine(hasReturnValue
            ? $$"""
                            try
                            {
                                var result = {{methodCall}};
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                                return result;
                            }
                            catch (global::System.Exception ex)
                            {
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                activity?.AddEvent(new global::System.Diagnostics.ActivityEvent("exception", tags: new global::System.Diagnostics.ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));
                                throw;
                            }
                        }

                """
            : $$"""
                            try
                            {
                                {{methodCall}};
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                            }
                            catch (global::System.Exception ex)
                            {
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                activity?.AddEvent(new global::System.Diagnostics.ActivityEvent("exception", tags: new global::System.Diagnostics.ActivityTagsCollection { { "exception.type", ex.GetType().FullName }, { "exception.message", ex.Message } }));
                                throw;
                            }
                        }

                """);
    }

    private static string BuildParameterList(TracedCallSite callSite, string containingType,
        IReadOnlyList<string>? typeParamNames = null)
    {
        var sb = new StringBuilder();

        // Static methods don't have a 'this' parameter
        if (!callSite.IsStatic) sb.Append($"this global::{containingType} @this");

        for (var i = 0; i < callSite.ParameterTypes.Count; i++)
        {
            if (sb.Length > 0)
                sb.Append(", ");
            var typeName = ToGlobalTypeName(callSite.ParameterTypes[i], typeParamNames);
            sb.Append($"{typeName} {callSite.ParameterNames[i]}");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Converts a type name to a fully qualified global:: format.
    ///     Handles C# keyword aliases, generics, and nullable types recursively.
    /// </summary>
    private static string ToGlobalTypeName(string typeName, IReadOnlyList<string>? typeParameterNames = null)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;

        // Check if this is a type parameter (T, TResult, etc.)
        if (typeParameterNames is not null && typeParameterNames.Contains(typeName))
            return typeName;

        // Handle nullable reference types (trailing ?)
        if (typeName.EndsWith("?", StringComparison.Ordinal))
            return ToGlobalTypeName(typeName[..^1], typeParameterNames) + "?";

        // Handle generic types: Task<Order> or Dictionary<string, Order>
        var genericStart = typeName.IndexOf('<');
        if (genericStart > 0 && typeName.EndsWith(">", StringComparison.Ordinal))
        {
            var baseTypeName = typeName[..genericStart];
            var argsContent = typeName[(genericStart + 1)..^1];

            var args = ParseGenericArguments(argsContent);
            var qualifiedArgs = args.Select(a => ToGlobalTypeName(a, typeParameterNames));

            return $"{ToGlobalTypeName(baseTypeName, typeParameterNames)}<{string.Join(", ", qualifiedArgs)}>";
        }

        // Map C# keyword aliases to their BCL names
        var mapped = typeName switch
        {
            "string" => "System.String",
            "int" => "System.Int32",
            "long" => "System.Int64",
            "short" => "System.Int16",
            "byte" => "System.Byte",
            "sbyte" => "System.SByte",
            "uint" => "System.UInt32",
            "ulong" => "System.UInt64",
            "ushort" => "System.UInt16",
            "float" => "System.Single",
            "double" => "System.Double",
            "decimal" => "System.Decimal",
            "bool" => "System.Boolean",
            "char" => "System.Char",
            "object" => "System.Object",
            "void" => "void",
            _ => typeName
        };

        return mapped == "void" ? "void" : $"global::{mapped}";
    }

    /// <summary>
    ///     Parses generic type arguments, handling nested generics correctly.
    /// </summary>
    private static List<string> ParseGenericArguments(string argsContent)
    {
        var args = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < argsContent.Length; i++)
            switch (argsContent[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth is 0:
                    args.Add(argsContent[start..i].Trim());
                    start = i + 1;
                    break;
            }

        if (start < argsContent.Length)
            args.Add(argsContent[start..].Trim());

        return args;
    }

    private static string BuildTypeParameterList(TracedCallSite callSite)
    {
        if (callSite.TypeParameters.Count is 0)
            return "";

        return "<" + string.Join(", ", callSite.TypeParameters.Select(static tp => tp.Name)) + ">";
    }

    private static string BuildConstraintClauses(TracedCallSite callSite)
    {
        var constraints = callSite.TypeParameters
            .Where(static tp => tp.Constraints is not null)
            .Select(static tp => tp.Constraints);

        var clauseList = constraints.ToList();
        return clauseList.Count > 0 ? " " + string.Join(" ", clauseList) : "";
    }

    private static IReadOnlyList<string> GetTypeParameterNames(TracedCallSite callSite)
    {
        return [.. callSite.TypeParameters.Select(static tp => tp.Name)];
    }

    private static string BuildArgumentList(TracedCallSite callSite)
    {
        return callSite.ParameterNames.Count is 0 ? string.Empty : string.Join(", ", callSite.ParameterNames);
    }

    private static string BuildTagSetters(TracedCallSite callSite)
    {
        if (callSite.TracedTags.Count is 0)
            return string.Empty;

        var sb = new StringBuilder();

        foreach (var tag in callSite.TracedTags)
            if (tag.SkipIfNull && tag.IsNullable)
            {
                sb.AppendLine($"                if ({tag.ParameterName} is not null)");
                sb.AppendLine($"                    activity.SetTag(\"{tag.TagName}\", {tag.ParameterName});");
            }
            else
            {
                sb.AppendLine($"                activity.SetTag(\"{tag.TagName}\", {tag.ParameterName});");
            }

        return sb.ToString();
    }
}
