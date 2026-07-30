using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Qyl.Collector.Workflow;

internal sealed class WorkflowProjectionLimits(
    int maxEventsPerRun = 10_000,
    long maxSerializedInputBytes = 32L * 1024 * 1024,
    int maxNodes = 20_000,
    int maxEdges = 100_000,
    long maxWorkUnits = 250_000,
    int maxCheckpointBytes = 64 * 1024 * 1024,
    long maxRuntimeCacheBytes = 128L * 1024 * 1024,
    int maxRuntimeDemands = 1024,
    int runtimeWorkerCount = 2,
    int runtimeEventQuantum = 256,
    int checkpointSweepLimit = 10_000,
    long checkpointReconciliationByteLimit = 64L * 1024 * 1024,
    int checkpointReconciliationMilliseconds = 100)
{
    public int MaxEventsPerRun { get; } = maxEventsPerRun;

    public long MaxSerializedInputBytes { get; } = maxSerializedInputBytes;

    public int MaxNodes { get; } = maxNodes;

    public int MaxEdges { get; } = maxEdges;

    public long MaxWorkUnits { get; } = maxWorkUnits;

    public int MaxCheckpointBytes { get; } = maxCheckpointBytes;

    public long MaxRuntimeCacheBytes { get; } = maxRuntimeCacheBytes;

    public int MaxRuntimeDemands { get; } = maxRuntimeDemands;

    public int RuntimeWorkerCount { get; } = runtimeWorkerCount;

    public int RuntimeEventQuantum { get; } = runtimeEventQuantum;

    public int CheckpointSweepLimit { get; } = checkpointSweepLimit;

    public long CheckpointReconciliationByteLimit { get; } =
        Math.Max(1, checkpointReconciliationByteLimit);

    public TimeSpan CheckpointReconciliationTimeLimit { get; } =
        TimeSpan.FromMilliseconds(Math.Max(1, checkpointReconciliationMilliseconds));

    public string ConfigurationFingerprint { get; } = Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            $"events={maxEventsPerRun.ToString(CultureInfo.InvariantCulture)}",
            $"input={maxSerializedInputBytes.ToString(CultureInfo.InvariantCulture)}",
            $"nodes={maxNodes.ToString(CultureInfo.InvariantCulture)}",
            $"edges={maxEdges.ToString(CultureInfo.InvariantCulture)}",
            $"work={maxWorkUnits.ToString(CultureInfo.InvariantCulture)}",
            $"checkpoint={maxCheckpointBytes.ToString(CultureInfo.InvariantCulture)}"))));
}

internal sealed class WorkflowProjectionBudget(WorkflowProjectionLimits limits)
{
    private long _workUnits;

    public WorkflowProjectionLimits Limits { get; } = limits;

    public void ChargeWork(long units = 1)
    {
        _workUnits = checked(_workUnits + units);
        if (_workUnits > Limits.MaxWorkUnits)
            throw Exceeded("projection work", Limits.MaxWorkUnits);
    }

    public void EnsureEventCount(long count)
    {
        if (count > Limits.MaxEventsPerRun)
            throw Exceeded("journal events", Limits.MaxEventsPerRun);
    }

    public void EnsureSerializedInput(long bytes)
    {
        if (bytes < 0)
            throw new InvalidDataException("Workflow projection serialized input counter is invalid.");
        if (bytes > Limits.MaxSerializedInputBytes)
            throw Exceeded("serialized projection input", Limits.MaxSerializedInputBytes);
    }

    public void EnsureGraphSize(int nodeCount, int edgeCount)
    {
        if (nodeCount > Limits.MaxNodes)
            throw Exceeded("graph nodes", Limits.MaxNodes);
        if (edgeCount > Limits.MaxEdges)
            throw Exceeded("graph edges", Limits.MaxEdges);
    }

    private static WorkflowProjectionLimitExceededException Exceeded(string dimension, long maximum) =>
        new($"Workflow projection {dimension} exceeds the configured maximum of {maximum}.");
}

internal sealed class WorkflowProjectionLimitExceededException(string message) : Exception(message);
