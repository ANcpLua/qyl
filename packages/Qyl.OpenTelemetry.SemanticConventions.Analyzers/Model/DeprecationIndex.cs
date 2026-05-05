
using System.Collections.Immutable;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

internal sealed class DeprecationIndex
{
    private static readonly Lazy<DeprecationIndex> s_instance = new(static () => new DeprecationIndex());

    private DeprecationIndex()
    {
        DeprecatedIds = DeprecatedDiagnostics.ByDeprecatedId.Keys.ToImmutableHashSet(
            StringComparer.OrdinalIgnoreCase);
    }

    public static DeprecationIndex Instance => s_instance.Value;

    public ImmutableHashSet<string> DeprecatedIds { get; }
}
