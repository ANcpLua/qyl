using Qyl.Api.Contracts.Workflow;

namespace Qyl.Collector.Storage;

[DuckDbTable("workflow_runs")]
internal sealed partial record WorkflowRunDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    public string? ThreadId { get; init; }

    public string? Title { get; init; }

    public required string Status { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset StartedAt { get; init; }

    [DuckDbColumn(SqlType = "TIMESTAMPTZ")]
    public DateTimeOffset? EndedAt { get; init; }

    [DuckDbColumn(DefaultSql = "0")]
    public ulong LatestJournalSequence { get; init; }

    public string? ActiveAttemptId { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public string? MetadataJson { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset CreatedAt { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset UpdatedAt { get; init; }
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

[DuckDbTable("workflow_content_refs", OnConflict = "ON CONFLICT DO NOTHING")]
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

[DuckDbTable("workflow_projection_nodes")]
internal sealed partial record WorkflowProjectionNodeDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string NodeId { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public required string NodeJson { get; init; }
}

[DuckDbTable("workflow_projection_edges")]
internal sealed partial record WorkflowProjectionEdgeDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 2)]
    public required string EdgeId { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public required string EdgeJson { get; init; }
}

[DuckDbTable("workflow_projection_state")]
internal sealed partial record WorkflowProjectionStateDbRow
{
    [DuckDbColumn(PrimaryKeyOrdinal = 0)]
    public required string ProjectId { get; init; }

    [DuckDbColumn(PrimaryKeyOrdinal = 1)]
    public required string RunId { get; init; }

    public ulong JournalSequence { get; init; }

    [DuckDbColumn(SqlType = "JSON")]
    public required string GraphJson { get; init; }

    [DuckDbColumn(ExcludeFromInsert = true, SqlType = "TIMESTAMPTZ", DefaultSql = "current_timestamp")]
    public DateTimeOffset RebuiltAt { get; init; }
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
    string? MetadataJson);

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

internal sealed record WorkflowProjectionState(
    WorkflowRun Run,
    WorkflowGraphStatistics Statistics,
    ulong JournalSequence,
    int TotalNodeCount,
    int TotalEdgeCount);

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
