// =============================================================================
// qyl.protocol - Copilot Integration Types
// BCL-only shared types for GitHub Copilot workflows
// Owner: qyl.protocol | Consumers: copilot, collector, dashboard
// =============================================================================

namespace qyl.protocol.Copilot;

/// <summary>
///     Trigger type for workflow execution.
/// </summary>
public enum WorkflowTrigger
{
    /// <summary>Manual trigger via API or UI.</summary>
    Manual,

    /// <summary>Scheduled trigger (cron-based).</summary>
    Scheduled,

    /// <summary>Event-driven trigger (e.g., error threshold).</summary>
    Event,

    /// <summary>Webhook trigger from external system.</summary>
    Webhook
}

/// <summary>
///     Workflow execution status.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>Workflow is queued but not started.</summary>
    Pending,

    /// <summary>Workflow is currently executing.</summary>
    Running,

    /// <summary>Workflow completed successfully.</summary>
    Completed,

    /// <summary>Workflow failed with an error.</summary>
    Failed,

    /// <summary>Workflow was cancelled by user or system.</summary>
    Cancelled
}

/// <summary>
///     Chat message role in a conversation.
/// </summary>
public enum ChatRole
{
    /// <summary>System message (instructions).</summary>
    System,

    /// <summary>User message (prompt).</summary>
    User,

    /// <summary>Assistant message (response).</summary>
    Assistant,

    /// <summary>Tool/function call result.</summary>
    Tool
}

/// <summary>
///     Type of streaming update during execution.
/// </summary>
public enum StreamUpdateKind
{
    /// <summary>Text content being streamed.</summary>
    Content,

    /// <summary>Tool/function is being called.</summary>
    ToolCall,

    /// <summary>Tool/function call completed.</summary>
    ToolResult,

    /// <summary>Execution completed successfully.</summary>
    Completed,

    /// <summary>Error occurred during execution.</summary>
    Error,

    /// <summary>Progress update with percentage.</summary>
    Progress,

    /// <summary>Metadata update (tokens, timing).</summary>
    Metadata
}

/// <summary>
///     Parsed workflow definition from .qyl/workflows/*.md files.
/// </summary>
public sealed record CopilotWorkflow
{
    /// <summary>Workflow name (from frontmatter or filename).</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>List of tool names this workflow can use.</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>How this workflow is triggered.</summary>
    public WorkflowTrigger Trigger { get; init; } = WorkflowTrigger.Manual;

    /// <summary>The workflow instructions (markdown body).</summary>
    public required string Instructions { get; init; }

    /// <summary>File path where this workflow was loaded from.</summary>
    public string? FilePath { get; init; }

    /// <summary>Optional cron schedule for scheduled workflows.</summary>
    public string? Schedule { get; init; }

    /// <summary>Custom metadata from frontmatter.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
///     Workflow execution state and result.
/// </summary>
public sealed record WorkflowExecution
{
    /// <summary>Unique execution ID (GUID).</summary>
    public required string Id { get; init; }

    /// <summary>Name of the workflow being executed.</summary>
    public required string WorkflowName { get; init; }

    /// <summary>Current execution status.</summary>
    public WorkflowStatus Status { get; init; }

    /// <summary>When execution started (UTC).</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>When execution completed (UTC), if finished.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Execution result text, if completed.</summary>
    public string? Result { get; init; }

    /// <summary>Error message, if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Input parameters provided at execution time.</summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    /// <summary>Total input tokens consumed.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Total output tokens generated.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Associated trace ID for observability.</summary>
    public string? TraceId { get; init; }
}

/// <summary>
///     Streaming update during workflow or chat execution.
/// </summary>
public sealed record StreamUpdate
{
    /// <summary>Type of update.</summary>
    public required StreamUpdateKind Kind { get; init; }

    /// <summary>Text content (for Content kind).</summary>
    public string? Content { get; init; }

    /// <summary>Tool name (for ToolCall/ToolResult kinds).</summary>
    public string? ToolName { get; init; }

    /// <summary>Tool call arguments JSON (for ToolCall kind).</summary>
    public string? ToolArguments { get; init; }

    /// <summary>Tool call result (for ToolResult kind).</summary>
    public string? ToolResult { get; init; }

    /// <summary>Error message (for Error kind).</summary>
    public string? Error { get; init; }

    /// <summary>Progress percentage 0-100 (for Progress kind).</summary>
    public int? Progress { get; init; }

    /// <summary>Input tokens consumed so far.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Output tokens generated so far.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Timestamp of this update (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
///     Chat message in a conversation.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>Message role.</summary>
    public required ChatRole Role { get; init; }

    /// <summary>Message content.</summary>
    public required string Content { get; init; }

    /// <summary>Tool call ID (for tool messages).</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Tool name (for tool messages).</summary>
    public string? ToolName { get; init; }

    /// <summary>When message was created.</summary>
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>
///     Context for a chat or workflow execution.
/// </summary>
public sealed record CopilotContext
{
    /// <summary>Session/conversation ID for multi-turn conversations.</summary>
    public string? SessionId { get; init; }

    /// <summary>Previous messages in the conversation.</summary>
    public IReadOnlyList<ChatMessage>? History { get; init; }

    /// <summary>Template parameters to substitute in workflow instructions.</summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    /// <summary>Additional context data (e.g., telemetry data to analyze).</summary>
    public string? AdditionalContext { get; init; }

    /// <summary>Maximum tokens to generate.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Temperature for sampling (0.0-2.0).</summary>
    public double? Temperature { get; init; }
}

/// <summary>
///     Copilot authentication status.
/// </summary>
public sealed record CopilotAuthStatus
{
    /// <summary>Whether authentication is valid.</summary>
    public required bool IsAuthenticated { get; init; }

    /// <summary>Authentication method used.</summary>
    public string? AuthMethod { get; init; }

    /// <summary>GitHub username if authenticated.</summary>
    public string? Username { get; init; }

    /// <summary>Available Copilot capabilities.</summary>
    public IReadOnlyList<string>? Capabilities { get; init; }

    /// <summary>Token expiration time (if known).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Error message if authentication failed.</summary>
    public string? Error { get; init; }
}

/// <summary>
///     Request to start a chat interaction.
/// </summary>
public sealed record ChatRequest
{
    /// <summary>The user's prompt.</summary>
    public required string Prompt { get; init; }

    /// <summary>Execution context.</summary>
    public CopilotContext? Context { get; init; }
}

/// <summary>
///     Request to execute a workflow.
/// </summary>
public sealed record WorkflowRunRequest
{
    /// <summary>Workflow name to execute.</summary>
    public required string WorkflowName { get; init; }

    /// <summary>Template parameters.</summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    /// <summary>Execution context.</summary>
    public CopilotContext? Context { get; init; }
}
