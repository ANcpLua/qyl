using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Context;

#pragma warning disable CA1305, CA1863 // Build-time code generators use invariant formatting

namespace Domain.CodeGen.Generators;

/// <summary>
///     Generates comprehensive OTel GenAI Semantic Conventions v1.38 code.
///     Produces:
///     - String constants for attribute keys
///     - UTF-8 byte spans for zero-allocation parsing
///     - Well-known value classes with constants
///     - SearchValues&lt;string&gt; for efficient prefix matching
///     - Metric and Event name constants
///     - Migration dictionaries
/// </summary>
public sealed class OTelSemconvGenerator : IGenerator
{
    const string GeneratorName = nameof(OTelSemconvGenerator);

    public string Name => "OTelSemconv";

    public FrozenDictionary<string, string> Generate(QylSchema schema, BuildPaths paths, string rootNamespace)
    {
        var outputs = new Dictionary<string, string>();

        // Main attributes file
        const string mainPath = "Telemetry/GenAiSemconv.g.cs";
        outputs[mainPath] = EmitMainFile(rootNamespace, mainPath);

        // UTF-8 attribute keys
        const string utf8Path = "Telemetry/GenAiSemconvUtf8.g.cs";
        outputs[utf8Path] = EmitUtf8File(rootNamespace, utf8Path);

        // Well-known values
        const string valuesPath = "Telemetry/GenAiSemconvValues.g.cs";
        outputs[valuesPath] = EmitValuesFile(rootNamespace, valuesPath);

        // Metrics and events
        const string metricsPath = "Telemetry/GenAiSemconvMetrics.g.cs";
        outputs[metricsPath] = EmitMetricsFile(rootNamespace, metricsPath);

        // Extensions with SearchValues
        const string extensionsPath = "Telemetry/GenAiSemconvExtensions.g.cs";
        outputs[extensionsPath] = EmitExtensionsFile(rootNamespace, extensionsPath);

        return outputs.ToFrozenDictionary();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Main Attributes File
    // ════════════════════════════════════════════════════════════════════════

    static string EmitMainFile(string rootNamespace, string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Telemetry;");
        sb.AppendLine();

        // Version info
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// OpenTelemetry GenAI Semantic Conventions v1.38.0 - Attribute Keys");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// Schema URL: https://opentelemetry.io/schemas/1.38.0");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine("public static partial class GenAiSemconv");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Semantic conventions version.</summary>");
        sb.AppendLine("    public const string Version = \"1.38.0\";");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Schema URL for telemetry SDK configuration.</summary>");
        sb.AppendLine("    public const string SchemaUrl = \"https://opentelemetry.io/schemas/1.38.0\";");
        sb.AppendLine();

        // Group attributes by prefix
        var groups = OTelGenAiSemconv.Attributes.Values
            .Where(a => !a.IsDeprecated)
            .GroupBy(a => a.Group ?? "other")
            .OrderBy(g => GetGroupOrder(g.Key));

        foreach (var group in groups)
        {
            sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
            sb.AppendLine($"    // {group.Key.ToUpperInvariant()}");
            sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
            sb.AppendLine();

            foreach (var attr in group.OrderBy(a => a.Key))
            {
                sb.AppendLine($"    /// <summary>{attr.Description}</summary>");
                sb.AppendLine($"    public const string {attr.ConstantName} = \"{attr.Key}\";");
                sb.AppendLine();
            }
        }

        // Deprecated attributes in separate region
        sb.AppendLine("    #region Deprecated");
        sb.AppendLine();

        foreach (var attr in OTelGenAiSemconv.Attributes.Values.Where(a => a.IsDeprecated).OrderBy(a => a.Key))
        {
            sb.AppendLine($"    /// <summary>{attr.Description}</summary>");
            if (attr.ReplacedBy is not null)
                sb.AppendLine($"    [global::System.Obsolete(\"Use {attr.ReplacedBy} instead\")]");
            sb.AppendLine($"    public const string {attr.ConstantName} = \"{attr.Key}\";");
            sb.AppendLine();
        }

        sb.AppendLine("    #endregion");
        sb.AppendLine();

        // AllKeys FrozenSet
        sb.AppendLine("    /// <summary>All valid (non-deprecated) gen_ai.* attribute keys.</summary>");
        sb.AppendLine("    public static FrozenSet<string> AllKeys { get; } = new HashSet<string>");
        sb.AppendLine("    {");
        foreach (var key in OTelGenAiSemconv.Attributes.Values
                     .Where(a => !a.IsDeprecated)
                     .Select(a => a.Key)
                     .OrderBy(k => k))
            sb.AppendLine($"        \"{key}\",");
        sb.AppendLine("    }.ToFrozenSet(StringComparer.Ordinal);");
        sb.AppendLine();

        // Migration dictionary
        sb.AppendLine("    /// <summary>Mapping from deprecated to current attribute keys.</summary>");
        sb.AppendLine(
            "    public static FrozenDictionary<string, string> Migrations { get; } = new Dictionary<string, string>");
        sb.AppendLine("    {");
        foreach (var attr in OTelGenAiSemconv.Attributes.Values
                     .Where(a => a.IsDeprecated && a.ReplacedBy is not null)
                     .OrderBy(a => a.Key))
            sb.AppendLine($"        [\"{attr.Key}\"] = \"{attr.ReplacedBy}\",");
        sb.AppendLine("    }.ToFrozenDictionary(StringComparer.Ordinal);");
        sb.AppendLine();

        // Normalize helper
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Normalizes a potentially deprecated attribute key to its current equivalent.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string Normalize(string key)");
        sb.AppendLine("        => Migrations.TryGetValue(key, out var replacement) ? replacement : key;");

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // UTF-8 Byte Span File
    // ════════════════════════════════════════════════════════════════════════

    static string EmitUtf8File(string rootNamespace, string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Telemetry;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// UTF-8 encoded attribute keys for zero-allocation JSON parsing.");
        sb.AppendLine("/// Use with System.Text.Json Utf8JsonReader for high-performance parsing.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GenAiSemconvUtf8");
        sb.AppendLine("{");

        // Non-deprecated attributes only
        foreach (var attr in OTelGenAiSemconv.Attributes.Values
                     .Where(a => !a.IsDeprecated)
                     .OrderBy(a => a.Key))
        {
            sb.AppendLine($"    /// <summary>{attr.Key}</summary>");
            sb.AppendLine($"    public static ReadOnlySpan<byte> {attr.ConstantName} => \"{attr.Key}\"u8;");
            sb.AppendLine();
        }

        // Deprecated for backward-compat parsing
        sb.AppendLine("    #region Deprecated (for backward-compatible parsing)");
        sb.AppendLine();

        foreach (var attr in OTelGenAiSemconv.Attributes.Values
                     .Where(a => a.IsDeprecated)
                     .OrderBy(a => a.Key))
        {
            sb.AppendLine($"    /// <summary>{attr.Key} - DEPRECATED</summary>");
            sb.AppendLine($"    public static ReadOnlySpan<byte> {attr.ConstantName} => \"{attr.Key}\"u8;");
            sb.AppendLine();
        }

        sb.AppendLine("    #endregion");

        // Prefix spans for StartsWith checks
        sb.AppendLine();
        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine("    // PREFIX PATTERNS");
        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>gen_ai. prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixGenAi => \"gen_ai.\"u8;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>gen_ai.agent. prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixAgent => \"gen_ai.agent.\"u8;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>gen_ai.tool. prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixTool => \"gen_ai.tool.\"u8;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>gen_ai.request. prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixRequest => \"gen_ai.request.\"u8;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>gen_ai.response. prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixResponse => \"gen_ai.response.\"u8;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>gen_ai.usage. prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixUsage => \"gen_ai.usage.\"u8;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>gen_ai.evaluation. prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixEvaluation => \"gen_ai.evaluation.\"u8;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>agents. legacy prefix</summary>");
        sb.AppendLine("    public static ReadOnlySpan<byte> PrefixLegacyAgents => \"agents.\"u8;");

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Well-Known Values File
    // ════════════════════════════════════════════════════════════════════════

    static string EmitValuesFile(string rootNamespace, string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Telemetry;");
        sb.AppendLine();

        foreach (var (attrKey, values) in OTelGenAiSemconv.WellKnownValues.OrderBy(kv => kv.Key))
        {
            var className = KeyToClassName(attrKey);

            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Well-known values for {attrKey}");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public static class {className}");
            sb.AppendLine("{");

            foreach (var value in values)
            {
                sb.AppendLine($"    /// <summary>{value.Description}</summary>");
                sb.AppendLine($"    public const string {value.ConstantName} = \"{value.Value}\";");
                sb.AppendLine();
            }

            // All values as ImmutableArray
            sb.AppendLine("    /// <summary>All well-known values.</summary>");
            sb.AppendLine("    public static ImmutableArray<string> All { get; } = [");
            foreach (var value in values) sb.AppendLine($"        {value.ConstantName},");
            sb.AppendLine("    ];");
            sb.AppendLine();

            // FrozenSet for validation
            sb.AppendLine("    /// <summary>FrozenSet for O(1) validation.</summary>");
            sb.AppendLine(
                "    public static FrozenSet<string> ValidValues { get; } = All.ToFrozenSet(StringComparer.Ordinal);");
            sb.AppendLine();

            // IsValid helper
            sb.AppendLine("    /// <summary>Checks if the value is a well-known value.</summary>");
            sb.AppendLine(
                "    public static bool IsValid(string? value) => value is not null && ValidValues.Contains(value);");

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Metrics and Events File
    // ════════════════════════════════════════════════════════════════════════

    static string EmitMetricsFile(string rootNamespace, string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine($"namespace {rootNamespace}.Telemetry;");
        sb.AppendLine();

        // Metrics
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// GenAI metric instrument names and units.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GenAiMetrics");
        sb.AppendLine("{");

        foreach (var metric in OTelGenAiSemconv.Metrics)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {metric.Description}");
            sb.AppendLine($"    /// <para>Type: {metric.InstrumentType}, Unit: {metric.Unit}</para>");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public const string {metric.ConstantName} = \"{metric.Name}\";");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Metric units
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// GenAI metric units (UCUM).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GenAiMetricUnits");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Token count unit.</summary>");
        sb.AppendLine("    public const string Token = \"{token}\";");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Duration in seconds.</summary>");
        sb.AppendLine("    public const string Second = \"s\";");
        sb.AppendLine("}");
        sb.AppendLine();

        // Events
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// GenAI event names.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GenAiEvents");
        sb.AppendLine("{");

        foreach (var evt in OTelGenAiSemconv.Events)
        {
            sb.AppendLine($"    /// <summary>{evt.Description}</summary>");
            sb.AppendLine($"    public const string {evt.ConstantName} = \"{evt.Name}\";");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Extensions with SearchValues<string>
    // ════════════════════════════════════════════════════════════════════════

    static string EmitExtensionsFile(string rootNamespace, string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Buffers;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Telemetry;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// High-performance extension methods for GenAI attribute key matching.");
        sb.AppendLine("/// Uses SearchValues&lt;string&gt; for efficient multi-prefix checks.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GenAiSemconvExtensions");
        sb.AppendLine("{");

        // SearchValues instances
        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine("    // SearchValues<string> for efficient prefix matching (.NET 10+)");
        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>SearchValues for agent and tool prefixes.</summary>");
        sb.AppendLine("    private static readonly SearchValues<string> AgentToolPrefixes =");
        sb.AppendLine("        SearchValues.Create([\"gen_ai.agent.\", \"gen_ai.tool.\"], StringComparison.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>SearchValues for all gen_ai.* sub-namespaces.</summary>");
        sb.AppendLine("    private static readonly SearchValues<string> GenAiSubPrefixes =");
        sb.AppendLine("        SearchValues.Create([");
        sb.AppendLine("            \"gen_ai.agent.\",");
        sb.AppendLine("            \"gen_ai.tool.\",");
        sb.AppendLine("            \"gen_ai.request.\",");
        sb.AppendLine("            \"gen_ai.response.\",");
        sb.AppendLine("            \"gen_ai.usage.\",");
        sb.AppendLine("            \"gen_ai.evaluation.\",");
        sb.AppendLine("            \"gen_ai.embeddings.\",");
        sb.AppendLine("            \"gen_ai.data_source.\",");
        sb.AppendLine("            \"gen_ai.input.\",");
        sb.AppendLine("            \"gen_ai.output.\",");
        sb.AppendLine("        ], StringComparison.Ordinal);");
        sb.AppendLine();

        // Extension methods
        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine("    // STRING EXTENSION METHODS");
        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the key starts with \"gen_ai.\" prefix.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsGenAiAttribute(this string key)");
        sb.AppendLine("        => key.StartsWith(\"gen_ai.\", StringComparison.Ordinal);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the key is an agent or tool attribute using SearchValues.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsAgentOrToolAttribute(this string key)");
        sb.AppendLine("        => AgentToolPrefixes.ContainsAny(key);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the key matches any gen_ai.* sub-namespace using SearchValues.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsGenAiSubNamespace(this string key)");
        sb.AppendLine("        => GenAiSubPrefixes.ContainsAny(key);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the key is a deprecated attribute that needs migration.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsDeprecatedAttribute(this string key)");
        sb.AppendLine("        => GenAiSemconv.Migrations.ContainsKey(key);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the key is a legacy agents.* prefix attribute.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsLegacyAgentsAttribute(this string key)");
        sb.AppendLine("        => key.StartsWith(\"agents.\", StringComparison.Ordinal);");
        sb.AppendLine();

        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine("    // READONLYSPAN<BYTE> EXTENSION METHODS (for Utf8JsonReader)");
        sb.AppendLine("    // ─────────────────────────────────────────────────────────────────────");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the UTF-8 key starts with \"gen_ai.\" prefix.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsGenAiAttribute(this ReadOnlySpan<byte> key)");
        sb.AppendLine("        => key.Length >= 7 && key[..7].SequenceEqual(GenAiSemconvUtf8.PrefixGenAi);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the UTF-8 key is an agent or tool attribute.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsAgentOrToolAttribute(this ReadOnlySpan<byte> key)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (key.Length < 13) return false;");
        sb.AppendLine("        return key.StartsWith(GenAiSemconvUtf8.PrefixAgent) ||");
        sb.AppendLine("               key.StartsWith(GenAiSemconvUtf8.PrefixTool);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if the UTF-8 key starts with \"agents.\" legacy prefix.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool IsLegacyAgentsAttribute(this ReadOnlySpan<byte> key)");
        sb.AppendLine("        => key.Length >= 7 && key.StartsWith(GenAiSemconvUtf8.PrefixLegacyAgents);");

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    static int GetGroupOrder(string group) => group switch
    {
        "core" => 0,
        "request" => 1,
        "response" => 2,
        "usage" => 3,
        "output" => 4,
        "conversation" => 5,
        "agent" => 6,
        "tool" => 7,
        "data_source" => 8,
        "content" => 9,
        "embeddings" => 10,
        "evaluation" => 11,
        "server" => 12,
        "deprecated" => 99,
        _ => 50
    };

    static string KeyToClassName(string key)
    {
        // gen_ai.operation.name -> GenAiOperationNames
        // gen_ai.provider.name -> GenAiProviderNames
        // error.type -> ErrorTypes

        var parts = key.Split('.');
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length is 0) continue;

            var words = part.Split('_');
            foreach (var word in words)
            {
                if (word.Length is 0) continue;
                sb.Append(char.ToUpperInvariant(word[0]));
                sb.Append(word[1..]);
            }
        }

        // Pluralize the last part
        sb.Append('s');

        return sb.ToString();
    }
}