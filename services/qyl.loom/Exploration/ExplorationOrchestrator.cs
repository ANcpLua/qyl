using Microsoft.Agents.AI.Workflows;
using Qyl.Contracts.Copilot;
using Qyl.Loom.Exploration.Workflow;

namespace Qyl.Loom.Exploration;

/// <summary>
///     Thin driver over the <see cref="ExplorationWorkflowFactory" /> workflow. Runs one workflow instance
///     per <see cref="ExploreAsync" /> invocation, observes its event stream, and forwards any
///     <see cref="ExplorationStreamEvent" /> to the caller as an <see cref="StreamUpdate" />. Preserves the
///     prior <c>IAsyncEnumerable&lt;StreamUpdate&gt;</c> contract so the SSE endpoint is untouched.
/// </summary>
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
