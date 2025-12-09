// qyl.collector - Schema Normalizer
// Normalizes telemetry attributes to OpenTelemetry Semantic Conventions v1.38

namespace qyl.collector.Ingestion;

/// <summary>
/// Schema version constant for OpenTelemetry Semantic Conventions v1.38.0.
/// </summary>
public static class SchemaVersion
{
    public const string Version = "1.38.0";
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";
}

/// <summary>
/// Normalizes semantic convention attributes to v1.38.
/// Maps deprecated attribute names to their current equivalents.
/// </summary>
public sealed class SchemaNormalizer
{
    /// <summary>
    /// Deprecated attribute names mapped to their v1.38 equivalents.
    /// </summary>
    private static readonly Dictionary<string, string> DeprecatedAttributes = new(StringComparer.Ordinal)
    {
        // GenAI deprecated in v1.37
        ["gen_ai.system"] = "gen_ai.provider.name",
        ["gen_ai.prompt"] = "gen_ai.input.messages",
        ["gen_ai.completion"] = "gen_ai.output.messages",
        ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
        ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",

        // Code attributes deprecated in v1.30
        ["code.function"] = "code.function.name",
        ["code.filepath"] = "code.file.path",
        ["code.lineno"] = "code.line.number",

        // DB attributes deprecated in v1.30
        ["db.system"] = "db.system.name",

        // OpenAI-specific deprecated in v1.30
        ["gen_ai.openai.request.seed"] = "gen_ai.request.seed",
    };

    /// <summary>
    /// Normalizes an attribute name to v1.38 conventions.
    /// Returns the input unchanged if no mapping exists.
    /// </summary>
    public string Normalize(string attributeName)
    {
        return DeprecatedAttributes.TryGetValue(attributeName, out var normalized)
            ? normalized
            : attributeName;
    }

    /// <summary>
    /// Normalizes all attributes in a dictionary to v1.38 conventions.
    /// </summary>
    public Dictionary<string, object?> NormalizeAttributes(IDictionary<string, object?> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var result = new Dictionary<string, object?>(attributes.Count, StringComparer.Ordinal);

        foreach (var (key, value) in attributes)
        {
            var normalizedKey = Normalize(key);
            result[normalizedKey] = value;
        }

        return result;
    }

    /// <summary>
    /// Checks if an attribute name is deprecated.
    /// </summary>
    public bool IsDeprecated(string attributeName)
    {
        return DeprecatedAttributes.ContainsKey(attributeName);
    }

    /// <summary>
    /// Gets all deprecated attribute mappings.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetDeprecatedMappings() => DeprecatedAttributes;
}
