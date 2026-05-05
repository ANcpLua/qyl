using Microsoft.Agents.AI.Workflows;
using Qyl.Contracts.Copilot;
using Qyl.Loom.Exploration.Workflow;

namespace Qyl.Loom.Exploration;

public sealed partial class ExplorationOrchestrator(
    IServiceProvider services,
    ILogger<ExplorationOrchestrator> logger)
{
    public async IAsyncEnumerable<StreamUpdate> ExploreAsync(
        string issueId,
        string? userContext,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        LogExplorationStarted(issueId);

        var workflow = ExplorationWorkflowFactory.Create(services);

        var streamingRun = await InProcessExecution
            .RunStreamingAsync(
                workflow,
                new StartExplore(issueId, userContext),
                CheckpointManager.Default,
                issueId,
                ct)
            .ConfigureAwait(false);

        await using (streamingRun.ConfigureAwait(false))
        {
            await foreach (var evt in streamingRun.WatchStreamAsync(ct).ConfigureAwait(false))
            {
                if (evt is ExplorationStreamEvent streamEvent)
                {
                    yield return streamEvent.Update;
                }
                else if (evt is WorkflowOutputEvent)
                {
                    yield break;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Exploration started for issue {IssueId}")]
    private partial void LogExplorationStarted(string issueId);
}
