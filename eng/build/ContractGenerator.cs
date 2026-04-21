// eng/build/ContractGenerator.cs

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Nuke.Common.IO;
using Serilog;

/// <summary>
///     Generates DomainContracts.g.cs for the instrumentation generator + collector from a
///     compile-time domain table inlined below. Previously read eng/semconv/qyl-extensions.json;
///     that JSON was deleted in the Weaver cutover (PR #141). The attribute lists that survived
///     the migration now live here as the single source of truth.
/// </summary>
public static class ContractGenerator
{
    private const string SchemaVersion = "semconv-1.40.0";

    // Attribute lists extracted from the former qyl-extensions.json facades. Each domain's
    // `required` set marks attributes that MUST be present on every emitted span/metric of
    // that signal; the rest are recommended. Bumping semconv = edit these lists.
    private static readonly string[] GenAiAttributes =
    [
        "gen_ai.provider.name",
        "gen_ai.operation.name",
        "gen_ai.request.model",
        "gen_ai.request.temperature",
        "gen_ai.request.max_tokens",
        "gen_ai.request.top_p",
        "gen_ai.request.top_k",
        "gen_ai.request.stop_sequences",
        "gen_ai.request.frequency_penalty",
        "gen_ai.request.presence_penalty",
        "gen_ai.request.choice.count",
        "gen_ai.request.seed",
        "gen_ai.request.encoding_formats",
        "gen_ai.response.model",
        "gen_ai.response.finish_reasons",
        "gen_ai.response.id",
        "gen_ai.usage.input_tokens",
        "gen_ai.usage.output_tokens",
        "gen_ai.usage.cache_read.input_tokens",
        "gen_ai.usage.cache_creation.input_tokens",
        "gen_ai.token.type",
        "gen_ai.tool.name",
        "gen_ai.tool.call.id",
        "gen_ai.tool.description",
        "gen_ai.tool.type",
        "gen_ai.tool.call.arguments",
        "gen_ai.tool.call.result",
        "gen_ai.tool.definitions",
        "gen_ai.input.messages",
        "gen_ai.output.messages",
        "gen_ai.output.type",
        "gen_ai.system_instructions",
        "gen_ai.agent.version",
        "gen_ai.conversation.id",
        "gen_ai.prompt.name",
        "gen_ai.embeddings.dimension.count",
        "gen_ai.evaluation.name",
        "gen_ai.evaluation.score.value",
        "gen_ai.evaluation.score.label",
        "gen_ai.evaluation.explanation",
        "gen_ai.data_source.id"
    ];

    private static readonly string[] DbAttributes =
    [
        "db.system.name",
        "db.operation.name",
        "db.query.text",
        "db.query.summary",
        "db.namespace",
        "db.collection.name",
        "db.response.status_code",
        "db.response.returned_rows",
        "db.client.connection.pool.name",
        "db.client.connection.state",
        "db.operation.batch.size",
        "db.stored_procedure.name"
    ];

    /// <summary>
    ///     Emits DomainContracts.g.cs to the instrumentation generator and collector.
    /// </summary>
    public static void Generate(
        AbsolutePath instrumentationGeneratorDir,
        AbsolutePath collectorObserveDir,
        GenerationGuard guard)
    {
        var domains = BuildDomains();
        Log.Information("Emitting {Count} domain contract(s)", domains.Count);

        var content = EmitDomainContracts(domains);

        var generatorDest = instrumentationGeneratorDir / "Generated" / "DomainContracts.g.cs";
        var collectorDest = collectorObserveDir / "Generated" / "DomainContracts.g.cs";

        guard.WriteIfAllowed(generatorDest, content, "DomainContracts.g.cs → qyl.instrumentation.generators");
        guard.WriteIfAllowed(collectorDest, content, "DomainContracts.g.cs → qyl.collector/Observe");
    }

    private static List<DomainSpec> BuildDomains() =>
    [
        new("gen_ai", "qyl.genai", ["traces", "metrics"],
            BuildAttributeSpecs(GenAiAttributes,
                ["gen_ai.operation.name", "gen_ai.provider.name", "gen_ai.request.model"]),
            [
                new MetricSpec("gen_ai.client.token.usage", "histogram", "token"),
                new MetricSpec("gen_ai.client.operation.duration", "histogram", "s")
            ]),
        new("db", "qyl.db", ["traces"],
            BuildAttributeSpecs(DbAttributes, ["db.system.name", "db.operation.name"]),
            []),
        new("traced", "qyl.traced", ["traces"], [], []),
        new("agent", "qyl.agent", ["traces", "metrics"],
            [
                new AttributeSpec("gen_ai.agent.name", "string", false),
                new AttributeSpec("gen_ai.operation.name", "string", true)
            ],
            [])
    ];

    private static List<AttributeSpec> BuildAttributeSpecs(string[] names, string[] requiredNames) =>
        [.. names.Select(n => new AttributeSpec(n, InferType(n), requiredNames.Contains(n)))];

