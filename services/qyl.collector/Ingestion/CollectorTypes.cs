namespace Qyl.Collector.Ingestion;

/// <summary>
///     OTel semantic convention schema version.
///     Static class instead of enum to allow static Current property.
/// </summary>
public static class SchemaVersion
{
    /// <summary>Current target schema version.</summary>
    public static readonly SemconvVersion Current = SemconvVersion.V1400;
}

/// <summary>
///     Semantic convention version identifier.
/// </summary>
public readonly record struct SemconvVersion(string Version, string SchemaUrl)
{
    public static readonly SemconvVersion V1400 = new("1.40.0", "https://opentelemetry.io/schemas/1.40.0");

    /// <summary>Get the schema URL for this version.</summary>
    public Uri ToSchemaUrl() => new(SchemaUrl);

    public override string ToString() => Version;
}
