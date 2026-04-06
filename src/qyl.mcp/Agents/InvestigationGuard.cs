using System.Collections.Concurrent;
using System.Text;

namespace qyl.mcp.Agents;

/// <summary>
///     Tracks tool invocations during a use_qyl meta-agent investigation and enforces
///     a configurable maximum. When the cap is hit, throws <see cref="OperationCanceledException" />
///     with a partial results summary so the caller receives what was found rather than nothing.
/// </summary>
/// <remarks>
///     One guard per investigation. Create via <see cref="FromEnvironment" /> at the start
///     of each <c>UseQylAsync</c> call.
///     <para>
///         Max tool calls default to 200, overridable via <c>QYL_AGENT_MAX_TOOL_CALLS</c>.
///     </para>
/// </remarks>
internal sealed class InvestigationGuard(int maxToolCalls)
{
    private readonly ConcurrentDictionary<string, int> _toolCallCounts = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _partialResults = [];
    private int _totalCalls;

    /// <summary>Total tool invocations recorded so far.</summary>
    public int TotalCalls => _totalCalls;

    /// <summary>Maximum tool invocations allowed before the guard trips.</summary>
    public int MaxToolCalls => maxToolCalls;

    /// <summary>Per-tool invocation counts for diagnostics.</summary>
    public IReadOnlyDictionary<string, int> ToolCallCounts =>
        _toolCallCounts.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal);

    /// <summary>
    ///     Creates a guard using the <c>QYL_AGENT_MAX_TOOL_CALLS</c> environment variable,
    ///     falling back to <paramref name="defaultMax" /> when the variable is absent or not a valid integer.
    /// </summary>
    public static InvestigationGuard FromEnvironment(int defaultMax = 200)
    {
        var raw = Environment.GetEnvironmentVariable("QYL_AGENT_MAX_TOOL_CALLS");
        var max = raw is not null && int.TryParse(raw, out var parsed) && parsed > 0
            ? parsed
            : defaultMax;
        return new InvestigationGuard(max);
    }

    /// <summary>
    ///     Records a tool invocation. If the invocation count reaches <see cref="MaxToolCalls" />,
    ///     throws <see cref="OperationCanceledException" /> containing a summary of partial results
    ///     and per-tool call counts.
    /// </summary>
    /// <param name="toolName">Name of the tool being invoked.</param>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when the total tool call count reaches <see cref="MaxToolCalls" />.
    ///     The exception message contains a diagnostic summary of all partial results collected
    ///     and per-tool invocation counts.
    /// </exception>
    public void RecordCall(string toolName)
    {
        _toolCallCounts.AddOrUpdate(toolName, 1, static (_, count) => count + 1);
        var current = Interlocked.Increment(ref _totalCalls);

        if (current < maxToolCalls)
            return;

        throw new OperationCanceledException(BuildCapMessage());
    }

    /// <summary>
    ///     Accumulates a partial result from a completed tool invocation.
    ///     These are included in the cap-reached summary so the caller gets
    ///     what was found before the investigation was stopped.
    /// </summary>
    public void AddPartialResult(string toolName, string result)
    {
        var trimmed = result.Length > 500
            ? string.Concat(result.AsSpan(0, 500), "... (truncated)")
            : result;

        _partialResults.Enqueue($"[{toolName}] {trimmed}");
    }

    /// <summary>
    ///     Builds a human-readable diagnostic summary of the investigation state.
    ///     Used both when the cap is hit and for on-demand diagnostics.
    /// </summary>
    public string BuildDiagnosticSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Investigation progress: {_totalCalls}/{maxToolCalls} tool calls");
        sb.AppendLine();

        AppendToolBreakdown(sb);
        AppendPartialResults(sb);

        return sb.ToString();
    }

    private string BuildCapMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Investigation stopped: reached {maxToolCalls} tool call limit.");
        sb.AppendLine("Partial results collected so far:");
        sb.AppendLine();

        AppendToolBreakdown(sb);
        AppendPartialResults(sb);

        return sb.ToString();
    }

    private void AppendToolBreakdown(StringBuilder sb)
    {
        sb.AppendLine("Tool call breakdown:");
        foreach (var (tool, count) in _toolCallCounts.OrderByDescending(static kv => kv.Value))
        {
            sb.AppendLine($"  {tool}: {count}");
        }
    }

    private void AppendPartialResults(StringBuilder sb)
    {
        if (_partialResults.IsEmpty)
            return;

        sb.AppendLine();
        sb.AppendLine("Partial results:");
        foreach (var result in _partialResults)
        {
            sb.AppendLine($"  {result}");
        }
    }
}
