// =============================================================================
// qyl.copilot - Execution Store Interface
// Abstraction for persisting workflow executions (DuckDB, in-memory, etc.)
// =============================================================================

using qyl.protocol.Copilot;

namespace qyl.copilot.Workflows;

/// <summary>
///     Storage abstraction for workflow execution persistence.
///     Implemented by DuckDbExecutionStore in qyl.collector for production,
///     or can be left null for in-memory-only operation.
/// </summary>
public interface IExecutionStore
{
    /// <summary>
    ///     Persists a new execution record.
    /// </summary>
    Task InsertExecutionAsync(WorkflowExecution execution, CancellationToken ct = default);

    /// <summary>
    ///     Updates an existing execution record (status, result, tokens, etc.).
    /// </summary>
    Task UpdateExecutionAsync(WorkflowExecution execution, CancellationToken ct = default);

    /// <summary>
    ///     Gets a single execution by ID.
    /// </summary>
    Task<WorkflowExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default);

    /// <summary>
    ///     Gets executions with optional filtering, ordered by start time descending.
    /// </summary>
    Task<IReadOnlyList<WorkflowExecution>> GetExecutionsAsync(
        string? workflowName = null,
        WorkflowStatus? status = null,
        int limit = 50,
        CancellationToken ct = default);
}
