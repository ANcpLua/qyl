namespace qyl.mcp.Agents;

/// <summary>
///     Abstraction for an embedded LLM agent that can investigate observability data.
///     Implementations may proxy to the collector's Copilot chat endpoint (HTTP)
///     or run an agent in-process via Microsoft.Extensions.AI.
/// </summary>
internal interface IAgentProvider
{
    /// <summary>Whether the agent provider is available and ready to handle requests.</summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Sends a natural language question to the embedded agent and streams the response.
    ///     The agent has access to observability tools (search_spans, get_trace, etc.)
    ///     and will translate the question into the appropriate queries.
    /// </summary>
    /// <param name="question">Natural language question about observability data.</param>
    /// <param name="systemPrompt">System prompt with domain-specific instructions.</param>
    /// <param name="context">Optional additional context (service name, time range, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of text chunks forming the agent's response.</returns>
    IAsyncEnumerable<AgentStreamChunk> InvestigateAsync(
        string question,
        string systemPrompt,
        string? context = null,
        CancellationToken ct = default);
}

/// <summary>
///     A chunk of the agent's streamed response.
/// </summary>
internal sealed record AgentStreamChunk
{
    /// <summary>Text content from the agent.</summary>
    public string? Content { get; init; }

    /// <summary>Tool name if this is a tool call event.</summary>
    public string? ToolName { get; init; }

    /// <summary>Tool result if this is a tool result event.</summary>
    public string? ToolResult { get; init; }

    /// <summary>Error message if something went wrong.</summary>
    public string? Error { get; init; }

    /// <summary>Whether this chunk signals stream completion.</summary>
    public bool IsCompleted { get; init; }

    /// <summary>Output tokens consumed (populated on completion).</summary>
    public long? OutputTokens { get; init; }
}
