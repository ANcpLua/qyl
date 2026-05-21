using System.IO;
using System.Reflection;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Loads the embedded resolved-registry projection once per analyzer assembly load.
/// The JSON shape is qyl-owned (not the upstream <c>resolved-registry-v2</c> contract);
/// it is the minimum projection needed for source generation, emitted by a custom Jinja
/// template pinned to semconv v1.41.0 + Weaver v0.23.0.
/// </summary>
/// <remarks>
/// Uses a minimal hand-rolled JSON reader rather than <c>System.Text.Json</c> because
/// shipping a runtime dependency on STJ in a Roslyn analyzer is a known IDE-version
/// clash hazard (the analyzer DLL would load alongside the IDE's bundled STJ and
/// may produce binding-redirect surprises). The reader covers exactly the shape
/// of <see cref="JsonReader"/>'s grammar.
/// </remarks>
internal static class RegistryLoader
{
    private const string ResourceName = "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json";

    private static readonly Lazy<JsonObject?> _root = new(LoadRootFromEmbeddedResource);
    private static readonly Lazy<RegistryModel> _registry = new(static () => ParseRegistry(_root.Value));
    private static readonly Lazy<InstrumentRegistryModel> _instruments = new(static () => ParseInstruments(_root.Value));

    public static RegistryModel Registry => _registry.Value;

    public static InstrumentRegistryModel Instruments => _instruments.Value;

    private static JsonObject? LoadRootFromEmbeddedResource()
    {
        var assembly = typeof(RegistryLoader).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return JsonReader.Parse(text) as JsonObject;
    }

    internal static RegistryModel ParseRegistry(JsonObject? root)
    {
        if (root is null)
            return new RegistryModel(default, default);

        var groups = root.TryGetArray("groups") is { } groupsArr
            ? ParseGroups(groupsArr)
            : default;

        var catalog = root.TryGetArray("catalog") is { } catalogArr
            ? ParseCatalog(catalogArr)
            : default;

        return new RegistryModel(groups, catalog);
    }

    internal static InstrumentRegistryModel ParseInstruments(JsonObject? root)
    {
        if (root is null)
            return new InstrumentRegistryModel(default, default);

        var catalog = root.TryGetArray("catalog") is { } catalogArr
            ? BuildAttributeIndex(catalogArr)
            : new Dictionary<string, AttributeModel>(StringComparer.Ordinal);

        var metrics = root.TryGetArray("metrics") is { } metricsArr
            ? ParseMetrics(metricsArr)
            : default;

        var events = root.TryGetArray("events") is { } eventsArr
            ? ParseEvents(eventsArr, catalog)
            : default;

        return new InstrumentRegistryModel(metrics, events);
    }

    private static Dictionary<string, AttributeModel> BuildAttributeIndex(JsonArray catalogArr)
    {
        var byKey = new Dictionary<string, AttributeModel>(StringComparer.Ordinal);
        foreach (var attr in ParseCatalog(catalogArr))
            byKey[attr.Key] = attr;
        return byKey;
    }

    private static EquatableArray<MetricGroupModel> ParseMetrics(JsonArray metricsArr)
    {
        var metrics = new List<MetricGroupModel>(metricsArr.Items.Count);
        foreach (var item in metricsArr.Items)
        {
            if (item is not JsonObject metric) continue;

            var refs = new List<string>();
            if (metric.TryGetArray("attribute_refs") is { } refsArr)
            {
                foreach (var value in refsArr.Items)
                {
                    if (value is JsonString s) refs.Add(s.Value);
                }
            }

            metrics.Add(new MetricGroupModel(
                MetricName: metric.GetString("metric_name"),
                Instrument: metric.GetString("instrument"),
                Unit: metric.GetString("unit"),
                Brief: metric.GetString("brief"),
                Stability: ParseStability(metric.GetString("stability")),
                Deprecated: ParseDeprecated(metric.TryGet("deprecated") as JsonObject),
                AttributeRefs: refs.ToEquatableArray()));
        }
        return metrics.ToEquatableArray();
    }

