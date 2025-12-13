// =============================================================================
// qyl.protocol - TraceNode Model
// Hierarchical trace representation
// =============================================================================

namespace Qyl.Protocol.Models;

/// <summary>
///     A node in a trace tree. Represents a span with its children.
/// </summary>
public sealed record TraceNode
{
    /// <summary>The span at this node.</summary>
    public required SpanRecord Span { get; init; }

    /// <summary>Child nodes (spans that have this span as parent).</summary>
    public IReadOnlyList<TraceNode> Children { get; init; } = [];

    /// <summary>Depth in the trace tree (0 for root).</summary>
    public int Depth { get; init; }

    /// <summary>Whether this is a root span (no parent).</summary>
    public bool IsRoot => string.IsNullOrEmpty(Span.ParentSpanId);

    /// <summary>Whether this span has children.</summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>Total number of descendants (recursive count).</summary>
    public int DescendantCount => Children.Sum(c => 1 + c.DescendantCount);
}