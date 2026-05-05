using Qyl.Contracts.Agenting;

namespace Qyl.Loom.Exploration;

public sealed class ExplorationSessionStore(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, ExplorationSessionState> _sessions = new(StringComparer.Ordinal);

    public ExplorationSessionState GetOrCreate(string issueId) =>
        _sessions.GetOrAdd(issueId,
            static (key, now) => new ExplorationSessionState
            {
                SessionId = key,
                IssueId = key,
                CurrentPhase = AgentRunPhase.Intake,
                CreatedAt = now,
                UpdatedAt = now
            },
            timeProvider.GetUtcNow());

    public ExplorationSessionState? Get(string sessionId) =>
        _sessions.GetValueOrDefault(sessionId);

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
        session.Transcript.Add(new ExplorationSessionEntry
        {
            Role = LoomSpecialistRole.User,
            Content = content,
            TimestampUtc = timeProvider.GetUtcNow(),
            Phase = session.CurrentPhase
        });
        session.TurnCount++;
        Touch(session);
    }

    public void SaveDiagnosis(string sessionId, string diagnosticTranscript, ExplorationRootCause? rootCause)
    {
        var session = GetRequired(sessionId);
        session.DiagnosticTranscript = diagnosticTranscript;
        session.RootCause = rootCause;
        session.CurrentPhase = AgentRunPhase.Diagnose;
        session.Transcript.Add(new ExplorationSessionEntry
        {
            Role = LoomSpecialistRole.Diagnostician,
            Content = diagnosticTranscript,
            TimestampUtc = timeProvider.GetUtcNow(),
            Phase = AgentRunPhase.Diagnose
        });
        session.TurnCount++;
        Touch(session);
    }

    public void SaveSolution(string sessionId, ExplorationSolution solution)
    {
        var session = GetRequired(sessionId);
        session.Solution = solution;
        session.CurrentPhase = AgentRunPhase.Plan;
        session.Transcript.Add(new ExplorationSessionEntry
        {
            Role = LoomSpecialistRole.Strategist,
            Content = JsonSerializer.Serialize(solution, ExplorationJsonContext.Default.ExplorationSolution),
            TimestampUtc = timeProvider.GetUtcNow(),
            Phase = AgentRunPhase.Plan
        });
        session.TurnCount++;
        Touch(session);
    }

    private ExplorationSessionState GetRequired(string sessionId) =>
        Get(sessionId) ??
        throw new InvalidOperationException($"Exploration session '{sessionId}' was not initialized.");

    private void Touch(ExplorationSessionState session) => session.UpdatedAt = timeProvider.GetUtcNow();
}

public sealed class ExplorationSessionState
{
    public required string SessionId { get; init; }
    public required string IssueId { get; init; }
    public AgentRunPhase CurrentPhase { get; set; }
    public LoomSpecialistRole LastSpecialist { get; set; } = LoomSpecialistRole.Orchestrator;
    public string? UserContext { get; set; }
    public string? ContextBlock { get; set; }
    public string? DiagnosticTranscript { get; set; }
    public ExplorationRootCause? RootCause { get; set; }
    public ExplorationSolution? Solution { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public int TurnCount { get; set; }
    public List<ExplorationSessionEntry> Transcript { get; } = [];
}

public sealed record ExplorationSessionEntry
{
    public required LoomSpecialistRole Role { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required AgentRunPhase Phase { get; init; }
}
