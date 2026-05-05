#nullable enable

namespace Qyl.Domains.Ops.Retention;

public sealed class RetentionPolicyEntity
{
    public required string Id { get; init; }
    public required string TargetTable { get; init; }
    public required int RetentionDays { get; init; }
    public long? MaxRowCount { get; init; }
    public required Qyl.Domains.Ops.Retention.CleanupStrategy CleanupStrategy { get; init; }
    public DateTimeOffset? LastCleanupAt { get; init; }
    public required long RowsCleaned { get; init; }
    public required bool Enabled { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class CompactionStateEntity
{
    public required string Id { get; init; }
    public required string TargetTable { get; init; }
    public DateTimeOffset? HighWatermark { get; init; }
    public DateTimeOffset? LowWatermark { get; init; }
    public required long RowsCompacted { get; init; }
    public required long RowsRemaining { get; init; }
    public DateTimeOffset? LastCompactionAt { get; init; }
    public int? LastDurationMs { get; init; }
    public DateTimeOffset? NextScheduledAt { get; init; }
    public required Qyl.Domains.Ops.Retention.CompactionStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class BackfillJobEntity
{
    public required string Id { get; init; }
    public required Qyl.Domains.Ops.Retention.BackfillJobType JobType { get; init; }
    public required string TargetTable { get; init; }
    public string? Description { get; init; }
    public string? FilterJson { get; init; }
    public required Qyl.Domains.Ops.Retention.BackfillJobStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public long? TotalRows { get; init; }
    public required long ProcessedRows { get; init; }
    public required double ProgressPct { get; init; }
    public required DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int? DurationMs { get; init; }
}

public enum CleanupStrategy
{
    Delete,
    Archive,
    Compact
}

public enum CompactionStatus
{
    Idle,
    Running,
    Scheduled,
    Failed
}

public enum BackfillJobType
{
    Backfill,
    Reindex,
    Repair,
    Migrate
}

public enum BackfillJobStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
