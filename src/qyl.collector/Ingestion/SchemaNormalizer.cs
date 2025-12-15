namespace qyl.collector.Ingestion;

public static class SchemaVersion
{
    public const string Version = "1.38.0";
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.38.0";
}

public sealed class SchemaNormalizer
{
    private static readonly Dictionary<string, string> _deprecatedAttributes = new(StringComparer.Ordinal)
    {
        ["gen_ai.system"] = "gen_ai.provider.name",
        ["gen_ai.prompt"] = "gen_ai.input.messages",
        ["gen_ai.completion"] = "gen_ai.output.messages",
        ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
        ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",

        ["code.function"] = "code.function.name",
        ["code.filepath"] = "code.file.path",
        ["code.lineno"] = "code.line.number",

        ["db.system"] = "db.system.name",

        ["gen_ai.openai.request.seed"] = "gen_ai.request.seed"
    };

    public Dictionary<string, object?> NormalizeAttributes(IDictionary<string, object?> attributes)
    {
        Throw.Throw.IfNull(attributes);

        var result = new Dictionary<string, object?>(attributes.Count, StringComparer.Ordinal);

        foreach (var (key, value) in attributes)
        {
            var normalizedKey = Normalize(key);
            result[normalizedKey] = value;
        }

        return result;
    }

    public static string Normalize(string attributeName)
    {
        return _deprecatedAttributes.GetValueOrDefault(attributeName, attributeName);
    }


    public bool IsDeprecated(string attributeName)
    {
        return _deprecatedAttributes.ContainsKey(attributeName);
    }

    public IReadOnlyDictionary<string, string> GetDeprecatedMappings()
    {
        return _deprecatedAttributes;
    }
}