    private static string InferType(string attributeName)
    {
        var suffix = attributeName.Split('.')[^1];

        if (suffix is "tokens" or "count" or "size" or "max_tokens" or "returned_rows"
            || suffix.EndsWith("_tokens", System.StringComparison.Ordinal)
            || suffix.EndsWith("_count", System.StringComparison.Ordinal)
            || suffix.EndsWith("_size", System.StringComparison.Ordinal))
            return "int";

        if (suffix is "temperature" or "top_p" or "top_k"
            || suffix.EndsWith("_penalty", System.StringComparison.Ordinal)
            || attributeName.EndsWith("score.value", System.StringComparison.Ordinal))
            return "double";

        if (suffix.EndsWith("_reasons", System.StringComparison.Ordinal)
            || suffix.EndsWith("_sequences", System.StringComparison.Ordinal)
            || suffix.EndsWith("_formats", System.StringComparison.Ordinal)
            || attributeName.EndsWith("input.messages", System.StringComparison.Ordinal)
            || attributeName.EndsWith("output.messages", System.StringComparison.Ordinal))
            return "string[]";

        return "string";
    }

    // ── C# source emission ────────────────────────────────────────────────────

    private static string EmitDomainContracts(List<DomainSpec> domains)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by eng/build/ContractGenerator.cs — do not edit manually.");
        sb.AppendLine("// Source: eng/semconv/qyl-extensions.json");
        sb.AppendLine(CultureInfo.InvariantCulture, $"// Schema: {SchemaVersion}");
        sb.AppendLine();
        sb.AppendLine("namespace Qyl.Contracts.Generated;");
        sb.AppendLine();

        // AttributeDef
        sb.AppendLine("internal readonly record struct AttributeDef(");
        sb.AppendLine("    string Name,");
        sb.AppendLine("    string Type,");
        sb.AppendLine("    bool Required);");
        sb.AppendLine();

        // MetricDef
        sb.AppendLine("internal readonly record struct MetricDef(");
        sb.AppendLine("    string Name,");
        sb.AppendLine("    string Instrument,");
        sb.AppendLine("    string Unit);");
        sb.AppendLine();

        // DomainDef
        sb.AppendLine("internal readonly record struct DomainDef(");
        sb.AppendLine("    string Name,");
        sb.AppendLine("    string Source,");
        sb.AppendLine("    string[] Signals,");
        sb.AppendLine("    AttributeDef[] TraceAttributes,");
        sb.AppendLine("    MetricDef[] MetricInstruments,");
        sb.AppendLine("    string SchemaVersion);");
        sb.AppendLine();

        // DomainContracts static class
        sb.AppendLine("internal static class DomainContracts");
        sb.AppendLine("{");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    internal const string SchemaVersion = \"{SchemaVersion}\";");
        sb.AppendLine();

        foreach (var domain in domains)
        {
            var fieldName = ToPascalCase(domain.Name);
            sb.AppendLine(CultureInfo.InvariantCulture, $"    internal static readonly DomainDef {fieldName} = new(");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        Name:     \"{domain.Name}\",");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        Source:   \"{domain.Source}\",");
            sb.Append(CultureInfo.InvariantCulture, $"        Signals:  [");
            sb.Append(string.Join(", ", domain.Signals.Select(static s => $"\"{s}\"")));
            sb.AppendLine("],");

            // TraceAttributes
            if (domain.TraceAttributes.Count == 0)
            {
                sb.AppendLine("        TraceAttributes: [],");
            }
            else
            {
                sb.AppendLine("        TraceAttributes:");
                sb.AppendLine("        [");
                foreach (var attr in domain.TraceAttributes)
                {
                    var req = attr.Required ? "Required: true" : "Required: false";
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"            new(\"{attr.Name}\", \"{attr.Type}\", {req}),");
                }

                sb.AppendLine("        ],");
            }

            // MetricInstruments
            if (domain.MetricInstruments.Count == 0)
            {
                sb.AppendLine("        MetricInstruments: [],");
            }
            else
            {
                sb.AppendLine("        MetricInstruments:");
                sb.AppendLine("        [");
                foreach (var metric in domain.MetricInstruments)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"            new(\"{metric.Name}\", \"{metric.Instrument}\", \"{metric.Unit}\"),");
                }

                sb.AppendLine("        ],");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"        SchemaVersion: \"{SchemaVersion}\");");
            sb.AppendLine();
        }

        // All array
        sb.Append("    internal static readonly DomainDef[] All = [");
        sb.Append(string.Join(", ", domains.Select(static d => ToPascalCase(d.Name))));
        sb.AppendLine("];");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string ToPascalCase(string name)
    {
        // "gen_ai" → "GenAi", "db" → "Db", "traced" → "Traced", "agent" → "Agent"
        var sb = new StringBuilder();
        var nextUpper = true;
        foreach (var ch in name)
        {
            if (ch == '_' || ch == '.')
            {
                nextUpper = true;
            }
            else
            {
                sb.Append(nextUpper ? char.ToUpperInvariant(ch) : ch);
                nextUpper = false;
            }
        }

        return sb.ToString();
    }

    // ── Internal data model ───────────────────────────────────────────────────

    private sealed record DomainSpec(
        string Name,
        string Source,
        string[] Signals,
        List<AttributeSpec> TraceAttributes,
        List<MetricSpec> MetricInstruments);

    private sealed record AttributeSpec(string Name, string Type, bool Required);

    private sealed record MetricSpec(string Name, string Instrument, string Unit);
}
