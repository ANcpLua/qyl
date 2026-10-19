using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed class AnalysisInputException(string field, string message, string code = "analysis.invalid") : Exception(message)
{
    public string Field { get; } = field;
    public string Code { get; } = code;
    public string Description { get; } = message;
}

internal readonly record struct AnalysisWindow(DateTimeOffset Start, DateTimeOffset End)
{
    public ulong StartNs => QylTimeConversions.ToUnixNanoUnsigned(Start);
    private ulong EndNs => QylTimeConversions.ToUnixNanoUnsigned(End);
    public JsonObject Json => J.O(("start_time", Start.ToString("O")), ("end_time", End.ToString("O")));
    public bool Contains(ulong time) => time >= StartNs && time < EndNs;
    public static AnalysisWindow Resolve(JsonObject input, DateTimeOffset now, double defaultMinutes = 60)
    {
        var end = J.Date(input, "end_time") ?? now;
        var start = J.Date(input, "start_time") ?? end.AddMinutes(-J.Number(input, "lookback_minutes", defaultMinutes));
        if (start < DateTimeOffset.UnixEpoch) throw new AnalysisInputException("start_time", "start_time must be on or after the Unix epoch.");
        if (end <= start) throw new AnalysisInputException("end_time", "end_time must be after start_time.");
        return new(start, end);
    }
}

internal static partial class J
{
    public static JsonNode? N(object? value) => value switch
    {
        null => null, JsonNode node => node.DeepClone(), string v => JsonValue.Create(v), bool v => JsonValue.Create(v),
        byte v => JsonValue.Create(v), short v => JsonValue.Create(v), int v => JsonValue.Create(v), long v => JsonValue.Create(v), ulong v => JsonValue.Create(v),
        double v when double.IsFinite(v) => JsonValue.Create(v), double => null, decimal v => JsonValue.Create(v),
        IEnumerable<JsonNode?> values => new JsonArray(values.Select(static v => v?.DeepClone()).ToArray()),
        IEnumerable<string> values => new JsonArray(values.Select(static v => (JsonNode?)JsonValue.Create(v)).ToArray()),
        _ => throw new InvalidOperationException("Unsupported analysis JSON value.")
    };
    public static JsonObject O(params (string Key, object? Value)[] items)
    { var obj = new JsonObject(); foreach (var item in items) obj[item.Key] = N(item.Value); return obj; }
    public static JsonArray A(IEnumerable<JsonNode?> items) => new(items.Select(static v => v?.DeepClone()).ToArray());
    public static string? String(JsonNode? input, string name) =>
        input is JsonObject obj && obj[name] is { } value && value.GetValueKind() is JsonValueKind.String
            ? value.GetValue<string>() : null;
    public static double Number(JsonNode? input, string name, double fallback = 0) =>
        input is JsonObject obj && obj[name] is { } value && value.GetValueKind() is JsonValueKind.Number
            ? double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture) : fallback;
    public static bool Bool(JsonNode? input, string name, bool fallback = false) =>
        input is JsonObject obj && obj[name] is { } value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? value.GetValue<bool>() : fallback;
    public static string Text(JsonNode? value) => value is null ? "" : value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();
    public static DateTimeOffset? Date(JsonNode? input, string name)
    {
        if ((input as JsonObject)?[name] is null) return null;
        var text = String(input, name) ?? "";
        if (Rfc3339().Match(text) is not { Success: true } match || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
            throw new AnalysisInputException(name, name + " must be an RFC 3339 timestamp.");
        // An instant holds 100 ns. A finer boundary cannot be applied exactly, and moving it would
        // include or exclude records the request did not select.
        if (match.Groups["fraction"].Value is { Length: > 7 } fraction && fraction.AsSpan(7).ContainsAnyExcept('0'))
            throw new AnalysisInputException(name, name + " is finer than 100 nanoseconds; use at most seven fractional digits.", "time.precision_unsupported");
        if (!QylTimeConversions.TryToUnixNanoUnsigned(value, out _))
            throw new AnalysisInputException(name, name + " must be within the unsigned OTLP Unix-nanosecond range.");
        return value;
    }
    public static JsonObject Attributes(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        var parsed = JsonNode.Parse(json);
        if (parsed is JsonObject obj) { var unwrapped = new JsonObject(); foreach (var item in obj) unwrapped[item.Key] = Unwrap(item.Value); return unwrapped; }
        var result = new JsonObject();
        if (parsed is JsonArray list) foreach (var pair in list)
            if (String(pair, "key") is { } key) result[key] = Unwrap(pair?["value"]);
        return result;
    }
    private static JsonNode? Unwrap(JsonNode? value)
    {
        if (value is not JsonObject obj) return value?.DeepClone();
        if (String(obj, "type") == "int" && long.TryParse(Text(obj["value"]), CultureInfo.InvariantCulture, out var integer)) return JsonValue.Create(integer);
        if (String(obj, "type") == "double" && double.TryParse(Text(obj["value"]), CultureInfo.InvariantCulture, out var number)) return JsonValue.Create(number);
        foreach (var name in new[] { "stringValue", "string_value", "intValue", "int_value", "doubleValue", "double_value", "boolValue", "bool_value", "value" })
            if (obj[name] is { } scalar) return scalar.DeepClone();
        return obj.DeepClone();
    }
    public static string Normalize(string message) => Numbers().Replace(Quoted().Replace(Hex().Replace(Uuid().Replace(message, "#"), "#"), "#"), "#");
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.(?<fraction>\d{1,9}))?(?:Z|[+-]\d{2}:\d{2})$")] private static partial Regex Rfc3339();
    [GeneratedRegex(@"\b\d+(?:\.\d+)?\b")] private static partial Regex Numbers();
    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}")] private static partial Regex Uuid();
    [GeneratedRegex(@"\b(?:0x)?[0-9a-fA-F]{8,}\b")] private static partial Regex Hex();
    [GeneratedRegex("\"[^\"]*\"|'[^']*'")] private static partial Regex Quoted();
    /// <summary>A recorded Unix-nanosecond time as RFC 3339, to the 100 ns a <see cref="DateTimeOffset"/> holds.</summary>
    public static string Time(ulong nanos) => Instant(nanos).ToString("O");
    /// <summary>The instant of a Unix-nanosecond time, to the 100 ns a <see cref="DateTimeOffset"/> holds.</summary>
    public static DateTimeOffset Instant(ulong nanos) => DateTimeOffset.UnixEpoch.AddTicks((long)(nanos / 100));
    public static double Percentile(IEnumerable<double> source, double quantile)
    {
        var values = source.Order().ToArray(); if (values.Length is 0) return double.NaN;
        var p = (values.Length - 1) * quantile; var i = (int)p;
        return values[i] + (values[Math.Min(i + 1, values.Length - 1)] - values[i]) * (p - i);
    }
}

