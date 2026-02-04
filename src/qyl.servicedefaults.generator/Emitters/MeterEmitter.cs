using System.Collections.Immutable;
using System.Text;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Emitters;

/// <summary>
///     Emits meter implementations for [Meter] attributed partial classes.
/// </summary>
internal static class MeterEmitter
{
    /// <summary>
    ///     Emits the meter implementation source code for all meter classes.
    /// </summary>
    public static string Emit(ImmutableArray<MeterDefinition> meters)
    {
        if (meters.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();

        EmitterHelpers.AppendFileHeader(sb, nullableEnable: true);
        AppendUsings(sb);

        foreach (var meter in meters.OrderBy(static m => m.SortKey, StringComparer.Ordinal))
            AppendMeterClass(sb, meter);

        return sb.ToString();
    }


    private static void AppendUsings(StringBuilder sb)
    {
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Diagnostics.Metrics;");
        sb.AppendLine();
    }

    private static void AppendMeterClass(StringBuilder sb, MeterDefinition meter)
    {
        sb.AppendLine($"namespace {meter.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {meter.ClassName}");
        sb.AppendLine("    {");

        // Meter field
        var meterCtor = meter.MeterVersion is not null
            ? $"new Meter(\"{meter.MeterName}\", \"{meter.MeterVersion}\")"
            : $"new Meter(\"{meter.MeterName}\")";

        sb.AppendLine($"        private static readonly Meter _meter = {meterCtor};");
        sb.AppendLine();

        // Instrument fields
        foreach (var method in meter.Methods) AppendInstrumentField(sb, method);

        sb.AppendLine();

        // Method implementations
        foreach (var method in meter.Methods) AppendMethodImplementation(sb, method);

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendInstrumentField(StringBuilder sb, MetricMethodDefinition method)
    {
        var fieldName = ToFieldName(method.MetricName);

        // Build arguments conditionally for cleaner generated code
        var args = $"\"{method.MetricName}\"";

        if (method.Unit is not null || method.Description is not null)
        {
            var unitArg = method.Unit is not null ? $"\"{method.Unit}\"" : "null";
            args += $", {unitArg}";
        }

        if (method.Description is not null)
            args += $", \"{method.Description}\"";

        switch (method.Kind)
        {
            case MetricKind.Counter:
                sb.AppendLine($"        private static readonly Counter<long> {fieldName} =");
                sb.AppendLine($"            _meter.CreateCounter<long>({args});");
                break;

            case MetricKind.Histogram:
            {
                var valueType = method.ValueTypeName ?? "double";
                sb.AppendLine($"        private static readonly Histogram<{valueType}> {fieldName} =");
                sb.AppendLine($"            _meter.CreateHistogram<{valueType}>({args});");
                break;
            }

            case MetricKind.Gauge:
            {
                // ObservableGauge uses stored value pattern:
                // 1. Storage field for current value
                // 2. ObservableGauge reads from storage via callback
                var valueType = method.ValueTypeName ?? "long";
                var storageFieldName = ToStorageFieldName(method.MetricName);

                sb.AppendLine($"        private static {valueType} {storageFieldName};");
                sb.AppendLine($"        private static readonly ObservableGauge<{valueType}> {fieldName} =");
                sb.AppendLine($"            _meter.CreateObservableGauge({args}, () => {storageFieldName});");
                break;
            }
        }
    }

    private static void AppendMethodImplementation(StringBuilder sb, MetricMethodDefinition method)
    {
        var fieldName = ToFieldName(method.MetricName);

        // Build parameter list
        var paramParts = new List<string>();

        // Histogram and Gauge both take a value parameter
        if (method.Kind is MetricKind.Histogram or MetricKind.Gauge && method.ValueTypeName is not null)
            paramParts.Add($"{method.ValueTypeName} value");

        foreach (var tag in method.Tags) paramParts.Add($"{tag.TypeName} {tag.ParameterName}");

        var paramList = string.Join(", ", paramParts);

        sb.AppendLine($"        public static partial void {method.MethodName}({paramList})");

        // Gauge uses expression-body syntax for simple storage update
        if (method.Kind == MetricKind.Gauge)
        {
            var storageFieldName = ToStorageFieldName(method.MetricName);
            sb.AppendLine($"            => {storageFieldName} = value;");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("        {");

        if (method.Tags.Count is 0)
        {
            sb.AppendLine(method.Kind == MetricKind.Counter
                ? $"            {fieldName}.Add(1);"
                : $"            {fieldName}.Record(value);");
        }
        else if (method.Tags.Count == 1)
        {
            var tag = method.Tags[0];
            var kvp = $"new KeyValuePair<string, object?>(\"{tag.TagName}\", {tag.ParameterName})";

            sb.AppendLine(method.Kind == MetricKind.Counter
                ? $"            {fieldName}.Add(1, {kvp});"
                : $"            {fieldName}.Record(value, {kvp});");
        }
        else
        {
            // Multiple tags - use params array
            sb.Append("            var tags = new KeyValuePair<string, object?>[] { ");

            var tagList = method.Tags
                .Select(static t => $"new(\"{t.TagName}\", {t.ParameterName})")
                .ToList();

            sb.Append(string.Join(", ", tagList));
            sb.AppendLine(" };");

            sb.AppendLine(method.Kind == MetricKind.Counter
                ? $"            {fieldName}.Add(1, tags);"
                : $"            {fieldName}.Record(value, tags);");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string ToFieldName(string metricName)
    {
        // orders.created -> _ordersCreated
        var parts = metricName.Split('.');
        var sb = new StringBuilder("_");

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (i is 0)
                sb.Append(part);
            else
                sb.Append(char.ToUpperInvariant(part[0])).Append(part[1..]);
        }

        return sb.ToString();
    }

    private static string ToStorageFieldName(string metricName)
    {
        // system.memory.usage -> _currentSystemMemoryUsage
        var parts = metricName.Split('.');
        var sb = new StringBuilder("_current");

        foreach (var part in parts)
            sb.Append(char.ToUpperInvariant(part[0])).Append(part[1..]);

        return sb.ToString();
    }
}
