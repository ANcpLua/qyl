#nullable enable

namespace Qyl.Domains.Agent.Checkpoint;

public sealed class WorkflowCheckpointEntity
{
    public required string CheckpointId { get; init; }
    public required string ExecutionId { get; init; }
    public required string NodeId { get; init; }
    public required string StateJson { get; init; }
    public required int SequenceNumber { get; init; }
    public required long CreatedAtUnixNano { get; init; }
}