internal sealed record AnalysisRecord(string Id, string? TraceId, string? SpanId, string? SessionId,
    string Service, string Name, ulong Start, ulong End, bool Error, double Duration,
    JsonObject Attributes, JsonObject Resource, SpanStorageRow? Span = null, LogStorageRow? Log = null)
{
    public JsonNode? Value(string key, string? scope = null) => key switch
    {
        CollectorSemanticAttributeCatalog.ServiceName => JsonValue.Create(Service), "span.name" => JsonValue.Create(Name),
        "status" => JsonValue.Create(Error ? "ERROR" : "OK"),
        "severity" => Log is null ? null : JsonValue.Create(Log.SeverityNumber),
        _ => scope == "resource" ? Resource[key] : Attributes[key] ?? (scope is null ? Resource[key] : null)
    };
    public static AnalysisRecord From(SpanStorageRow s) => new(s.SpanId, s.TraceId, s.SpanId, s.SessionId,
        s.ServiceName ?? "unknown_service", s.Name, s.StartTimeUnixNano, s.EndTimeUnixNano, s.StatusCode == 2,
        s.DurationNs / 1_000_000d, SpanAttributes(s), J.Attributes(s.ResourceJson), Span: s);
    private static JsonObject SpanAttributes(SpanStorageRow span)
    {
        var attributes = J.Attributes(span.AttributesJson);
        if (JsonNode.Parse(span.EventsJson ?? "[]") is JsonArray events)
            foreach (var entry in events.OfType<JsonObject>().Where(static e => J.String(e, "name") == "exception"))
                foreach (var pair in J.Attributes(entry["attributes"]?.ToJsonString()))
                    if (pair.Key is CollectorSemanticAttributeCatalog.RecordedFailureKind)
                        attributes[pair.Key] ??= pair.Value?.DeepClone();
        return attributes;
    }
    public static AnalysisRecord From(LogStorageRow l) => new(l.LogId, l.TraceId, l.SpanId, l.SessionId,
        l.ServiceName ?? "unknown_service", l.EventName ?? "", l.EffectiveTimeUnixNano() ?? 0, l.EffectiveTimeUnixNano() ?? 0, l.SeverityNumber >= 17,
        0, J.Attributes(l.AttributesJson), J.Attributes(l.ResourceJson), Log: l);
}

