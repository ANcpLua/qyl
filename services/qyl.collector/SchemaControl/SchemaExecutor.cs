namespace Qyl.Collector.SchemaControl;

[QylService(QylLifetime.Singleton)]
public sealed partial class SchemaExecutor(DuckDbStore store, ILogger<SchemaExecutor> logger)
{
    public async Task<SchemaPromotionRecord?> ExecutePromotionAsync(
        string promotionId,
        CancellationToken ct = default)
    {
        if (await store.GetSchemaPromotionAsync(promotionId, ct).ConfigureAwait(false) is not { } promotion)
            return null;

        if (promotion.Status is not "pending")
        {
            throw new InvalidOperationException(
                $"Promotion {promotionId} has status '{promotion.Status}' and cannot be applied.");
        }

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


    [LoggerMessage(Level = LogLevel.Information,
        Message = "Schema promotion applied: {PromotionId} in {DurationMs}ms")]
    private partial void LogPromotionApplied(string promotionId, long durationMs);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Schema promotion failed: {PromotionId}")]
    private partial void LogPromotionFailed(string promotionId, Exception ex);
}


public sealed record PromotionRequest(
    string ChangeType,
    string TargetTable,
    string? TargetColumn = null,
    string? ColumnType = null,
    string? RequestedBy = null);

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
