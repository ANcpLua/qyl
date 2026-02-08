using qyl.copilot.Workflows;
using qyl.protocol.Copilot;

namespace qyl.collector.Storage;

/// <summary>
///     DuckDB-backed implementation of <see cref="IExecutionStore"/>.
///     Delegates to <see cref="DuckDbStore"/> workflow execution methods.
/// </summary>
internal sealed class DuckDbExecutionStore(DuckDbStore store) : IExecutionStore
{
    public Task InsertExecutionAsync(WorkflowExecution execution, CancellationToken ct = default)
        => store.InsertExecutionAsync(execution, ct);

    public Task UpdateExecutionAsync(WorkflowExecution execution, CancellationToken ct = default)
        => store.UpdateExecutionAsync(execution, ct);

    public Task<WorkflowExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default)
        => store.GetExecutionAsync(executionId, ct);

    public Task<IReadOnlyList<WorkflowExecution>> GetExecutionsAsync(
        string? workflowName = null,
        WorkflowStatus? status = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var statusStr = status?.ToString().ToUpperInvariant();
        return store.GetExecutionsAsync(workflowName, statusStr, limit, ct);
    }
}
