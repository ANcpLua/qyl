namespace Qyl.Collector.Autofix;

/// <summary>
///     Keeps the active Loom exploration session in memory so the orchestrator
///     can hand off context between the bounded sub-agents.
/// </summary>
public sealed class LoomSessionStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, LoomSessionState> _sessions = new(StringComparer.Ordinal);

    public LoomSessionState GetOrCreate(string issueId) =>
        _sessions.GetOrAdd(issueId,
            static (key, now) =>
                new LoomSessionState { SessionId = key, IssueId = key, CreatedAt = now, UpdatedAt = now },
            timeProvider.GetUtcNow());

    public LoomSessionState? Get(string sessionId) =>
        ((IReadOnlyDictionary<string, LoomSessionState>)_sessions).GetOrNull(sessionId);

    public void SetContext(string sessionId, string? userContext, string contextBlock)
    {
        var session = GetRequired(sessionId);
        session.UserContext = userContext;
        session.ContextBlock = contextBlock;
        Touch(session);
    }

    public void AppendUserMessage(string sessionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        var session = GetRequired(sessionId);
        session.Messages.Add(new LoomSessionMessage(LoomSessionMessageRole.User, content, timeProvider.GetUtcNow()));
        Touch(session);
    }

    public void SaveDiagnosis(string sessionId, string diagnosticTranscript, LoomRootCause? rootCause)
    {
        var session = GetRequired(sessionId);
        session.DiagnosticTranscript = diagnosticTranscript;
        session.RootCause = rootCause;
        session.Messages.Add(new LoomSessionMessage(
            LoomSessionMessageRole.Diagnostician,
            diagnosticTranscript,
            timeProvider.GetUtcNow()));
        Touch(session);
    }

    public void SaveSolution(string sessionId, LoomSolution solution)
    {
        var session = GetRequired(sessionId);
        session.Solution = solution;
        session.Messages.Add(new LoomSessionMessage(
            LoomSessionMessageRole.Strategist,
            JsonSerializer.Serialize(solution, LoomInsightJsonContext.Default.LoomSolution),
            timeProvider.GetUtcNow()));
        Touch(session);
    }

    private LoomSessionState GetRequired(string sessionId) =>
        Get(sessionId) ?? throw new InvalidOperationException($"Loom session '{sessionId}' was not initialized.");

    private void Touch(LoomSessionState session) => session.UpdatedAt = timeProvider.GetUtcNow();
}

public sealed class LoomSessionState
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public string? UserContext { get; set; }
    public string? ContextBlock { get; set; }
    public string? DiagnosticTranscript { get; set; }
    public LoomRootCause? RootCause { get; set; }
    public LoomSolution? Solution { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public List<LoomSessionMessage> Messages { get; } = [];
}

public sealed record LoomSessionMessage(
    LoomSessionMessageRole Role,
    string Content,
    DateTimeOffset Timestamp);

public enum LoomSessionMessageRole
{
    User,
    Diagnostician,
    Strategist
}
