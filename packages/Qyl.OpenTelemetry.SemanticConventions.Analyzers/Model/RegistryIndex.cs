
using System.Collections.Immutable;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

internal sealed class RegistryIndex
{
    private static readonly Lazy<RegistryIndex> s_instance = new(static () => new RegistryIndex());

    private static readonly ImmutableArray<string> s_wellKnownOtelPrefixes =
        ImmutableArray.Create(
            "http", "db", "rpc", "messaging", "faas", "net", "peer",
            "enduser", "service", "telemetry", "code", "exception",
            "thread", "process", "k8s", "container", "host", "os",
            "deployment", "cloud", "device", "browser", "android", "ios",
            "otel", "gen_ai", "log", "event", "session", "url", "user",
            "network", "system", "feature_flag", "azure", "aws", "gcp",
            "graphql", "vcs", "artifact", "cicd", "linux", "tls");

    private RegistryIndex()
    {
        var validBuilder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        var prefixBuilder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var entry in DeprecatedDiagnostics.ByDeprecatedId.Values)
            foreach (var replacement in entry.Replacements)
            {
                validBuilder.Add(replacement);
                var dot = replacement.IndexOf('.');
                if (dot > 0)
                    prefixBuilder.Add(replacement.Substring(0, dot));
            }
        }
        catch
        {
        }

        try
        {
            foreach (var prefix in s_wellKnownOtelPrefixes)
                prefixBuilder.Add(prefix);
        }
        catch
        {
        }

        ValidIds = validBuilder.ToImmutable();
        KnownPrefixes = prefixBuilder.ToImmutable();
    }

    public static RegistryIndex Instance => s_instance.Value;

    public ImmutableHashSet<string> ValidIds { get; }

    public ImmutableHashSet<string> KnownPrefixes { get; }

    public bool IsValid(string id)
    {
        return ValidIds.Contains(id);
    }

    public bool HasOtelPrefix(string id)
    {
        var dot = id.IndexOf('.');
        return dot > 0 && KnownPrefixes.Contains(id.Substring(0, dot));
    }
}
