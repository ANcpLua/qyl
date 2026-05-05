namespace Qyl.Collector.Ingestion;

public static class SchemaVersion
{
    public static readonly SemconvVersion Current = SemconvVersion.V1400;
}

public readonly record struct SemconvVersion(string Version, string SchemaUrl)
{
    public static readonly SemconvVersion V1400 = new("1.40.0", "https://opentelemetry.io/schemas/1.40.0");

    public Uri ToSchemaUrl() => new(SchemaUrl);

    public override string ToString() => Version;
}
