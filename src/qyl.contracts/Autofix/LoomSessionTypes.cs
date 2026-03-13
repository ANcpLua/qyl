using System.Text.Json.Serialization;

namespace Qyl.Contracts.Autofix;

// ── Session model ────────────────────────────────────────────────────────────

public enum LoomSessionMode { Interactive, Background }
public enum LoomMessageRole { User, Assistant, System, Tool }
public enum LoomPauseReason { WaitingForUser, NeedMoreInfo }

public sealed record LoomSession
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public required LoomSessionMode Mode { get; set; }
    public required LoomStage Stage { get; set; }
    public required LoomStatus Status { get; set; }
    public LoomPauseReason? PauseReason { get; set; }
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; set; }
    public LoomRootCauseResult? RootCause { get; set; }
    public LoomSolutionResult? Solution { get; set; }
    public string? FixRunId { get; set; }
    public string? Error { get; set; }

    // In-memory only (not persisted to DuckDB)
    [JsonIgnore] public List<LoomMessage> Messages { get; } = [];
    [JsonIgnore] public CancellationTokenSource? CancellationTokenSource { get; set; }
}

public sealed record LoomSessionSummary(
    string SessionId,
    string IssueId,
    LoomStage Stage,
    LoomStatus Status,
    LoomSessionMode Mode,
    long CreatedAt,
    bool HasRootCause,
    bool HasSolution);

public sealed record LoomMessage(
    LoomMessageRole Role,
    string Content,
    string? ToolName = null,
    string? ToolArgs = null);

// ── Wire format types (SSE contract) ─────────────────────────────────────────

public sealed record LoomCausalStep(int Order, string Description, bool IsRootCause);
public sealed record LoomSolutionStep(string Title, string Description);

// ── Tool return types ────────────────────────────────────────────────────────

public sealed record LoomRootCauseResult(string Summary, LoomCausalStep[] Steps);
public sealed record LoomSolutionResult(string Summary, LoomSolutionStep[] Steps);
public sealed record LoomCodeItUpResult(
    bool Success, string? RunId, string? PrUrl, double Confidence, string? Error);

// ── Request types ────────────────────────────────────────────────────────────

public sealed record InterruptRequest(string Message);
