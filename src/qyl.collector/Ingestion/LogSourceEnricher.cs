namespace qyl.collector.Ingestion;

public sealed class LogSourceEnricher
{
    private readonly SourceLocationCache _cache;
    private readonly PdbSourceResolver _pdbResolver;

    public LogSourceEnricher(SourceLocationCache cache, PdbSourceResolver pdbResolver)
    {
        _cache = cache;
        _pdbResolver = pdbResolver;
    }

    public SourceLocation? Enrich(OtlpLogRecord log)
    {
        var attrs = ToAttributeMap(log.Attributes);

        var file = attrs.GetValueOrDefault("code.file.path");
        var method = attrs.GetValueOrDefault("code.function.name");
        var line = ParseInt(attrs.GetValueOrDefault("code.line.number"));
        var column = ParseInt(attrs.GetValueOrDefault("code.column.number"));

        if (!string.IsNullOrWhiteSpace(file) || line.HasValue || column.HasValue || !string.IsNullOrWhiteSpace(method))
            return new SourceLocation(file, line, column, method);

        // Fallback to stacktrace parsing first.
        var stackTrace = attrs.GetValueOrDefault("exception.stacktrace")
            ?? attrs.GetValueOrDefault("code.stacktrace");

        if (!string.IsNullOrWhiteSpace(stackTrace))
        {
            var key = $"stack:{stackTrace.GetHashCode(StringComparison.Ordinal)}";
            return _cache.GetOrAdd(key, () => _pdbResolver.ResolveFromStackTrace(stackTrace));
        }

        if (string.IsNullOrWhiteSpace(method))
            return null;

        var methodKey = $"method:{method}";
        return _cache.GetOrAdd(methodKey, () => _pdbResolver.ResolveFromCurrentMethod(method));
    }

    private static Dictionary<string, string?> ToAttributeMap(IReadOnlyList<OtlpKeyValue>? attributes)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (attributes is null)
            return map;

        foreach (var kv in attributes)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            var key = SchemaNormalizer.Normalize(kv.Key);
            map[key] = kv.Value?.StringValue
                ?? kv.Value?.IntValue?.ToString(CultureInfo.InvariantCulture)
                ?? kv.Value?.DoubleValue?.ToString(CultureInfo.InvariantCulture)
                ?? kv.Value?.BoolValue?.ToString();
        }

        return map;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
