using System.Text.RegularExpressions;

namespace qyl.collector.SchemaControl;

/// <summary>
///     Plans schema changes by generating safe, additive DDL statements.
///     Never produces destructive DDL (DROP TABLE, DROP COLUMN).
/// </summary>
public sealed partial class SchemaPlanner(DuckDbStore store, ILogger<SchemaPlanner> logger)
{
    private static readonly string[] AllowedChangeTypes = ["add_column", "add_table", "add_index"];

    [GeneratedRegex(@"\b(DROP\s+TABLE|DROP\s+COLUMN|ALTER\s+TABLE\s+\S+\s+DROP)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DestructiveDdlPattern();

    /// <summary>
    ///     Generates a migration plan for the requested schema change.
    /// </summary>
    public async Task<SchemaPromotionRecord> PlanPromotionAsync(
        PromotionRequest request,
        CancellationToken ct = default)
    {
        if (!AllowedChangeTypes.Contains(request.ChangeType))
            throw new ArgumentException(
                $"Unsupported change type: {request.ChangeType}. Allowed: {string.Join(", ", AllowedChangeTypes)}");

        var sql = GenerateSql(request);
        ValidateNoDestructiveDdl(sql);

        var promotionId = $"promo-{Guid.CreateVersion7():N}"[..24];

        var record = new SchemaPromotionRecord(
            promotionId,
            request.RequestedBy,
            request.ChangeType,
            request.TargetTable,
            request.TargetColumn,
            request.ColumnType,
            sql,
            "pending",
            null,
            TimeProvider.System.GetUtcNow().UtcDateTime);

        await store.InsertSchemaPromotionAsync(record, ct).ConfigureAwait(false);

        LogPromotionPlanned(promotionId, request.ChangeType, request.TargetTable);

        return record;
    }

    /// <summary>
    ///     Returns all promotions with status 'pending'.
    /// </summary>
    public Task<IReadOnlyList<SchemaPromotionRecord>> GetPendingPromotionsAsync(
        CancellationToken ct = default) =>
        store.GetSchemaPromotionsByStatusAsync("pending", ct);

    /// <summary>
    ///     Gets a single promotion by its ID.
    /// </summary>
    public Task<SchemaPromotionRecord?> GetPromotionAsync(
        string promotionId,
        CancellationToken ct = default) =>
        store.GetSchemaPromotionAsync(promotionId, ct);

    private static string GenerateSql(PromotionRequest request) => request.ChangeType switch
    {
        "add_column" =>
            $"ALTER TABLE {request.TargetTable} ADD COLUMN IF NOT EXISTS {request.TargetColumn} {request.ColumnType};",
        "add_table" =>
            $"CREATE TABLE IF NOT EXISTS {request.TargetTable} ({request.TargetColumn} {request.ColumnType});",
        "add_index" =>
            $"CREATE INDEX IF NOT EXISTS idx_{request.TargetTable}_{request.TargetColumn} ON {request.TargetTable}({request.TargetColumn});",
        _ => throw new ArgumentException($"Unsupported change type: {request.ChangeType}")
    };

    private static void ValidateNoDestructiveDdl(string sql)
    {
        if (DestructiveDdlPattern().IsMatch(sql))
            throw new InvalidOperationException("Destructive DDL (DROP) is not allowed in schema promotions.");
    }

    // ==========================================================================
    // LoggerMessage — structured, zero-allocation logging
    // ==========================================================================

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Schema promotion planned: {PromotionId} ({ChangeType} on {TargetTable})")]
    private partial void LogPromotionPlanned(string promotionId, string changeType, string targetTable);
}
