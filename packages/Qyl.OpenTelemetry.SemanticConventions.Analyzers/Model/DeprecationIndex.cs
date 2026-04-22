// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

/// <summary>
/// Thin facade over the generated <see cref="DeprecatedDiagnostics"/> table. Exposes the
/// deprecated-id set for consumers that only need membership checks
/// (e.g. <c>UnknownConventionAnalyzer</c>).
/// </summary>
internal sealed class DeprecationIndex
{
    private static readonly Lazy<DeprecationIndex> s_instance = new(static () => new DeprecationIndex());

    public static DeprecationIndex Instance => s_instance.Value;

    /// <summary>All deprecated IDs as a case-insensitive set.</summary>
    public ImmutableHashSet<string> DeprecatedIds { get; }

    private DeprecationIndex()
    {
        DeprecatedIds = DeprecatedDiagnostics.ByDeprecatedId.Keys.ToImmutableHashSet(
            StringComparer.OrdinalIgnoreCase);
    }
}
