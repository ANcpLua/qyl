// =============================================================================
// qyl.copilot - Conditional Router
// Predicate-based edge selection for DAG workflow branching
// =============================================================================

namespace qyl.copilot.Workflows;

/// <summary>
///     A conditional edge from one node to another.
/// </summary>
public sealed record ConditionalEdge
{
    /// <summary>Target node ID if the predicate matches.</summary>
    public required string TargetNodeId { get; init; }

    /// <summary>
    ///     Predicate function evaluated against node output and shared state.
    ///     If null, this edge is the default/fallback route.
    /// </summary>
    public Func<object?, IReadOnlyDictionary<string, object?>, bool>? Predicate { get; init; }

    /// <summary>Human-readable label for logging.</summary>
    public string? Label { get; init; }
}

/// <summary>
///     Evaluates conditional edges to determine which nodes execute next.
///     Predicates are evaluated in order; first match wins unless multiple are configured.
/// </summary>
public sealed partial class ConditionalRouter
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Creates a new conditional router.
    /// </summary>
    public ConditionalRouter(ILogger<ConditionalRouter> logger) => _logger = Guard.NotNull(logger);

    /// <summary>
    ///     Resolves the next node IDs based on the current node's output and conditional edges.
    ///     Falls back to the node's static dependencies if no conditional edges are defined.
    /// </summary>
    public IReadOnlyList<string> ResolveNextNodes(
        WorkflowNode currentNode,
        object? nodeOutput,
        IReadOnlyDictionary<string, object?> sharedState)
    {
        ArgumentNullException.ThrowIfNull(currentNode);

        if (currentNode.ConditionalEdges is not { Count: > 0 })
            return currentNode.Dependents;

        var selected = new List<string>();

        foreach (var edge in currentNode.ConditionalEdges)
        {
            if (edge.Predicate is null)
            {
                // Default/fallback edge — selected only if no other edge matched
                if (selected.Count == 0)
                {
                    selected.Add(edge.TargetNodeId);
                    LogBranchSelected(_logger, currentNode.Id, edge.TargetNodeId, edge.Label ?? "default");
                }

                continue;
            }

            try
            {
                if (edge.Predicate(nodeOutput, sharedState))
                {
                    selected.Add(edge.TargetNodeId);
                    LogBranchSelected(_logger, currentNode.Id, edge.TargetNodeId, edge.Label ?? "predicate");
                }
            }
            catch (Exception ex)
            {
                LogPredicateError(_logger, currentNode.Id, edge.TargetNodeId, ex.Message);
            }
        }

        return selected;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "DAG router: {FromNode} -> {ToNode} via [{Label}]")]
    private static partial void LogBranchSelected(ILogger logger, string fromNode, string toNode, string label);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DAG router: predicate error {FromNode} -> {ToNode}: {Error}")]
    private static partial void LogPredicateError(ILogger logger, string fromNode, string toNode, string error);
}
