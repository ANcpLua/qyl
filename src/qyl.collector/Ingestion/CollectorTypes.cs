// =============================================================================
// qyl.collector - Collector-specific types
// Types used only by the collector that are not in the TypeSpec schema
// =============================================================================

namespace qyl.collector.Ingestion;

/// <summary>
///     Unix timestamp in nanoseconds since epoch.
///     Matches OTel fixed64 timestamp format.
/// </summary>
public readonly record struct UnixNano(ulong Value)
{
    /// <summary>Empty/zero timestamp.</summary>
    public static UnixNano Empty => default;

    /// <summary>Check if this is an empty/zero timestamp.</summary>
    public bool IsEmpty => Value == 0;

    public static implicit operator ulong(UnixNano value) => value.Value;
    public static implicit operator UnixNano(ulong value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
///     OTel semantic convention schema version.
///     Static class instead of enum to allow static Current property.
/// </summary>
public static class SchemaVersion
{
    /// <summary>Current target schema version.</summary>
    public static readonly SemconvVersion Current = SemconvVersion.V1_38_0;
}

/// <summary>
///     Semantic convention version identifier.
/// </summary>
public readonly record struct SemconvVersion(string Version, string SchemaUrl)
{
    public static readonly SemconvVersion V1_38_0 = new("1.38.0", "https://opentelemetry.io/schemas/1.38.0");

    /// <summary>Get the schema URL for this version.</summary>
    public Uri ToSchemaUrl() => new(SchemaUrl);

    public override string ToString() => Version;
}
