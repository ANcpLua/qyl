namespace Qyl.Collector.Storage;

[DuckDbTable("model_pricing_catalog_sources",
    OnConflict = """
    ON CONFLICT (source_id) DO UPDATE SET
        configured_endpoint = EXCLUDED.configured_endpoint,
        configured_fingerprint = EXCLUDED.configured_fingerprint,
        active_snapshot_id = CASE
            WHEN EXCLUDED.status = 'current' THEN EXCLUDED.active_snapshot_id
            WHEN model_pricing_catalog_sources.configured_fingerprint = EXCLUDED.configured_fingerprint THEN model_pricing_catalog_sources.active_snapshot_id
            ELSE NULL
        END,
        active_configuration_fingerprint = CASE
            WHEN EXCLUDED.status = 'current' THEN EXCLUDED.active_configuration_fingerprint
            WHEN model_pricing_catalog_sources.configured_fingerprint = EXCLUDED.configured_fingerprint THEN model_pricing_catalog_sources.active_configuration_fingerprint
            ELSE NULL
        END,
        active_content_hash = CASE
            WHEN EXCLUDED.status = 'current' THEN EXCLUDED.active_content_hash
            WHEN model_pricing_catalog_sources.configured_fingerprint = EXCLUDED.configured_fingerprint THEN model_pricing_catalog_sources.active_content_hash
            ELSE NULL
        END,
        status = EXCLUDED.status,
        last_attempt_at = EXCLUDED.last_attempt_at,
        last_verified_at = CASE
            WHEN EXCLUDED.status = 'current' THEN EXCLUDED.last_verified_at
            WHEN model_pricing_catalog_sources.configured_fingerprint = EXCLUDED.configured_fingerprint THEN model_pricing_catalog_sources.last_verified_at
            ELSE NULL
        END,
        failure_category = EXCLUDED.failure_category
    """)]
internal sealed partial record ModelPricingCatalogSourceRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(64)")]
    public required string SourceId { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(2048)")]
    public required string ConfiguredEndpoint { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(128)")]
    public required string ConfiguredFingerprint { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public string? ActiveSnapshotId { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(128)")]
    public string? ActiveConfigurationFingerprint { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public string? ActiveContentHash { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(32)")]
    public required string Status { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public required DateTimeOffset LastAttemptAt { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset? LastVerifiedAt { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public string? FailureCategory { get; init; }
}

[DuckDbTable("model_pricing_catalog_snapshots",
    Indexes = "SourceId,RetrievedAt")]
internal sealed partial record ModelPricingCatalogSnapshotRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(64)")]
    public required string SourceId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1, SqlType = "VARCHAR(64)")]
    public required string SnapshotId { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string ContentHash { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(128)")]
    public required string ConfigurationFingerprint { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(2048)")]
    public required string SourceEndpoint { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string PriceSemantics { get; init; }

    public required int ModelCount { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public required DateTimeOffset RetrievedAt { get; init; }
}

[DuckDbTable("model_pricing_catalog_models",
    Indexes = "SourceId,SnapshotId;SourceId,SnapshotId,CanonicalModelId")]
internal sealed partial record ModelPricingCatalogModelRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(64)")]
    public required string SourceId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1, SqlType = "VARCHAR(64)")]
    public required string SnapshotId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2, SqlType = "VARCHAR(512)")]
    public required string ModelId { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(512)")]
    public string? CanonicalModelId { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(3)")]
    public required string CurrencyCode { get; init; }
}

[DuckDbTable("model_pricing_catalog_overrides",
    Indexes = "SourceId,SnapshotId,ModelId")]
internal sealed partial record ModelPricingCatalogOverrideRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(64)")]
    public required string SourceId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1, SqlType = "VARCHAR(64)")]
    public required string SnapshotId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2, SqlType = "VARCHAR(512)")]
    public required string ModelId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 3)]
    public required int Priority { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(128)")]
    public required string ConditionUsageDimension { get; init; }

    [DuckDbColumn(SqlType = "DECIMAL(38, 12)")]
    public required decimal ExclusiveMinimumQuantity { get; init; }
}

[DuckDbTable("model_pricing_catalog_rates",
    Indexes = "SourceId,SnapshotId,ModelId")]
internal sealed partial record ModelPricingCatalogRateRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = "VARCHAR(64)")]
    public required string SourceId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1, SqlType = "VARCHAR(64)")]
    public required string SnapshotId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2, SqlType = "VARCHAR(512)")]
    public required string ModelId { get; init; }

    // Zero is the model's base rate card; positive priorities refer to conditional overrides.
    [DuckDbColumn(PrimaryKeyOrdinal = 3)]
    public required int TierPriority { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 4, SqlType = "VARCHAR(128)")]
    public required string SourceMeter { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(128)")]
    public required string UsageDimension { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(32)")]
    public required string Unit { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(64)")]
    public required string SourceBillingMode { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(32)")]
    public required string BillingMode { get; init; }

    [DuckDbColumn(SqlType = "VARCHAR(128)")]
    public string? ReplacesUsageDimension { get; init; }

    [DuckDbColumn(SqlType = "DECIMAL(38, 18)")]
    public required decimal UsdPerUnit { get; init; }
}

internal sealed record ModelPricingCatalogStorageSnapshot(
    ModelPricingCatalogSourceRow Source,
    ModelPricingCatalogSnapshotRow Snapshot,
    IReadOnlyList<ModelPricingCatalogModelRow> Models,
    IReadOnlyList<ModelPricingCatalogOverrideRow> Overrides,
    IReadOnlyList<ModelPricingCatalogRateRow> Rates);

internal sealed record ModelPricingCatalogSourceState(
    ModelPricingCatalogSourceRow Source,
    ModelPricingCatalogSnapshotRow? ActiveSnapshot);
