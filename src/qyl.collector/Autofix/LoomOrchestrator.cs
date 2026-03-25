using Qyl.Contracts.Copilot;

namespace Qyl.Collector.Autofix;

/// <summary>
///     Facade for the interactive Loom exploration flow. The endpoint talks to
///     this orchestrator only; diagnosis and planning are delegated internally.
/// </summary>
public sealed partial class LoomOrchestrator(
    IssueContextBuilder issueContextBuilder,
    LoomSessionStore sessionStore,
    IServiceProvider services,
    ILogger<LoomOrchestrator> logger)
{
    public async IAsyncEnumerable<StreamUpdate> ExploreAsync(
        string issueId,
        string? userContext,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var diagnostician = services.GetRequiredKeyedService<LoomDiagnostician>(LoomAgentKeys.Diagnostician);
        var strategist = services.GetRequiredKeyedService<LoomStrategist>(LoomAgentKeys.Strategist);

        if (!diagnostician.IsConfigured || !strategist.IsConfigured)
        {
            yield return MakeError("No LLM configured — cannot start Loom exploration.");
            yield break;
        }

        yield return MakeProgress(0, "Ingesting qyl data...");

        var context = await issueContextBuilder.BuildAsync(issueId, userContext, ct: ct).ConfigureAwait(false);
        if (context.IsEmpty || context.Issue is null)
        {
            yield return MakeError($"Issue '{issueId}' not found.");
            yield break;
        }

        var session = sessionStore.GetOrCreate(issueId);
        sessionStore.SetContext(session.SessionId, userContext, context.FormattedBlock);
        sessionStore.AppendUserMessage(session.SessionId, userContext ?? $"Explore issue {issueId}");

        LogExplorationStarted(issueId, context.Events.Count);

        yield return MakeProgress(20, "Figuring out the root cause...");

        var diagnosis = await diagnostician.DiagnoseAsync(context, ct).ConfigureAwait(false);
        foreach (var update in diagnosis.Updates)
            yield return update;

        if (diagnosis.IsInterrupted)
        {
            yield return MakeProgress(100, "Exploration interrupted.");
            yield return MakeCompleted();
            yield break;
        }

        sessionStore.SaveDiagnosis(session.SessionId, diagnosis.Monologue, diagnosis.RootCause);

        yield return MakeProgress(60, "Synthesizing root cause...");
        if (diagnosis.RootCause is not null)
        {
            yield return MakeContent(
                JsonSerializer.Serialize(diagnosis.RootCause, LoomInsightJsonContext.Default.LoomRootCause),
                "root_cause");
        }

        yield return MakeProgress(80, "Planning solution...");

        var solution = await strategist.PlanAsync(session.SessionId, ct).ConfigureAwait(false);
        if (solution is not null)
        {
            sessionStore.SaveSolution(session.SessionId, solution);
            yield return MakeContent(
                JsonSerializer.Serialize(solution, LoomInsightJsonContext.Default.LoomSolution),
                "solution");
        }

        yield return MakeProgress(100, "Formatting for human consumption...");
        yield return MakeCompleted();

        LogExplorationCompleted(
            issueId,
            diagnosis.RootCause?.Steps.Length ?? 0,
            solution?.Steps.Length ?? 0);
    }

    private static StreamUpdate MakeProgress(int percent, string message) => new()
    {
        Kind = StreamUpdateKind.Progress,
        Progress = percent,
        Content = message,
        Timestamp = TimeProvider.System.GetUtcNow()
    };

    private static StreamUpdate MakeContent(string content, string? toolName = null) => new()
    {
        Kind = StreamUpdateKind.Content,
        Content = content,
        ToolName = toolName,
        Timestamp = TimeProvider.System.GetUtcNow()
    };

    private static StreamUpdate MakeError(string error) => new()
    {
        Kind = StreamUpdateKind.Error, Error = error, Timestamp = TimeProvider.System.GetUtcNow()
    };

    private static StreamUpdate MakeCompleted() => new()
    {
        Kind = StreamUpdateKind.Completed, Timestamp = TimeProvider.System.GetUtcNow()
    };

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Loom exploration started for issue {IssueId} with {EventCount} events")]
    private partial void LogExplorationStarted(string issueId, int eventCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Loom exploration completed for issue {IssueId}: {RcaSteps} RCA steps, {SolutionSteps} solution steps")]
    private partial void LogExplorationCompleted(string issueId, int rcaSteps, int solutionSteps);
}

public static class LoomAgentKeys
{
    public const string Diagnostician = "loom.diagnostician";
    public const string Strategist = "loom.strategist";
}
