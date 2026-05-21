namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

/// <summary>
/// Phase 2 additive companion to <see cref="RegistryModel"/>. Carries metric- and event-group
/// projections that the metrics/events generators emit (PR-B, PR-C). Populated from the same
/// embedded <c>resolved-registry.json</c> as <see cref="RegistryModel"/>; the Jinja template
/// (<c>scripts/templates/registry/resolved-registry.json.j2</c>) emits <c>metrics</c> and
/// <c>events</c> arrays alongside the existing attribute catalog.
/// </summary>
internal readonly record struct InstrumentRegistryModel(
    EquatableArray<MetricGroupModel> Metrics,
    EquatableArray<EventGroupModel> Events);

/// <summary>
/// A semconv metric group (a registry entry with <c>type == "metric"</c>).
/// </summary>
internal readonly record struct MetricGroupModel(
    string MetricName,
    string Instrument,
    string Unit,
    string Brief,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<string> AttributeRefs);

/// <summary>
/// A semconv event group (a registry entry with <c>type == "event"</c>).
/// </summary>
internal readonly record struct EventGroupModel(
    string EventName,
    string Brief,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<EventAttributeModel> Payload);

/// <summary>
/// One member of an event's typed payload. Ordering, nullability, and naming
/// of the emitted <c>readonly record struct</c> property are derived from this model.
/// </summary>
internal readonly record struct EventAttributeModel(
    string Key,
    AttributeTypeModel Type,
    bool Required,
    string Brief);

/// <summary>
/// Extracted state from a single metrics-marker application — either
/// <c>[SemanticConventionMetrics("&lt;prefix&gt;")]</c> (stable surface) or
/// <c>[SemanticConventionIncubatingMetrics("&lt;prefix&gt;")]</c> (all-stabilities surface).
/// Mirrors <see cref="MarkerModel"/> for the metrics generator.
/// </summary>
internal readonly record struct MetricsMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);

/// <summary>
/// Extracted state from a single events-marker application — either
/// <c>[SemanticConventionEvents("&lt;prefix&gt;")]</c> (stable surface) or
/// <c>[SemanticConventionIncubatingEvents("&lt;prefix&gt;")]</c> (all-stabilities surface).
/// Mirrors <see cref="MarkerModel"/> for the events generator.
/// </summary>
internal readonly record struct EventsMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);
