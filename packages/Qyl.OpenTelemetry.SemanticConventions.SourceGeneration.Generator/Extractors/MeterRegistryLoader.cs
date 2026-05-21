using System.IO;
using System.Reflection;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Loads the metric-shaped projection of the embedded resolved registry.
/// Reads the same JSON resource as <see cref="RegistryLoader"/> but lifts a
/// different shape: <see cref="MeterRegistryModel"/>.
///
/// Owned by activity-surface-eng (Task #5 / PR-D). instrument-surface-eng
/// (Task #4) maintains its own catalog projection independently — the Phase 3
/// auditor reconciles if and when the two converge on the same fields.
/// </summary>
internal static class MeterRegistryLoader
{
    private const string ResourceName = "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json";

    private static readonly Lazy<MeterRegistryModel> _registry = new(LoadFromEmbeddedResource);

    public static MeterRegistryModel Registry => _registry.Value;

    private static MeterRegistryModel LoadFromEmbeddedResource()
    {
        var assembly = typeof(MeterRegistryLoader).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var root = JsonReader.Parse(text);
        return ParseRegistry(root);
    }

    internal static MeterRegistryModel ParseRegistry(JsonValue root)
    {
        if (root is not JsonObject obj)
            return new MeterRegistryModel(default);

        if (obj.TryGetArray("metrics") is not { } metricsArr)
            return new MeterRegistryModel(default);

        var meters = new List<MeterModel>();
        foreach (var item in metricsArr.Items)
        {
            if (item is not JsonObject metric) continue;

            var unit = metric.GetString("unit");
            meters.Add(new MeterModel(
                MetricName: metric.GetString("metric_name"),
                Instrument: metric.GetString("instrument"),
                Unit: unit,
                ValueType: MeterValueTypeRules.SelectValueType(metric.GetString("instrument"), unit),
                Brief: metric.GetString("brief"),
                Stability: ParseStability(metric.GetString("stability")),
                Deprecated: RegistryParsing.ParseDeprecated(metric.TryGet("deprecated") as JsonObject)));
        }

        return new MeterRegistryModel(meters.ToEquatableArray());
    }

    private static StabilityModel ParseStability(string value) => value switch
    {
        "stable" => StabilityModel.Stable,
        "development" => StabilityModel.Development,
        "deprecated" => StabilityModel.Deprecated,
        "alpha" => StabilityModel.Alpha,
        "beta" => StabilityModel.Beta,
        "release_candidate" => StabilityModel.ReleaseCandidate,
        _ => StabilityModel.Development
    };
}

/// <summary>
/// Unit-to-value-type mapping for OpenTelemetry instruments. The rules follow
/// the OTel <c>instrument</c> + UCUM-style <c>unit</c> convention:
/// continuous units (<c>s</c>, <c>ms</c>, <c>By</c>, <c>1</c>, <c>%</c>) project
/// to <see cref="double"/>; counting units (<c>{request}</c>, <c>{count}</c>)
/// project to <see cref="long"/>. Gauges are always observable and default to
/// <see cref="double"/>.
///
/// Audit (pinned-goal directive, Phase B-2): state-style metrics
/// (<c>http.client.active_requests</c>, <c>http.client.open_connections</c>,
/// <c>http.server.active_requests</c>, etc.) follow the
/// <c>updowncounter</c> pattern in upstream semconv, not <c>gauge</c>. This
/// loader preserves the registry's declared <c>instrument</c> verbatim —
/// <c>MetersEmitter.ResolveInstrumentType</c> dispatches <c>updowncounter</c>
/// to <c>UpDownCounter&lt;T&gt;</c> and only <c>gauge</c>/<c>observablegauge</c>
/// to <c>ObservableGauge&lt;T&gt;</c>. We never re-route <c>updowncounter</c>
/// rows to <c>Gauge</c> based on metric name or unit heuristics; the
/// registry's <c>instrument</c> field is authoritative.
/// </summary>
internal static class MeterValueTypeRules
{
    public static string SelectValueType(string instrument, string unit)
    {
        if (string.Equals(instrument, "gauge", StringComparison.Ordinal) ||
            string.Equals(instrument, "observablegauge", StringComparison.Ordinal))
            return "double";

        return unit switch
        {
            "s" or "ms" or "us" or "ns" => "double",
            "By" => "long",
            "%" => "double",
            "1" => "double",
            _ => IsCountingUnit(unit) ? "long" : "double"
        };
    }

    private static bool IsCountingUnit(string unit)
        => !string.IsNullOrEmpty(unit) && unit.StartsWith("{", StringComparison.Ordinal);
}

internal static class RegistryParsing
{
    public static DeprecatedModel? ParseDeprecated(JsonObject? obj)
    {
        if (obj is null) return null;

        var reason = obj.GetString("reason");
        var note = obj.GetString("note");

        return reason switch
        {
            "renamed" => new DeprecatedModel.Renamed(obj.GetString("renamed_to"), note),
            "obsoleted" => new DeprecatedModel.Obsoleted(note),
            _ => new DeprecatedModel.Uncategorized(note)
        };
    }
}
