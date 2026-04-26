using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Qyl.Instrumentation.Instrumentation.GenAi;

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
            var agent = llm.AsAIAgent(new ChatClientAgentOptions
            {
                Name = "ExplorationStrategistAgent",
                Description = "Converts an exploration root-cause analysis into a minimal implementation plan.",
                ChatOptions = new ChatOptions { Instructions = ExplorationPrompts.SolutionPlanning }
            }).AsBuilder().UseQylAgentTelemetry().Build();

            var response = await agent.RunAsync(BuildUserMessage(session), cancellationToken: ct)
                .ConfigureAwait(false);
            return ExplorationResponseParser.TryParseSolution(response.Text ?? "{}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSolutionPlanFailed(ex);
            return null;
        }
    }

    private static string BuildUserMessage(ExplorationSessionState session)
    {
        var rootCauseJson = session.RootCause is null
            ? session.DiagnosticTranscript ?? session.ContextBlock ?? string.Empty
            : JsonSerializer.Serialize(session.RootCause, ExplorationJsonContext.Default.ExplorationRootCause);

        return $"""
                Root cause analysis:
                {rootCauseJson}
                """;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exploration solution planning failed")]
    private partial void LogSolutionPlanFailed(Exception ex);
}
