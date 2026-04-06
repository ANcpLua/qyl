using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Qyl.Loom.Exploration;

/// <summary>
///     Bounded sub-agent responsible for turning the investigation
///     into a minimal implementation plan.
/// </summary>
public sealed partial class ExplorationStrategist(
    ExplorationSessionStore sessionStore,
    ILogger<ExplorationStrategist> logger,
    IChatClient? llm = null)
{
    public bool IsConfigured => llm is not null;

    public async Task<ExplorationSolution?> PlanAsync(string sessionId, CancellationToken ct = default)
    {
        if (llm is null)
            return null;

        var session = sessionStore.Get(sessionId);
        if (session is null)
            return null;

        try
        {
            var response = await llm.GetResponseAsync(BuildPrompt(session), cancellationToken: ct)
                .ConfigureAwait(false);
            return ExplorationResponseParser.TryParseSolution(response.Text ?? "{}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSolutionPlanFailed(ex);
            return null;
        }
    }

    private static string BuildPrompt(ExplorationSessionState session)
    {
        var rootCauseJson = session.RootCause is null
            ? session.DiagnosticTranscript ?? session.ContextBlock ?? string.Empty
            : JsonSerializer.Serialize(session.RootCause, ExplorationJsonContext.Default.ExplorationRootCause);

        return $"""
                {ExplorationPrompts.SolutionPlanning}

                Root cause analysis:
                {rootCauseJson}
                """;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exploration solution planning failed")]
    private partial void LogSolutionPlanFailed(Exception ex);
}