internal static class AnalysisMatchers
{
    private static readonly HashSet<string> s_ops = ["eq", "neq", "in", "prefix", "contains", "exists", "gt", "gte", "lt", "lte"];
    public static void Validate(JsonNode? node)
    {
        if (node is null) return;
        if (node is not JsonArray list || list.Count > 32) throw new AnalysisInputException("matchers", "Use an array with at most 32 matchers.");
        foreach (var m in list)
        {
            var op = J.String(m, "op");
            if (string.IsNullOrEmpty(J.String(m, "key"))) throw new AnalysisInputException("matchers.key", "A non-empty attribute key is required.");
            if (op is null || !s_ops.Contains(op)) throw new AnalysisInputException("matchers.op", "Choose eq, neq, in, prefix, contains, exists, gt, gte, lt or lte.");
            if (J.String(m, "scope") is { } and not ("span" or "resource" or "log")) throw new AnalysisInputException("matchers.scope", "Choose span, resource or log.");
            if (op == "in" && m?["values"] is not JsonArray) throw new AnalysisInputException("matchers.values", "The in operator requires values.");
            if (op != "exists" && op != "in" && m?["value"] is null) throw new AnalysisInputException("matchers.value", "This operator requires a value.");
        }
    }
    public static bool Match(AnalysisRecord record, JsonNode? node)
    {
        if (node is not JsonArray list) return true;
        foreach (var m in list)
        {
            var actual = record.Value(J.String(m, "key")!, J.String(m, "scope") ?? (record.Log is null ? "span" : "log"));
            var expected = m?["value"]; var a = J.Text(actual); var b = J.Text(expected);
            var numeric = double.TryParse(a, CultureInfo.InvariantCulture, out var an) && double.TryParse(b, CultureInfo.InvariantCulture, out var bn) ? an.CompareTo(bn) : (int?)null;
            var ok = J.String(m, "op") switch
            {
                "exists" => actual is not null, "eq" => actual is not null && Equal(actual, expected),
                "neq" => actual is null || !Equal(actual, expected), "in" => actual is { } present && ((JsonArray)m!["values"]!).Any(v => Equal(present, v)),
                "prefix" => actual is not null && a.StartsWith(b, StringComparison.Ordinal),
                "contains" => actual is not null && a.Contains(b, StringComparison.Ordinal),
                "gt" => numeric > 0, "gte" => numeric >= 0, "lt" => numeric < 0, "lte" => numeric <= 0, _ => false
            };
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>
    /// Equality keeps the operand's type: a number equals a number of the same value, a string the same
    /// string and a boolean the same boolean. A number never equals a string, so 1 does not match "1".
    /// </summary>
    public static bool Equal(JsonNode actual, JsonNode? expected) => (actual.GetValueKind(), expected?.GetValueKind()) switch
    {
        (JsonValueKind.Number, JsonValueKind.Number) => SameNumber(actual.ToJsonString(), expected!.ToJsonString()),
        (JsonValueKind.String, JsonValueKind.String) => string.Equals(actual.GetValue<string>(), expected!.GetValue<string>(), StringComparison.Ordinal),
        (JsonValueKind.True or JsonValueKind.False, JsonValueKind.True or JsonValueKind.False) => actual.GetValueKind() == expected!.GetValueKind(),
        _ => false
    };

    // Integers keep every digit as decimals; only values beyond decimal's range compare as doubles.
    private static bool SameNumber(string actual, string expected) =>
        decimal.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
            ? a == b : double.Parse(actual, CultureInfo.InvariantCulture) == double.Parse(expected, CultureInfo.InvariantCulture);
}

internal sealed class AnalysisHandles(byte[] key)
{
    public string Issue(string project, string kind, JsonObject value)
    {
        var data = Encoding.UTF8.GetBytes(J.O(("project", project), ("kind", kind), ("value", value)).ToJsonString());
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + Convert.ToHexStringLower(HMACSHA256.HashData(key, data));
    }
    public JsonObject Read(string token, string project, string kind)
    {
        try
        {
            var parts = token.Split('.'); var b64 = parts[0].Replace('-', '+').Replace('_', '/');
            var data = Convert.FromBase64String(b64.PadRight((b64.Length + 3) / 4 * 4, '='));
            if (parts.Length != 2 || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(parts[1]), HMACSHA256.HashData(key, data))) throw new FormatException();
            var payload = JsonNode.Parse(data)!.AsObject();
            if (J.String(payload, "project") != project || J.String(payload, "kind") != kind) throw new FormatException();
            return payload["value"]!.AsObject();
        }
        catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException or IndexOutOfRangeException)
        { throw new AnalysisInputException(kind, "This handle is invalid or belongs to another project.", kind + ".invalid"); }
    }
}
