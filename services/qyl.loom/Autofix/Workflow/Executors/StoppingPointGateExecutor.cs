
namespace Qyl.Loom.Autofix.Workflow.Executors;

internal sealed class StoppingPointGateExecutor<T>(string id) : Executor<T, T>(id)
    where T : notnull
{
    public override ValueTask<T> HandleAsync(T input, IWorkflowContext _, CancellationToken __ = default) =>
        ValueTask.FromResult(input);
}
