// eng/build/ContractGenerator.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Nuke.Common.IO;
using Serilog;

/// <summary>
///     Generates DomainContracts.g.cs from qyl-extensions.json into both generator projects.
///     Single entry point: <see cref="Generate"/>.
/// </summary>
public static class ContractGenerator
{
    private const string SchemaVersion = "semconv-1.40.0";

    /// <summary>
    ///     Reads qyl-extensions.json, emits DomainContracts.g.cs to both generator destinations.
    /// </summary>
    public static void Generate(
        AbsolutePath extensionsJsonPath,
        AbsolutePath serviceDefaultsGeneratorDir,
        AbsolutePath instrumentationGeneratorsDir,
        GenerationGuard guard)
    {
        if (!extensionsJsonPath.FileExists())
        {
            Log.Error("qyl-extensions.json not found at {Path}", extensionsJsonPath);
            throw new FileNotFoundException("qyl-extensions.json not found", extensionsJsonPath);
        }

        var domains = LoadDomains(extensionsJsonPath);
        Log.Information("Loaded {Count} domain(s) from qyl-extensions.json", domains.Count);

        var content = EmitDomainContracts(domains);

        var dest1 = serviceDefaultsGeneratorDir / "Generated" / "DomainContracts.g.cs";
        var dest2 = instrumentationGeneratorsDir / "Generated" / "DomainContracts.g.cs";

        guard.WriteIfAllowed(dest1, content, "DomainContracts.g.cs → servicedefaults.generator");
        guard.WriteIfAllowed(dest2, content, "DomainContracts.g.cs → instrumentation.generators");

        Log.Information("DomainContracts.g.cs written to 2 destination(s)");
    }

    // ── Domain loading ────────────────────────────────────────────────────────

    private static List<DomainSpec> LoadDomains(AbsolutePath extensionsJsonPath)
    {
        using var stream = File.OpenRead(extensionsJsonPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var domains = new List<DomainSpec>();

        // gen_ai domain — lookup by upstreamPrefix, not positional index
        var genAiFacade = FindFacade(root, "gen_ai");
        domains.Add(new DomainSpec(
            Name:     "gen_ai",
            Source:   "qyl.genai",
            Signals:  ["traces", "metrics"],
            TraceAttributes: ExtractAttributes(genAiFacade, requiredNames: ["gen_ai.operation.name", "gen_ai.provider.name", "gen_ai.request.model"]),
            MetricInstruments:
            [
                new MetricSpec("gen_ai.client.token.usage",       "histogram", "token"),
                new MetricSpec("gen_ai.client.operation.duration", "histogram", "s"),
            ]));

        // db domain — lookup by upstreamPrefix, not positional index
        var dbFacade = FindFacade(root, "db");
        domains.Add(new DomainSpec(
            Name:    "db",
            Source:  "qyl.db",
            Signals: ["traces"],
            TraceAttributes: ExtractAttributes(dbFacade, requiredNames: ["db.system.name", "db.operation.name"]),
            MetricInstruments: []));

        // traced domain — open schema, no fixed attributes
        domains.Add(new DomainSpec(
            Name:    "traced",
            Source:  "qyl.traced",
            Signals: ["traces"],
            TraceAttributes: [],
            MetricInstruments: []));

        // agent domain — subset of gen_ai attributes
        domains.Add(new DomainSpec(
            Name:    "agent",
            Source:  "qyl.agent",
            Signals: ["traces", "metrics"],
            TraceAttributes:
            [
                new AttributeSpec("gen_ai.agent.name",      "string", Required: false),
                new AttributeSpec("gen_ai.operation.name",  "string", Required: true),
            ],
            MetricInstruments: []));

        return domains;
    }

    private static JsonElement FindFacade(JsonElement root, string upstreamPrefix)
    {
        foreach (var facade in root.GetProperty("facades").EnumerateArray())
        {
            if (string.Equals(facade.GetProperty("upstreamPrefix").GetString(),
                    upstreamPrefix, StringComparison.Ordinal))
                return facade;
        }
        throw new InvalidOperationException(
            $"Facade with upstreamPrefix '{upstreamPrefix}' not found in qyl-extensions.json");
    }

    private static List<AttributeSpec> ExtractAttributes(
        JsonElement facade,
        string[] requiredNames)
    {
        var attrs = new List<AttributeSpec>();

        foreach (var nameElem in facade.GetProperty("attributes").EnumerateArray())
        {
            var name = nameElem.GetString()!;
            var type = InferType(name);
            var required = requiredNames.Contains(name);
            attrs.Add(new AttributeSpec(name, type, required));
        }

        return attrs;
    }

    private static string InferType(string attributeName)
    {
        var suffix = attributeName.Split('.')[^1];

        if (suffix is "tokens" || attributeName.EndsWith("_tokens", StringComparison.Ordinal)
                                || attributeName.EndsWith("_count", StringComparison.Ordinal)
                                || attributeName.EndsWith("_size", StringComparison.Ordinal)
                                || attributeName.EndsWith("max_tokens", StringComparison.Ordinal)
                                || attributeName.EndsWith("returned_rows", StringComparison.Ordinal)
                                || attributeName.EndsWith("batch.size", StringComparison.Ordinal))
            return "int";

        if (attributeName.EndsWith("_temperature", StringComparison.Ordinal)
         || attributeName.EndsWith("_top_p", StringComparison.Ordinal)
         || attributeName.EndsWith("_top_k", StringComparison.Ordinal)
         || attributeName.EndsWith("_penalty", StringComparison.Ordinal)
         || attributeName.EndsWith("score.value", StringComparison.Ordinal))
            return "double";

        if (attributeName.EndsWith("_reasons", StringComparison.Ordinal)
         || attributeName.EndsWith("_sequences", StringComparison.Ordinal)
         || attributeName.EndsWith("_formats", StringComparison.Ordinal)
         || attributeName.EndsWith("input.messages", StringComparison.Ordinal)
         || attributeName.EndsWith("output.messages", StringComparison.Ordinal))
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
        sb.AppendLine("namespace qyl.Contracts;");
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
                    sb.AppendLine(CultureInfo.InvariantCulture, $"            new(\"{attr.Name}\", \"{attr.Type}\", {req}),");
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
                    sb.AppendLine(CultureInfo.InvariantCulture, $"            new(\"{metric.Name}\", \"{metric.Instrument}\", \"{metric.Unit}\"),");
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
