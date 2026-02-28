using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Emitters;

internal static class TracedInterceptorEmitter
{
    public static string Emit(ImmutableArray<TracedCallSite> invocations)
    {
        if (invocations.IsEmpty)
            return string.Empty;

        var fieldNames = BuildFieldNameMap(invocations);
        var sb = new StringBuilder();

        EmitterHelpers.AppendFileHeader(sb, suppressWarnings: true);
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine();
        EmitterHelpers.AppendInterceptsLocationAttribute(sb);
        AppendActivitySourcesClass(sb, fieldNames);

        sb.AppendLine("namespace Qyl.ServiceDefaults.Generator");
        sb.AppendLine("{");
        sb.AppendLine("    file static class TracedInterceptors");
        sb.AppendLine("    {");

        var index = 0;
        foreach (var cs in invocations.OrderBy(static cs => cs.SortKey, StringComparer.Ordinal))
            AppendInterceptor(sb, cs, index++, fieldNames);

        EmitterHelpers.AppendClassClose(sb);
        return sb.ToString();
    }

    // ── ActivitySource field map ──────────────────────────────────────────────

    private static Dictionary<string, string> BuildFieldNameMap(ImmutableArray<TracedCallSite> invocations)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in invocations.Select(static i => i.ActivitySourceName).Distinct(StringComparer.Ordinal).OrderBy(static n => n, StringComparer.Ordinal))
        {
            var field = Sanitize(name);
            var candidate = field;
            var n = 1;
            while (!used.Add(candidate)) candidate = $"{field}_{n++}";
            map[name] = candidate;
        }
        return map;
    }

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static void AppendActivitySourcesClass(StringBuilder sb, Dictionary<string, string> fieldNames)
    {
        sb.AppendLine("namespace Qyl.ServiceDefaults.Generator");
        sb.AppendLine("{");
        sb.AppendLine("    file static class TracedActivitySources");
        sb.AppendLine("    {");
        foreach (var kv in fieldNames.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            var (name, field) = (kv.Key, kv.Value);
            sb.AppendLine($"        internal static readonly global::System.Diagnostics.ActivitySource {field} = new(\"{name}\");");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    // ── Per-call-site interceptor ─────────────────────────────────────────────

    private static void AppendInterceptor(
        StringBuilder sb,
        TracedCallSite cs,
        int index,
        Dictionary<string, string> fieldNames)
    {
        var typeParamNames = cs.TypeParameters.Select(static tp => tp.Name).ToArray().ToEquatableArray();
        var returnType = cs.ReturnTypeName.ToGlobalTypeName(typeParamNames.IsDefaultOrEmpty ? null : typeParamNames.AsImmutableArray());
        var typeParams = cs.TypeParameters.Length is 0 ? "" : "<" + string.Join(", ", cs.TypeParameters.Select(static tp => tp.Name)) + ">";
        var constraints = cs.TypeParameters.Length is 0 ? "" : " " + string.Join(" ", cs.TypeParameters.Where(static tp => tp.Constraints is not null).Select(static tp => tp.Constraints!));
        var parameters = EmitterHelpers.BuildParameterList(cs.ContainingTypeName, cs.ParameterTypes, cs.ParameterNames, cs.IsStatic, typeParamNames);
        var arguments = EmitterHelpers.BuildArgumentList(cs.ParameterNames);
        var field = fieldNames.TryGetValue(cs.ActivitySourceName, out var f) ? f : Sanitize(cs.ActivitySourceName);
        var call = cs.IsStatic
            ? $"global::{cs.ContainingTypeName}.{cs.MethodName}{typeParams}({arguments})"
            : $"@this.{cs.MethodName}{typeParams}({arguments})";

        var display = cs.Location.GetDisplayLocation();
        var intercept = cs.Location.GetInterceptsLocationAttributeSyntax();

        if (cs.IsAsyncEnumerable)
            AppendAsyncEnumerableInterceptor(sb, cs, index, typeParams, constraints, returnType, parameters, field, call, display, intercept);
        else if (cs.IsAsync)
            AppendAsyncInterceptor(sb, cs, index, typeParams, constraints, returnType, parameters, field, call, display, intercept);
        else
            AppendSyncInterceptor(sb, cs, index, typeParams, constraints, returnType, parameters, field, call, display, intercept);
    }

    // ── Sync interceptor ──────────────────────────────────────────────────────

    private static void AppendSyncInterceptor(
        StringBuilder sb, TracedCallSite cs, int index,
        string typeParams, string constraints, string returnType, string parameters,
        string field, string call, string display, string intercept)
    {
        var hasReturn = returnType != "void";
        var startActivity = BuildStartActivity(field, cs);
        var tagSetters = BuildAllTagSetters(cs);
        var returnCapture = BuildReturnCaptureExpr(cs.ReturnCapture, "result");

        sb.AppendLine($$"""
                                // {{display}}
                                {{intercept}}
                                public static {{returnType}} Intercept_Traced_{{index}}{{typeParams}}({{parameters}}){{constraints}}
                                {
                                    {{startActivity}}
                        {{tagSetters}}
                                    try
                                    {
                        """);

        if (hasReturn)
        {
            sb.AppendLine($"                var result = {call};");
            if (returnCapture is not null)
                sb.AppendLine($"                activity?.SetTag({returnCapture});");
            sb.AppendLine("                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
            sb.AppendLine("                return result;");
        }
        else
        {
            sb.AppendLine($"                {call};");
            sb.AppendLine("                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
        }

        sb.AppendLine($$"""
                                    }
                                    catch (global::System.Exception ex)
                                    {
                        {{ExceptionBlock}}
                                    }
                                }

                        """);
    }

    // ── Async (Task/ValueTask) interceptor ────────────────────────────────────

    private static void AppendAsyncInterceptor(
        StringBuilder sb, TracedCallSite cs, int index,
        string typeParams, string constraints, string returnType, string parameters,
        string field, string call, string display, string intercept)
    {
        var hasReturn = !returnType.EndsWithOrdinal("Task") && !returnType.EndsWithOrdinal("ValueTask");
        var startActivity = BuildStartActivity(field, cs);
        var tagSetters = BuildAllTagSetters(cs);
        var returnCapture = BuildReturnCaptureExpr(cs.ReturnCapture, "result");

        sb.AppendLine($$"""
                                // {{display}}
                                {{intercept}}
                                public static async {{returnType}} Intercept_Traced_{{index}}{{typeParams}}({{parameters}}){{constraints}}
                                {
                                    {{startActivity}}
                        {{tagSetters}}
                                    try
                                    {
                        """);

        if (hasReturn)
        {
            sb.AppendLine($"                var result = await {call};");
            if (returnCapture is not null)
                sb.AppendLine($"                activity?.SetTag({returnCapture});");
            sb.AppendLine("                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
            sb.AppendLine("                return result;");
        }
        else
        {
            sb.AppendLine($"                await {call};");
            sb.AppendLine("                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);");
        }

        sb.AppendLine($$"""
                                    }
                                    catch (global::System.Exception ex)
                                    {
                        {{ExceptionBlock}}
                                    }
                                }

                        """);
    }

    // ── IAsyncEnumerable interceptor (T-002) ──────────────────────────────────

    private static void AppendAsyncEnumerableInterceptor(
        StringBuilder sb, TracedCallSite cs, int index,
        string typeParams, string constraints, string returnType, string parameters,
        string field, string call, string display, string intercept)
    {
        var startActivity = BuildStartActivity(field, cs);
        var tagSetters = BuildAllTagSetters(cs);

        sb.AppendLine($$"""
                                // {{display}}
                                {{intercept}}
                                public static async {{returnType}} Intercept_Traced_{{index}}{{typeParams}}({{parameters}}){{constraints}}
                                {
                                    {{startActivity}}
                        {{tagSetters}}
                                    try
                                    {
                                        await foreach (var __item in {{call}})
                                            yield return __item;
                                        activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                                    }
                                    catch (global::System.Exception ex)
                                    {
                        {{ExceptionBlock}}
                                    }
                                }

                        """);
    }

    // ── Building blocks ───────────────────────────────────────────────────────

    // T-001: parentContext: default breaks context inheritance for root spans.
    private static string BuildStartActivity(string field, TracedCallSite cs) =>
        cs.RootSpan
            ? $"""using var activity = TracedActivitySources.{field}.StartActivity("{cs.SpanName}", global::System.Diagnostics.ActivityKind.{cs.SpanKind}, parentContext: default);"""
            : $"""using var activity = TracedActivitySources.{field}.StartActivity("{cs.SpanName}", global::System.Diagnostics.ActivityKind.{cs.SpanKind});""";

    private static string BuildAllTagSetters(TracedCallSite cs)
    {
        var sb = new StringBuilder();
        AppendParameterTagSetters(sb, cs);
        AppendPropertyTagSetters(sb, cs);
        if (sb.Length is 0) return string.Empty;

        var wrapped = new StringBuilder();
        wrapped.AppendLine("            if (activity is not null)");
        wrapped.AppendLine("            {");
        wrapped.Append(sb);
        wrapped.AppendLine("            }");
        return wrapped.ToString();
    }

    private static void AppendParameterTagSetters(StringBuilder sb, TracedCallSite cs)
    {
        foreach (var tag in cs.TracedTags)
        {
            // T-006: value types with SkipIfDefault
            if (tag is { SkipIfDefault: true, IsValueType: true })
            {
                var globalType = tag.TypeName.ToGlobalTypeName(null);
                sb.AppendLine($"                if (!global::System.Collections.Generic.EqualityComparer<{globalType}>.Default.Equals({tag.ParameterName}, default))");
                sb.AppendLine($"                    activity.SetTag(\"{tag.TagName}\", {tag.ParameterName});");
            }
            else if (tag is { SkipIfNull: true, IsNullable: true })
            {
                sb.AppendLine($"                if ({tag.ParameterName} is not null)");
                sb.AppendLine($"                    activity.SetTag(\"{tag.TagName}\", {tag.ParameterName});");
            }
            else
            {
                sb.AppendLine($"                activity.SetTag(\"{tag.TagName}\", {tag.ParameterName});");
            }
        }
    }

    // T-004: property-level tag setters
    private static void AppendPropertyTagSetters(StringBuilder sb, TracedCallSite cs)
    {
        foreach (var prop in cs.TracedTagProperties)
        {
            var accessor = prop.IsStatic
                ? $"global::{cs.ContainingTypeName}.{prop.PropertyName}"
                : $"@this.{prop.PropertyName}";

            if (prop is { SkipIfNull: true, IsNullable: true })
            {
                sb.AppendLine($"                if ({accessor} is not null)");
                sb.AppendLine($"                    activity.SetTag(\"{prop.TagName}\", {accessor});");
            }
            else
            {
                sb.AppendLine($"                activity.SetTag(\"{prop.TagName}\", {accessor});");
            }
        }
    }

    // T-007: build the SetTag expression for the return value, or null if not applicable.
    private static string? BuildReturnCaptureExpr(TracedReturnInfo? capture, string resultVar)
    {
        if (capture is null) return null;
        var accessor = capture.PropertyPath is { Length: > 0 }
            ? resultVar + "?." + string.Join("?.", capture.PropertyPath.Split('.'))
            : $"{resultVar}?.ToString()";
        return $"\"{capture.TagName}\", {accessor}";
    }

    // T-003: OTel standard exception semconv (exception.type / message / stacktrace / escaped).
    private const string ExceptionBlock = """
                            activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                            activity?.AddEvent(new global::System.Diagnostics.ActivityEvent(
                                "exception",
                                tags: new global::System.Diagnostics.ActivityTagsCollection
                                {
                                    { "exception.type",       ex.GetType().FullName },
                                    { "exception.message",    ex.Message },
                                    { "exception.stacktrace", ex.ToString() },
                                    { "exception.escaped",    true },
                                }));
                            throw;
                    """;
}