    private static EquatableArray<EventGroupModel> ParseEvents(
        JsonArray eventsArr,
        Dictionary<string, AttributeModel> attributeIndex)
    {
        var events = new List<EventGroupModel>(eventsArr.Items.Count);
        foreach (var item in eventsArr.Items)
        {
            if (item is not JsonObject ev) continue;

            var payload = new List<EventAttributeModel>();
            if (ev.TryGetArray("payload") is { } payloadArr)
            {
                foreach (var value in payloadArr.Items)
                {
                    if (value is not JsonObject p) continue;

                    var key = p.GetString("key");
                    var requirement = p.GetString("requirement_level");
                    var type = p.TryGet("type") is { } typeNode
                        ? ParseType(typeNode)
                        : attributeIndex.TryGetValue(key, out var catalogAttr)
                            ? catalogAttr.Type
                            : new AttributeTypeModel.Primitive("string");
                    var brief = p.TryGet("brief") is JsonString briefStr
                        ? briefStr.Value
                        : attributeIndex.TryGetValue(key, out var briefAttr)
                            ? briefAttr.Brief
                            : string.Empty;

                    payload.Add(new EventAttributeModel(
                        Key: key,
                        Type: type,
                        Required: string.Equals(requirement, "required", StringComparison.Ordinal),
                        Brief: brief));
                }
            }

            events.Add(new EventGroupModel(
                EventName: ev.GetString("event_name"),
                Brief: ev.GetString("brief"),
                Stability: ParseStability(ev.GetString("stability")),
                Deprecated: ParseDeprecated(ev.TryGet("deprecated") as JsonObject),
                Payload: payload.ToEquatableArray()));
        }
        return events.ToEquatableArray();
    }

    private static EquatableArray<GroupModel> ParseGroups(JsonArray groupsArr)
    {
        var groups = new List<GroupModel>(groupsArr.Items.Count);
        foreach (var item in groupsArr.Items)
        {
            if (item is not JsonObject group) continue;

            var refs = new List<string>();
            if (group.TryGetArray("attribute_refs") is { } refsArr)
            {
                foreach (var value in refsArr.Items)
                {
                    if (value is JsonString s) refs.Add(s.Value);
                }
            }

            groups.Add(new GroupModel(
                Id: group.GetString("id"),
                Type: group.GetString("type"),
                Prefix: group.GetString("prefix"),
                AttributeRefs: refs.ToEquatableArray()));
        }
        return groups.ToEquatableArray();
    }

    private static EquatableArray<AttributeModel> ParseCatalog(JsonArray catalogArr)
    {
        var attributes = new List<AttributeModel>(catalogArr.Items.Count);
        foreach (var item in catalogArr.Items)
        {
            if (item is not JsonObject attr) continue;

            var examples = new List<string>();
            if (attr.TryGetArray("examples") is { } examplesArr)
            {
                foreach (var value in examplesArr.Items)
                {
                    if (value is JsonString s) examples.Add(s.Value);
                }
            }

            var stability = ParseStability(attr.GetString("stability"));

            attributes.Add(new AttributeModel(
                Key: attr.GetString("key"),
                Type: ParseType(attr.TryGet("type"), stability),
                Brief: attr.GetString("brief"),
                Note: attr.GetString("note"),
                Stability: stability,
                Deprecated: ParseDeprecated(attr.TryGet("deprecated") as JsonObject),
                Examples: examples.ToEquatableArray()));
        }
        return attributes.ToEquatableArray();
    }

    private static AttributeTypeModel ParseType(
        JsonValue? value,
        StabilityModel defaultStability = StabilityModel.Development)
    {
        if (value is JsonString s)
        {
            return s.Value.StartsWith("template[", StringComparison.Ordinal)
                ? new AttributeTypeModel.Template(s.Value)
                : new AttributeTypeModel.Primitive(s.Value);
        }

        if (value is JsonObject obj && obj.TryGetArray("members") is { } membersArr)
        {
            var members = new List<EnumMemberModel>();
            foreach (var item in membersArr.Items)
            {
                if (item is not JsonObject member) continue;
                members.Add(new EnumMemberModel(
                    Id: member.GetString("id"),
                    Value: member.GetString("value"),
                    Brief: member.GetString("brief"),
                    Stability: ParseStability(member.GetString("stability"), defaultStability),
                    Deprecated: ParseDeprecated(member.TryGet("deprecated") as JsonObject)));
            }
            return new AttributeTypeModel.EnumType(members.ToEquatableArray());
        }

        return new AttributeTypeModel.Primitive("string");
    }

    private static StabilityModel ParseStability(
        string value,
        StabilityModel defaultStability = StabilityModel.Development) => value switch
    {
        "stable" => StabilityModel.Stable,
        "development" => StabilityModel.Development,
        "deprecated" => StabilityModel.Deprecated,
        "alpha" => StabilityModel.Alpha,
        "beta" => StabilityModel.Beta,
        "release_candidate" => StabilityModel.ReleaseCandidate,
        _ => defaultStability
    };

    private static DeprecatedModel? ParseDeprecated(JsonObject? obj)
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
