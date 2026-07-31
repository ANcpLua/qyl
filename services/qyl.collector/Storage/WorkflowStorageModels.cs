using Qyl.Api.Contracts.Workflow;

namespace Qyl.Collector.Storage;

[DuckDbTable("workflow_runs", Indexes = "ActiveCheckpointStorageKey")]
internal sealed partial record WorkflowRunDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(DefaultSql = "replace(lower(uuid()::VARCHAR), '-', '')")]
    public required string RunGeneration { get; init; }

    public string? ThreadId { get; init; }

    public string? Title { get; init; }

    public required string Status { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset StartedAt { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset? EndedAt { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public ulong LatestJournalSequence { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public long EventCount { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public long ProjectionInputBytes { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public long ImmutableProjectionInputBytes { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public long DynamicProjectionInputBytes { get; init; }

    public string? ActiveAttemptId { get; init; }

    [DuckDbColumn(DefaultSql = "1")]
    public ulong NextCommandSequence { get; init; }

    [DuckDbColumn(DefaultSql = "1")]
    public ulong NextControlEventSourceSequence { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public ulong ActiveCheckpointSequence { get; init; }

    public string? ActiveCheckpointId { get; init; }

    public string? ActiveCheckpointStorageKey { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public ulong CheckpointManifestEpoch { get; init; }

    public ulong? ProjectionFailureSequence { get; init; }

    public string? ProjectionFailureKind { get; init; }

    public string? ProjectionFailureConfiguration { get; init; }

    public string? ProjectionFailureSemantic { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? MetadataJson { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset CreatedAt { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset UpdatedAt { get; init; }

    [DuckDbColumn(
        ExcludeFromInsert = true,
        SqlType = "TIMESTAMPTZ",
        DefaultSql = "current_timestamp",
        OmitDefaultFromMigration = true)]
    public DateTimeOffset LastActivityAt { get; init; }
}

[DuckDbTable(
    "workflow_events",
    UniqueIndexes = "ProjectId,RunId,EventId;ProjectId,RunId,ClientId,SourceSequence")]
internal sealed partial record WorkflowEventDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public ulong JournalSequence { get; init; }

    public required string EventId { get; init; }

    public required string ClientId { get; init; }

    public ulong SourceSequence { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset EventTime { get; init; }

    public required string Kind { get; init; }

    public string? ThreadId { get; init; }

    public string? TurnId { get; init; }

    public string? AttemptId { get; init; }

    public string? AgentId { get; init; }

    public string? ParentAgentId { get; init; }

    public string? ReceiverAgentId { get; init; }

    public string? ToolCallId { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public required string ContentRefsJson { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? DataJson { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset CreatedAt { get; init; }
}

[DuckDbTable("workflow_content", OnConflict = "ON CONFLICT DO NOTHING")]
internal sealed partial record WorkflowContentDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string ContentRef { get; init; }

    public required string ContentType { get; init; }

    public required string Encoding { get; init; }

    public required byte[] Nonce { get; init; }

    public required byte[] Tag { get; init; }

    public required byte[] Ciphertext { get; init; }

    public long UncompressedSize { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset CreatedAt { get; init; }
}

[DuckDbTable(
    "workflow_content_refs",
    OnConflict = "ON CONFLICT DO NOTHING",
    Indexes = "ProjectId,RunId,ContentRef")]
internal sealed partial record WorkflowContentReferenceDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string EventId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 3)]
    public required string ContentRef { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset CreatedAt { get; init; }
}

[DuckDbTable("workflow_client_journal")]
internal sealed partial record WorkflowClientJournalDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string ClientId { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public ulong AcknowledgedSourceSequence { get; init; }
}

[DuckDbTable(
    "workflow_client_journal_ranges",
    Indexes = "ProjectId,RunId,ClientId,RangeEnd")]
internal sealed partial record WorkflowClientJournalRangeDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string ClientId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 3)]
    public ulong RangeStart { get; init; }

    public ulong RangeEnd { get; init; }
}

[DuckDbTable(
    "workflow_commands",
    UniqueIndexes = "ProjectId,RunId,IdempotencyKey")]
internal sealed partial record WorkflowCommandDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string CommandId { get; init; }

    public ulong CommandSequence { get; init; }

    public required string Action { get; init; }

    public required string Status { get; init; }

    public required string IdempotencyKey { get; init; }

    public string? Input { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset RequestedAt { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset UpdatedAt { get; init; }

    public string? Error { get; init; }
}

internal sealed record WorkflowRunStorageRow(
    string ProjectId,
    string RunId,
    string? ThreadId,
    string? Title,
    WorkflowRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    ulong LatestJournalSequence,
    string? ActiveAttemptId,
    string? MetadataJson,
    long EventCount = 0,
    long ProjectionInputBytes = 0,
    long ImmutableProjectionInputBytes = 0,
    long DynamicProjectionInputBytes = 0,
    ulong NextCommandSequence = 1,
    ulong NextControlEventSourceSequence = 1,
    ulong ActiveCheckpointSequence = 0,
    string? ActiveCheckpointId = null,
    string? ActiveCheckpointStorageKey = null,
    ulong CheckpointManifestEpoch = 0,
    ulong? ProjectionFailureSequence = null,
    string? ProjectionFailureKind = null,
    string? ProjectionFailureConfiguration = null,
    string RunGeneration = "",
    string? ProjectionFailureSemantic = null,
    DateTimeOffset LastActivityAt = default);

internal sealed record WorkflowEventWrite(
    string EventId,
    ulong SourceSequence,
    DateTimeOffset Timestamp,
    WorkflowJournalEventKind Kind,
    string? ThreadId,
    string? TurnId,
    string? AttemptId,
    string? AgentId,
    string? ParentAgentId,
    string? ReceiverAgentId,
    string? ToolCallId,
    IReadOnlyList<string> ContentRefs,
    string? DataJson);

internal sealed record WorkflowEventStorageRow(
    string ProjectId,
    string RunId,
    ulong JournalSequence,
    string EventId,
    string ClientId,
    ulong SourceSequence,
    DateTimeOffset Timestamp,
    WorkflowJournalEventKind Kind,
    string? ThreadId,
    string? TurnId,
    string? AttemptId,
    string? AgentId,
    string? ParentAgentId,
    string? ReceiverAgentId,
    string? ToolCallId,
    IReadOnlyList<string> ContentRefs,
    string? DataJson);

internal sealed record WorkflowContentWrite(
    string ContentRef,
    string ContentType,
    WorkflowContentEncoding Encoding,
    string Content);

internal sealed record WorkflowContentStorageRow(
    string ContentRef,
    string ContentType,
    WorkflowContentEncoding Encoding,
    byte[] Nonce,
    byte[] Tag,
    byte[] Ciphertext,
    long SizeBytes);

internal sealed record WorkflowContentReadRow(
    string ContentRef,
    string ContentType,
    WorkflowContentEncoding Encoding,
    string Content,
    long SizeBytes);

internal sealed record WorkflowAppendResult(
    int AcceptedCount,
    int DuplicateCount,
    ulong AcknowledgedSourceSequence,
    ulong? FirstJournalSequence,
    ulong? LastJournalSequence);

internal sealed record WorkflowEventStoragePage(
    IReadOnlyList<WorkflowEventStorageRow> Events,
    ulong NextSequence,
    ulong HighWaterMark,
    bool CursorGap);

internal sealed record WorkflowProjectionNodeState(
    string NodeId,
    WorkflowNodeKind Kind,
    string Label,
    string Status,
    string? AttemptId,
    string? AgentId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<string> ContentRefs);

internal sealed record WorkflowProjectionOwnerCursor(string OwnerNodeId, string NodeId);

internal sealed record WorkflowProjectionWriteWitness(string NodeId, string EventId);

internal sealed record WorkflowProjectionPathWrites(
    string PathKey,
    IReadOnlyList<WorkflowProjectionWriteWitness> Witnesses);

internal sealed record WorkflowProjectionReplayState(
    string? ActiveAttemptId,
    IReadOnlyList<WorkflowProjectionNodeState> Nodes,
    IReadOnlyList<WorkflowGraphEdge> Edges,
    IReadOnlyList<WorkflowProjectionOwnerCursor> OwnerCursors,
    IReadOnlyList<WorkflowProjectionPathWrites> PathWrites);

internal sealed record WorkflowProjectionCheckpoint(
    int FormatVersion,
    string ProjectId,
    string RunId,
    string RunGeneration,
    string ProjectorSemanticFingerprint,
    string ProjectionConfigurationFingerprint,
    string RunInputHash,
    ulong JournalSequence,
    DateTimeOffset ProjectionTime,
    WorkflowProjectionReplayState ReplayState,
    WorkflowGraphSnapshot Graph);

internal sealed record WorkflowControlCommandStorageRow(
    string ProjectId,
    string RunId,
    string CommandId,
    ulong CommandSequence,
    WorkflowControlAction Action,
    WorkflowControlStatus Status,
    string IdempotencyKey,
    string? Input,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt,
    string? Error);

internal sealed record WorkflowControlCommandStoragePage(
    IReadOnlyList<WorkflowControlCommandStorageRow> Commands,
    ulong NextSequence);

internal readonly record struct WorkflowRetentionResult(
    int Runs,
    int Events,
    int Commands,
    int Content);

internal sealed class WorkflowRunConflictException(string message) : Exception(message);

internal sealed class WorkflowEventConflictException(string message) : Exception(message);

internal sealed class WorkflowControlConflictException(string message) : Exception(message);

// Applied storage migrations. The identifier is the migration's own name, so a
// migration that has already run is recognised by presence alone.
[DuckDbTable("qyl_storage_migrations")]
internal sealed partial record QylStorageMigrationDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string MigrationId { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset AppliedAt { get; init; }
}

// Runs whose checkpoint manifest was found broken and owe a rebuild. The row is
// deleted by the publication that replaces the manifest.
[DuckDbTable("workflow_checkpoint_repairs")]
internal sealed partial record WorkflowCheckpointRepairDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string RunGeneration { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public ulong LatestJournalSequence { get; init; }
}

// Single-row monotonic clock ordering manifest mutations against a sweep cycle.
// The primary key pins the single row: every read and write addresses
// `singleton = 0`, so no second row can be introduced.
[DuckDbTable("workflow_checkpoint_clock")]
internal sealed partial record WorkflowCheckpointClockDbRow
{
    [DuckDbColumn(SqlType = "UTINYINT", PrimaryKeyOrdinal = 0)]
    public byte Singleton { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public ulong CurrentEpoch { get; init; }
}
