using Qyl.Contracts.Agenting;

namespace Qyl.Collector.Autofix;

/// <summary>
///     Keeps active Loom exploration sessions and issues continuation tokens
///     for multi-turn specialist execution. In-memory for now — persistence
///     is the next step (DuckDB or event-sourced ledger).
/// </summary>
public sealed class LoomSessionStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, LoomSessionState> _sessions = new(StringComparer.Ordinal);

    public LoomSessionState GetOrCreate(string issueId) =>
        _sessions.GetOrAdd(issueId,
            static (key, now) => new LoomSessionState
            {
                SessionId = key,
                IssueId = key,
                CurrentPhase = AgentRunPhase.Intake,
                CreatedAt = now,
                UpdatedAt = now
            },
            timeProvider.GetUtcNow());

    public LoomSessionState? Get(string sessionId) =>
        ((IReadOnlyDictionary<string, LoomSessionState>)_sessions).GetOrNull(sessionId);

    public void SetContext(string sessionId, string? userContext, string contextBlock)
    {
        var session = GetRequired(sessionId);
        session.UserContext = userContext;
        session.ContextBlock = contextBlock;
        session.CurrentPhase = AgentRunPhase.Context;
        Touch(session);
    }

    public void AppendUserMessage(string sessionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        var session = GetRequired(sessionId);
        session.Transcript.Add(new LoomSessionEntry
        {
            Role = LoomSpecialistRole.User,
            Content = content,
            TimestampUtc = timeProvider.GetUtcNow(),
            Phase = session.CurrentPhase
        });
        session.TurnCount++;
        Touch(session);
    }

    public void SaveDiagnosis(string sessionId, string diagnosticTranscript, LoomRootCause? rootCause)
    {
        var session = GetRequired(sessionId);
        session.DiagnosticTranscript = diagnosticTranscript;
        session.RootCause = rootCause;
        session.CurrentPhase = AgentRunPhase.Diagnose;
        session.Transcript.Add(new LoomSessionEntry
        {
            Role = LoomSpecialistRole.Diagnostician,
            Content = diagnosticTranscript,
            TimestampUtc = timeProvider.GetUtcNow(),
            Phase = AgentRunPhase.Diagnose
        });
        session.TurnCount++;
        Touch(session);
    }

    public void SaveSolution(string sessionId, LoomSolution solution)
    {
        var session = GetRequired(sessionId);
        session.Solution = solution;
        session.CurrentPhase = AgentRunPhase.Plan;
        session.Transcript.Add(new LoomSessionEntry
        {
            Role = LoomSpecialistRole.Strategist,
            Content = JsonSerializer.Serialize(solution, LoomInsightJsonContext.Default.LoomSolution),
            TimestampUtc = timeProvider.GetUtcNow(),
            Phase = AgentRunPhase.Plan
        });
        session.TurnCount++;
        Touch(session);
    }

    /// <summary>
    ///     Issues a continuation token from the current session state.
    ///     Use this to resume a session across turns or process restarts.
    /// </summary>
    public LoomContinuationToken? IssueContinuation(string sessionId)
    {
        var session = Get(sessionId);
        return session?.ToSnapshot().IssueContinuation(
            session.LastSpecialist,
            session.CurrentPhase);
    }

    /// <summary>
    ///     Takes a snapshot of the session for persistence or handoff.
    /// </summary>
    public LoomSessionSnapshot? TakeSnapshot(string sessionId) =>
        Get(sessionId)?.ToSnapshot();

    private LoomSessionState GetRequired(string sessionId) =>
        Get(sessionId) ?? throw new InvalidOperationException($"Loom session '{sessionId}' was not initialized.");

    private void Touch(LoomSessionState session) => session.UpdatedAt = timeProvider.GetUtcNow();
}

public sealed class LoomSessionState
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public AgentRunPhase CurrentPhase { get; set; }
    public LoomSpecialistRole LastSpecialist { get; set; } = LoomSpecialistRole.Orchestrator;
    public string? UserContext { get; set; }
    public string? ContextBlock { get; set; }
    public string? DiagnosticTranscript { get; set; }
    public LoomRootCause? RootCause { get; set; }
    public LoomSolution? Solution { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public int TurnCount { get; set; }
    public List<LoomSessionEntry> Transcript { get; } = [];

    internal LoomSessionSnapshot ToSnapshot() => new()
    {
        SessionId = SessionId,
        IssueId = IssueId,
        CurrentPhase = CurrentPhase,
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = UpdatedAt,
        UserContext = UserContext,
        ContextBlock = ContextBlock,
        DiagnosticTranscript = DiagnosticTranscript,
        RootCauseJson = RootCause is not null
            ? JsonSerializer.Serialize(RootCause, LoomInsightJsonContext.Default.LoomRootCause)
            : null,
        SolutionJson = Solution is not null
            ? JsonSerializer.Serialize(Solution, LoomInsightJsonContext.Default.LoomSolution)
            : null,
        TurnCount = TurnCount,
        Transcript = [.. Transcript]
    };
}
