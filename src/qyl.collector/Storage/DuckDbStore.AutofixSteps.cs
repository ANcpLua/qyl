using Qyl.Collector.Autofix;

namespace Qyl.Collector.Storage;

/// <summary>
///     Partial class extending <see cref="DuckDbStore" /> with autofix step
///     tracking operations for the Loom autofix pipeline.
/// </summary>
public sealed partial class DuckDbStore
{
    /// <summary>
    ///     Inserts a new autofix step via the channel-buffered write path.
    /// </summary>
    public async Task InsertAutofixStepAsync(AutofixStepRecord step, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO autofix_steps
                                  (step_id, run_id, step_number, step_name, status, input_json)
                              VALUES ($1, $2, $3, $4, $5, $6)
                              ON CONFLICT (step_id) DO NOTHING
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = step.StepId });
            cmd.Parameters.Add(new DuckDBParameter { Value = step.RunId });
            cmd.Parameters.Add(new DuckDBParameter { Value = step.StepNumber });
            cmd.Parameters.Add(new DuckDBParameter { Value = step.StepName });
            cmd.Parameters.Add(new DuckDBParameter { Value = step.Status });
            cmd.Parameters.Add(new DuckDBParameter { Value = step.InputJson ?? (object)DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Updates an autofix step status and output.
    /// </summary>
    public async Task UpdateAutofixStepAsync(
        string stepId, string status, string? outputJson = null,
        string? errorMessage = null, CancellationToken ct = default) =>
        await ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              UPDATE autofix_steps SET
                                  status = $1,
                                  output_json = $2,
                                  error_message = $3,
                                  started_at = CASE WHEN $1 = 'running' AND started_at IS NULL THEN now() ELSE started_at END,
                                  completed_at = CASE WHEN $1 IN ('completed', 'failed') THEN now() ELSE completed_at END
                              WHERE step_id = $4
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = outputJson ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = errorMessage ?? (object)DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = stepId });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>
    ///     Gets all steps for a specific fix run, ordered by step number.
    /// </summary>
    public Task<IReadOnlyList<AutofixStepRecord>> GetAutofixStepsAsync(
        string runId, CancellationToken ct = default) =>
        ReadManyAsync(
            """
            SELECT step_id, run_id, step_number, step_name, status,
                   input_json, output_json, error_message,
                   started_at, completed_at, created_at
            FROM autofix_steps
            WHERE run_id = $1
            ORDER BY step_number ASC
            """,
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = runId }),
            MapAutofixStep, ct);

    /// <summary>
    ///     Returns fix runs with status 'pending' that are ready for agent processing.
    /// </summary>
    public Task<IReadOnlyList<FixRunRecord>> GetPendingFixRunsAsync(
        int limit = 10, CancellationToken ct = default) =>
        ReadManyAsync(
            FixRunSelectSql + " WHERE status = 'pending' ORDER BY created_at ASC LIMIT $1",
            cmd => cmd.Parameters.Add(new DuckDBParameter { Value = limit }),
            MapFixRun, ct);

    private static AutofixStepRecord MapAutofixStep(DbDataReader reader) =>
        new()
        {
            StepId = reader.GetString(0),
            RunId = reader.GetString(1),
            StepNumber = reader.GetInt32(2),
            StepName = reader.GetString(3),
            Status = reader.GetString(4),
            InputJson = reader.Col(5).AsString,
            OutputJson = reader.Col(6).AsString,
            ErrorMessage = reader.Col(7).AsString,
            StartedAt = reader.Col(8).AsDateTime,
            CompletedAt = reader.Col(9).AsDateTime,
            CreatedAt = reader.Col(10).AsDateTime
        };
}
