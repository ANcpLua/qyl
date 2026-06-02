using ProtoAnyValue = OpenTelemetry.Proto.Common.V1.AnyValue;
using ProtoKeyValue = OpenTelemetry.Proto.Common.V1.KeyValue;

namespace Qyl.Collector.Ingestion;

internal sealed class LogSourceEnricher
{
    private readonly SourceLocationCache _cache;

    public LogSourceEnricher(SourceLocationCache cache) => _cache = cache;

    public SourceLocation? Enrich(IEnumerable<ProtoKeyValue> attributes)
    {
        var attrs = ToAttributeMap(attributes);

        var file = attrs.GetValueOrDefault(SemanticAttributeKeys.CodeFilePath);
        var method = attrs.GetValueOrDefault(SemanticAttributeKeys.CodeFunctionName);
        var line = ParseInt(attrs.GetValueOrDefault(SemanticAttributeKeys.CodeLineNumber));
        var column = ParseInt(attrs.GetValueOrDefault(SemanticAttributeKeys.CodeColumnNumber));

        if (!string.IsNullOrWhiteSpace(file) || line.HasValue || column.HasValue || !string.IsNullOrWhiteSpace(method))
            return new SourceLocation(file, line, column, method);

        var stackTrace = attrs.GetValueOrDefault(SemanticAttributeKeys.ExceptionStacktrace)
                         ?? attrs.GetValueOrDefault(SemanticAttributeKeys.CodeStacktrace);

        if (!string.IsNullOrWhiteSpace(stackTrace))
        {
            var key = $"stack:{stackTrace.GetHashCode(StringComparison.Ordinal)}";
            return _cache.GetOrAdd(key, () => PdbSourceResolver.ResolveFromStackTrace(stackTrace));
        }

        if (string.IsNullOrWhiteSpace(method))
            return null;

        var methodKey = $"method:{method}";
        return _cache.GetOrAdd(methodKey, () => PdbSourceResolver.ResolveFromCurrentMethod(method));
    }

    private static Dictionary<string, string?> ToAttributeMap(IEnumerable<ProtoKeyValue> attributes)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var kv in attributes)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            map[kv.Key] = kv.Value.ValueCase switch
            {
                ProtoAnyValue.ValueOneofCase.StringValue => kv.Value.StringValue,
                ProtoAnyValue.ValueOneofCase.IntValue => kv.Value.IntValue.ToString(CultureInfo.InvariantCulture),
                ProtoAnyValue.ValueOneofCase.DoubleValue => kv.Value.DoubleValue.ToString(CultureInfo.InvariantCulture),
                ProtoAnyValue.ValueOneofCase.BoolValue => kv.Value.BoolValue.ToString(),
                _ => null
            };
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
