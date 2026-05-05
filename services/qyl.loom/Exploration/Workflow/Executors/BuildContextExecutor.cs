
using Microsoft.Agents.AI.Workflows;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Loom.Exploration.Workflow.Executors;

internal sealed class BuildContextExecutor(
    ExplorationContextBuilder contextBuilder,
    ExplorationSessionStore sessionStore,
    ExplorationDiagnostician diagnostician,
    ExplorationStrategist strategist)
    : Executor<StartExplore, ExplorationRunState>("exploration.build_context")
{
    public override async ValueTask<ExplorationRunState> HandleAsync(
        StartExplore message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = new ExplorationRunState { IssueId = message.IssueId, UserContext = message.UserContext };

        if (!diagnostician.IsConfigured || !strategist.IsConfigured)
        {
            await context
                .AddEventAsync(
                    new ExplorationStreamEvent(
                        ExplorationStreamUpdates.Error("No LLM configured — cannot start exploration.")),
                    cancellationToken)
                .ConfigureAwait(false);
            return state with { IsError = true, ErrorMessage = "No LLM configured" };
        }

        await context
            .AddEventAsync(
                new ExplorationStreamEvent(ExplorationStreamUpdates.Progress(0, "Ingesting qyl data...")),
                cancellationToken)
            .ConfigureAwait(false);

        var explorationContext = await contextBuilder
            .BuildAsync(message.IssueId, message.UserContext, ct: cancellationToken)
            .ConfigureAwait(false);

        if (explorationContext.IsEmpty || explorationContext.Issue is null)
        {
            await context
                .AddEventAsync(
                    new ExplorationStreamEvent(
                        ExplorationStreamUpdates.Error($"Issue '{message.IssueId}' not found.")),
                    cancellationToken)
                .ConfigureAwait(false);
            return state with { IsError = true, ErrorMessage = $"Issue '{message.IssueId}' not found." };
        }

        var session = sessionStore.GetOrCreate(message.IssueId);
        sessionStore.SetContext(session.SessionId, message.UserContext, explorationContext.FormattedBlock);
        sessionStore.AppendUserMessage(
            session.SessionId,
            message.UserContext ?? $"Explore issue {message.IssueId}");

        Activity.Current?.SetTag(GenAiAttributes.ConversationId, $"loom:{session.SessionId}");

        return state with { Context = explorationContext, SessionId = session.SessionId };
    }
}
