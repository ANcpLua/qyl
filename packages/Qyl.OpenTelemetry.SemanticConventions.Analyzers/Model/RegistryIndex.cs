// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

/// <summary>Collects the set of all valid (non-deprecated) OTel attribute IDs and known namespace prefixes.</summary>
internal sealed class RegistryIndex
{
    private static readonly Lazy<RegistryIndex> s_instance = new(static () => new RegistryIndex());

    // Floor set of stable OTel namespace prefixes so QYL-SEMCONV-003 can flag
    // typos even when the upstream registry submodule isn't initialised.
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
            // Replacement IDs are by definition valid current IDs.
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
            // Degrade gracefully if the generated table failed to load.
        }

        // Well-known OTel namespace prefixes as a floor (covers stable attrs not in the deprecated set)
        try
        {
            foreach (var prefix in s_wellKnownOtelPrefixes)
                prefixBuilder.Add(prefix);
        }
        catch
        {
            // Never let static field init issues propagate.
        }

        ValidIds = validBuilder.ToImmutable();
        KnownPrefixes = prefixBuilder.ToImmutable();
    }

    public static RegistryIndex Instance => s_instance.Value;

    /// <summary>All valid (replacement) attribute IDs derived from the deprecated index.</summary>
    public ImmutableHashSet<string> ValidIds { get; }

    /// <summary>Namespace prefixes found in the OTel registry (everything before the first dot).</summary>
    public ImmutableHashSet<string> KnownPrefixes { get; }

    /// <summary>Returns true if <paramref name="id" /> is a current valid OTel attribute ID.</summary>
    public bool IsValid(string id)
    {
        return ValidIds.Contains(id);
    }

    /// <summary>Returns true if <paramref name="id" /> looks like it belongs to an OTel namespace.</summary>
    public bool HasOtelPrefix(string id)
    {
        var dot = id.IndexOf('.');
        return dot > 0 && KnownPrefixes.Contains(id.Substring(0, dot));
    }
}
