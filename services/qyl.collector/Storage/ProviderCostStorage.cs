namespace Qyl.Collector.Storage;

[DuckDbTable("provider_cost_buckets",
    Indexes = "ProjectId,PeriodStart;ProjectId,Provider,PeriodStart;ProjectId,Provider,ModelKey,PeriodStart",
    OnConflict = """
    ON CONFLICT (project_id, provider, period_start, model_key, currency_code) DO UPDATE SET
        period_end = EXCLUDED.period_end,
        source_endpoint = EXCLUDED.source_endpoint,
        provider_scope_key = EXCLUDED.provider_scope_key,
        source_kind = EXCLUDED.source_kind,
        attribution = EXCLUDED.attribution,
        amount = EXCLUDED.amount,
        retrieved_at = EXCLUDED.retrieved_at
    """)]
internal sealed partial record ProviderCostBucketRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1, SqlType = "VARCHAR(64)")]
    public required string Provider { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2, SqlType = "TIMESTAMPTZ")]
    public required DateTimeOffset PeriodStart { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public required DateTimeOffset PeriodEnd { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 3, SqlType = "VARCHAR(256)")]
    public required string ModelKey { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(2048)")]
    public required string SourceEndpoint { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)", DefaultSql = "''")]
    public required string ProviderScopeKey { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string SourceKind { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string Attribution { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 4, SqlType = "VARCHAR(3)")]
    public required string CurrencyCode { get; init; }

    [DuckDbColumn(SqlType = "DECIMAL(38, 12)")]
    public required decimal Amount { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public required DateTimeOffset RetrievedAt { get; init; }
}

[DuckDbTable("provider_cost_sync",
    Indexes = "ProjectId;ProjectId,Provider",
    OnConflict = """
    ON CONFLICT (project_id, provider) DO UPDATE SET
        source_endpoint = EXCLUDED.source_endpoint,
        provider_scope_key = EXCLUDED.provider_scope_key,
        source_kind = EXCLUDED.source_kind,
        attribution = EXCLUDED.attribution,
        status = EXCLUDED.status,
        last_attempt_at = EXCLUDED.last_attempt_at,
        last_success_at = CASE
            WHEN EXCLUDED.status = 'current' THEN EXCLUDED.last_success_at
            WHEN provider_cost_sync.provider_scope_key = EXCLUDED.provider_scope_key THEN provider_cost_sync.last_success_at
            ELSE NULL
        END,
        period_start = CASE
            WHEN EXCLUDED.status = 'current' THEN EXCLUDED.period_start
            WHEN provider_cost_sync.provider_scope_key = EXCLUDED.provider_scope_key THEN provider_cost_sync.period_start
            ELSE NULL
        END,
        period_end = CASE
            WHEN EXCLUDED.status = 'current' THEN EXCLUDED.period_end
            WHEN provider_cost_sync.provider_scope_key = EXCLUDED.provider_scope_key THEN provider_cost_sync.period_end
            ELSE NULL
        END,
        failure_category = EXCLUDED.failure_category
    """)]
internal sealed partial record ProviderCostSyncRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(128)")]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1, SqlType = "VARCHAR(64)")]
    public required string Provider { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(2048)")]
    public required string SourceEndpoint { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)", DefaultSql = "''")]
    public required string ProviderScopeKey { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string SourceKind { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string Attribution { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string Status { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public required DateTimeOffset LastAttemptAt { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset? LastSuccessAt { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset? PeriodStart { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset? PeriodEnd { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public string? FailureCategory { get; init; }
}
