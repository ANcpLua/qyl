using Microsoft.Extensions.AI;

namespace Qyl.Collector.Autofix;

/// <summary>
///     Bounded Loom sub-agent responsible only for turning the investigation
///     into a minimal implementation plan.
/// </summary>
public sealed partial class LoomStrategist(
    LoomSessionStore sessionStore,
    ILogger<LoomStrategist> logger,
    IChatClient? llm = null)
{
    public bool IsConfigured => llm is not null;

    public async Task<LoomSolution?> PlanAsync(string sessionId, CancellationToken ct = default)
    {
        if (llm is null)
            return null;

        var session = sessionStore.Get(sessionId);
        if (session is null)
            return null;

        try
        {
            var response = await llm.GetResponseAsync(BuildPrompt(session), cancellationToken: ct).ConfigureAwait(false);
            return LoomResponseParser.TryParseSolution(response.Text ?? "{}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSolutionPlanFailed(ex);
            return null;
        }
    }

    private static string BuildPrompt(LoomSessionState session)
    {
        var rootCauseJson = session.RootCause is null
            ? session.DiagnosticTranscript ?? session.ContextBlock ?? string.Empty
            : JsonSerializer.Serialize(session.RootCause, LoomInsightJsonContext.Default.LoomRootCause);

        return $"""
                {LoomPrompts.SolutionPlanning}

                Root cause analysis:
                {rootCauseJson}
                """;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loom solution planning failed")]
    private partial void LogSolutionPlanFailed(Exception ex);
}
