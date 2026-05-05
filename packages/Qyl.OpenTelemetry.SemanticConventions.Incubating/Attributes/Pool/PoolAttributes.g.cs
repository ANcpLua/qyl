

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Pool;

public static class PoolAttributes
{
    [global::System.Obsolete("Replaced by db.client.connection.pool.name.", false)]
    public const string Name = "pool.name";
}
