namespace Qyl.Collector.Storage;

internal sealed record GenAiEtlAuditStorageRow
{
    public required string ServiceName { get; init; }

    public string? OperationName { get; init; }

    public string? OutputType { get; init; }

    public string? ProviderName { get; init; }

    public string? ModelName { get; init; }

    public string? ModelIdentityBasis { get; init; }

    public required long CallCount { get; init; }

    public required long InputTokens { get; init; }

    public required long OutputTokens { get; init; }

    public required long CacheReadInputTokens { get; init; }

    public required long CacheCreationInputTokens { get; init; }

    public required long ReasoningOutputTokens { get; init; }

    public required long TokenUsageCallCount { get; init; }

    public required long ErrorCount { get; init; }

    public required double AverageLatencyMs { get; init; }

    public required double P95LatencyMs { get; init; }
}

internal enum GenAiEtlAuditUsageEligibility
{
    Eligible,
    UnsupportedOperation,
    MissingRequiredUsage,
    InvalidUsage
}

internal sealed record GenAiEtlAuditUsageBucket
{
    public required string ServiceName { get; init; }

    public string? OperationName { get; init; }

    public string? OutputType { get; init; }

    public string? ProviderName { get; init; }

    public string? ModelName { get; init; }

    public string? ModelIdentityBasis { get; init; }

    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? CacheReadInputTokens { get; init; }

    public long? CacheCreationInputTokens { get; init; }

    public long? ReasoningOutputTokens { get; init; }

    public required long CallCount { get; init; }

    public GenAiEtlAuditUsageEligibility Eligibility => GetEligibility();

    private GenAiEtlAuditUsageEligibility GetEligibility()
    {
        var requiresOutput = OperationName switch
        {
            CollectorSemanticAttributeCatalog.GenAiOperationNameValues.Chat or
                CollectorSemanticAttributeCatalog.GenAiOperationNameValues.GenerateContent or
                CollectorSemanticAttributeCatalog.GenAiOperationNameValues.TextCompletion => true,
            CollectorSemanticAttributeCatalog.GenAiOperationNameValues.Embeddings => false,
            _ => (bool?)null
        };
        if (!requiresOutput.HasValue)
            return GenAiEtlAuditUsageEligibility.UnsupportedOperation;

        if (InputTokens is < 0 ||
            OutputTokens is < 0 ||
            CacheReadInputTokens is < 0 ||
            CacheCreationInputTokens is < 0 ||
            ReasoningOutputTokens is < 0)
        {
            return GenAiEtlAuditUsageEligibility.InvalidUsage;
        }

        if (!InputTokens.HasValue || requiresOutput.Value && !OutputTokens.HasValue)
            return GenAiEtlAuditUsageEligibility.MissingRequiredUsage;

        var inputTokens = InputTokens.Value;
        var cacheReadTokens = CacheReadInputTokens.GetValueOrDefault();
        var cacheCreationTokens = CacheCreationInputTokens.GetValueOrDefault();
        if (cacheReadTokens > inputTokens || cacheCreationTokens > inputTokens - cacheReadTokens)
            return GenAiEtlAuditUsageEligibility.InvalidUsage;

        if (ReasoningOutputTokens is { } reasoningTokens &&
            (OutputTokens is not { } outputTokens || reasoningTokens > outputTokens))
        {
            return GenAiEtlAuditUsageEligibility.InvalidUsage;
        }

        return GenAiEtlAuditUsageEligibility.Eligible;
    }
}

internal sealed record GenAiEtlAuditStorageSnapshot(
    IReadOnlyList<GenAiEtlAuditStorageRow> AuditRows,
    IReadOnlyList<GenAiEtlAuditUsageBucket> UsageBuckets,
    IReadOnlyList<ProviderCostBucketRow> CostBuckets,
    IReadOnlyList<ProviderCostSyncRow> CostSyncRows);
