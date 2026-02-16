namespace qyl.collector.SchemaControl;

/// <summary>
///     Executes planned schema promotions against DuckDB.
///     Records execution time, status, and SQL applied. Rollback is not supported (append-only).
/// </summary>
public sealed partial class SchemaExecutor(DuckDbStore store, ILogger<SchemaExecutor> logger)
{
    /// <summary>
    ///     Executes the SQL statements for a pending promotion and records the outcome.
    /// </summary>
    public async Task<SchemaPromotionRecord?> ExecutePromotionAsync(
        string promotionId,
        CancellationToken ct = default)
    {
        var promotion = await store.GetSchemaPromotionAsync(promotionId, ct).ConfigureAwait(false);
        if (promotion is null)
            return null;

        if (promotion.Status is not "pending")
            throw new InvalidOperationException(
                $"Promotion {promotionId} has status '{promotion.Status}' and cannot be applied.");

        var sw = Stopwatch.StartNew();
        try
        {
            await store.ExecuteSchemaDdlAsync(promotion.SqlStatements, ct).ConfigureAwait(false);
            sw.Stop();

            await store.UpdateSchemaPromotionStatusAsync(promotionId, "applied", ct).ConfigureAwait(false);

            LogPromotionApplied(promotionId, sw.ElapsedMilliseconds);

            return promotion with { Status = "applied", AppliedAt = TimeProvider.System.GetUtcNow().UtcDateTime };
        }
        catch (Exception ex)
        {
            sw.Stop();

            await store.UpdateSchemaPromotionStatusAsync(promotionId, "failed", ct).ConfigureAwait(false);

            LogPromotionFailed(promotionId, ex);

            return promotion with { Status = "failed" };
        }
    }

    // ==========================================================================
    // LoggerMessage — structured, zero-allocation logging
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Schema promotion applied: {PromotionId} in {DurationMs}ms")]
    private partial void LogPromotionApplied(string promotionId, long durationMs);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Schema promotion failed: {PromotionId}")]
    private partial void LogPromotionFailed(string promotionId, Exception ex);
}

// =============================================================================
// Schema Control Records
// =============================================================================

/// <summary>
///     Request to create a schema promotion.
/// </summary>
public sealed record PromotionRequest(
    string ChangeType,
    string TargetTable,
    string? TargetColumn = null,
    string? ColumnType = null,
    string? RequestedBy = null);

/// <summary>
///     Storage record for a schema promotion.
/// </summary>
public sealed record SchemaPromotionRecord(
    string PromotionId,
    string? RequestedBy,
    string ChangeType,
    string TargetTable,
    string? TargetColumn,
    string? ColumnType,
    string SqlStatements,
    string Status,
    DateTime? AppliedAt,
    DateTime CreatedAt);
