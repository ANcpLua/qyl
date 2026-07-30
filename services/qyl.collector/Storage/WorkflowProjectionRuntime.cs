using DuckDB.NET.Data;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Storage;

internal readonly record struct WorkflowProjectionKey(
    string ProjectId,
    string RunId,
    string RunGeneration);

internal sealed record WorkflowProjectionState(
    WorkflowProjectionCheckpoint Checkpoint,
    ulong DurableSequence,
    string? DurableCheckpointId,
    long EstimatedBytes);

internal readonly record struct WorkflowProjectionStep(
    WorkflowProjectionState? State,
    bool Gone);

internal readonly record struct WorkflowProjectionRuntimeSnapshot(
    int WorkerCount,
    int ActiveWorkers,
    int AdmittedDemands,
    int CachedStates,
    long CachedBytes);

internal sealed class WorkflowProjectionRuntime : IAsyncDisposable
{
    private const int MaximumTransientAttempts = 3;

    private readonly object _sync = new();
    private readonly DuckDbStore _store;
    private readonly WorkflowProjectionLimits _limits;
    private readonly ILogger<WorkflowProjectionRuntime> _logger;
    private readonly CancellationTokenSource _shutdown;
    private readonly Channel<ReadyDemand> _ready;
    private readonly Task[] _workers;
    private readonly Dictionary<WorkflowProjectionKey, Demand> _demands = [];
    private readonly Dictionary<WorkflowProjectionKey, CacheEntry> _cache = [];
    private readonly LinkedList<WorkflowProjectionKey> _lru = [];
    private long _cachedBytes;
    private int _activeWorkers;
    private int _disposed;

    public WorkflowProjectionRuntime(
        DuckDbStore store,
        WorkflowProjectionLimits limits,
        ILoggerFactory loggerFactory,
        CancellationToken storeShutdown)
    {
        _store = store;
        _limits = limits;
        _logger = loggerFactory.CreateLogger<WorkflowProjectionRuntime>();
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(storeShutdown);
        _ready = Channel.CreateBounded<ReadyDemand>(new BoundedChannelOptions(
            Math.Max(1, limits.MaxRuntimeDemands))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = limits.RuntimeWorkerCount is 1,
            SingleWriter = false
        });
        _workers = Enumerable.Range(0, Math.Max(1, limits.RuntimeWorkerCount))
            .Select(index => Task.Run(() => WorkerLoopAsync(index)))
            .ToArray();
    }

    public WorkflowProjectionRuntimeSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return new WorkflowProjectionRuntimeSnapshot(
                    _workers.Length,
                    Volatile.Read(ref _activeWorkers),
                    _demands.Count,
                    _cache.Count,
                    _cachedBytes);
            }
        }
    }

    public bool TrySchedule(
        WorkflowProjectionKey key,
        ulong desiredSequence,
        ulong forcePersistThroughSequence = 0)
    {
        desiredSequence = Math.Max(
            desiredSequence,
            forcePersistThroughSequence);
        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) is not 0)
            {
                QylTelemetry.RecordWorkflowLifecycleOutcome(
                    "cancelled",
                    "projection_shutdown");
                return false;
            }
            if (!_demands.TryGetValue(key, out var demand))
            {
                if (_demands.Count >= Math.Max(1, _limits.MaxRuntimeDemands))
                {
                    RecordAdmissionRejection("demand_capacity");
                    return false;
                }
                demand = new Demand(
                    desiredSequence,
                    forcePersistThroughSequence);
                _demands.Add(key, demand);
                demand.Enqueued = _ready.Writer.TryWrite(new ReadyDemand(key, demand));
                if (!demand.Enqueued)
                {
                    _demands.Remove(key);
                    RecordAdmissionRejection("ready_queue_invariant");
                    return false;
                }
                QylTelemetry.RecordWorkflowLifecycleOutcome(
                    "admitted",
                    "scheduled");
                return true;
            }

            demand.DesiredSequence = Math.Max(demand.DesiredSequence, desiredSequence);
            demand.ForcePersistThroughSequence = Math.Max(
                demand.ForcePersistThroughSequence,
                forcePersistThroughSequence);
            return true;
        }
    }

    public Task<WorkflowProjectionCheckpoint> WaitForAsync(
        WorkflowProjectionKey key,
        ulong desiredSequence,
        CancellationToken ct) =>
        WaitForAsync(
            key,
            desiredSequence,
            forcePersistThroughSequence: 0,
            ct);

    public Task<WorkflowProjectionCheckpoint> WaitForAsync(
        WorkflowProjectionKey key,
        ulong desiredSequence,
        ulong forcePersistThroughSequence,
        CancellationToken ct)
    {
        desiredSequence = Math.Max(
            desiredSequence,
            forcePersistThroughSequence);
        ct.ThrowIfCancellationRequested();
        Waiter waiter;
        Task? retirement = null;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) is not 0, this);
            if (TryGetCachedLocked(key, out var cached) &&
                cached.Checkpoint.JournalSequence == desiredSequence &&
                cached.DurableSequence >= forcePersistThroughSequence)
            {
                return Task.FromResult(cached.Checkpoint);
            }

            if (!_demands.TryGetValue(key, out var demand))
            {
                if (_demands.Count >= Math.Max(1, _limits.MaxRuntimeDemands))
                {
                    RecordAdmissionRejection("demand_capacity");
                    return Task.FromException<WorkflowProjectionCheckpoint>(
                        new QylStoreUnavailableException(
                            "Workflow projection admission is at capacity."));
                }
                demand = new Demand(
                    desiredSequence,
                    forcePersistThroughSequence);
                _demands.Add(key, demand);
                demand.Enqueued = _ready.Writer.TryWrite(new ReadyDemand(key, demand));
                if (!demand.Enqueued)
                {
                    _demands.Remove(key);
                    RecordAdmissionRejection("ready_queue_invariant");
                    return Task.FromException<WorkflowProjectionCheckpoint>(
                        new QylStoreUnavailableException(
                            "Workflow projection admission is at capacity."));
                }
                QylTelemetry.RecordWorkflowLifecycleOutcome(
                    "admitted",
                    "waiter");
            }
            else if (demand.Retired)
            {
                retirement = demand.Joined.Task;
                waiter = null!;
            }
            else
            {
                demand.DesiredSequence = Math.Max(demand.DesiredSequence, desiredSequence);
                demand.ForcePersistThroughSequence = Math.Max(
                    demand.ForcePersistThroughSequence,
                    forcePersistThroughSequence);
            }

            if (retirement is null)
            {
                waiter = new Waiter(desiredSequence);
                demand.Add(waiter);
            }
        }

        if (retirement is not null)
        {
            return WaitAfterRetirementAsync(
                key,
                desiredSequence,
                forcePersistThroughSequence,
                retirement,
                ct);
        }
        waiter.ArmCancellation(
            ct,
            () => CancelWaiter(key, waiter, ct));
        return waiter.Task;
    }

    public Task RetireAsync(WorkflowProjectionKey key)
    {
        List<Waiter>? waiters = null;
        Task joined;
        lock (_sync)
        {
            RemoveCachedLocked(key);
            if (!_demands.TryGetValue(key, out var demand))
                return Task.CompletedTask;
            demand.Retired = true;
            QylTelemetry.RecordWorkflowLifecycleOutcome("retired", "requested");
            waiters = demand.TakeAllWaiters();
            if (!demand.Processing && !demand.Enqueued)
            {
                _demands.Remove(key);
                demand.Joined.TrySetResult(true);
            }
            joined = demand.Joined.Task;
        }

        CompleteGone(waiters, key);
        return joined;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;
        _ready.Writer.TryComplete();
        await _shutdown.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }

        List<Waiter> waiters;
        lock (_sync)
        {
            waiters = _demands.Values.SelectMany(static demand => demand.TakeAllWaiters()).ToList();
            foreach (var demand in _demands.Values)
                demand.Joined.TrySetResult(true);
            _demands.Clear();
            _cache.Clear();
            _lru.Clear();
            _cachedBytes = 0;
        }
        foreach (var waiter in waiters)
            waiter.Cancel(_shutdown.Token);
        QylTelemetry.RecordWorkflowLifecycleOutcome(
            "cancelled",
            "projection_shutdown");
        _shutdown.Dispose();
    }

    private async Task WorkerLoopAsync(int workerIndex)
    {
        try
        {
            await foreach (var ready in _ready.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                var key = ready.Key;
                Demand demand;
                WorkflowProjectionState? prior;
                ulong target;
                ulong forcePersistThroughSequence;
                lock (_sync)
                {
                    if (!_demands.TryGetValue(key, out var currentDemand) ||
                        !ReferenceEquals(currentDemand, ready.Demand))
                        continue;
                    currentDemand.Enqueued = false;
                    if (currentDemand.Retired)
                    {
                        _demands.Remove(key);
                        currentDemand.Joined.TrySetResult(true);
                        continue;
                    }
                    if (currentDemand.Processing)
                        continue;
                    demand = currentDemand;
                    demand.Processing = true;
                    prior = TryGetCachedLocked(key, out var cached) ? cached : null;
                    var earliestWaiter = demand.FirstWaiter();
                    if (prior is not null &&
                        earliestWaiter.HasValue &&
                        prior.Checkpoint.JournalSequence > earliestWaiter.Value)
                    {
                        prior = null;
                    }
                    var current = prior?.Checkpoint.JournalSequence ?? 0;
                    target = Math.Min(
                        demand.DesiredSequence,
                        checked(current + (ulong)Math.Max(1, _limits.RuntimeEventQuantum)));
                    var waiterSequence = demand.FirstWaiterAtOrAfter(current);
                    if (waiterSequence.HasValue)
                        target = Math.Min(target, waiterSequence.Value);
                    forcePersistThroughSequence =
                        demand.ForcePersistThroughSequence;
                }

                Interlocked.Increment(ref _activeWorkers);
                try
                {
                    var step = await _store.ProjectWorkflowQuantumAsync(
                        key,
                        prior,
                        target,
                        forcePersistThroughSequence,
                        _shutdown.Token).ConfigureAwait(false);
                    if (step.Gone)
                    {
                        CompleteDemandAsGone(key, demand);
                        continue;
                    }
                    if (step.State is null)
                        throw new InvalidOperationException("Workflow projection produced no state.");
                    CompleteSuccessfulQuantum(key, demand, step.State);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    CompleteDemandWithCancellation(key, demand, _shutdown.Token);
                }
                catch (Exception error)
                {
                    if (IsTransient(error) && RetryDemand(key, demand, error))
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(25 * demand.TransientAttempts),
                            _shutdown.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        CompleteDemandWithError(key, demand, error);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _activeWorkers);
                    var requeue = false;
                    lock (_sync)
                    {
                        if (_demands.TryGetValue(key, out var current) &&
                            ReferenceEquals(current, demand))
                        {
                            current.Processing = false;
                            if (current.Retired)
                            {
                                _demands.Remove(key);
                                current.Joined.TrySetResult(true);
                            }
                            else
                            {
                                requeue = !current.Enqueued;
                            }
                        }
                    }
                    if (requeue)
                        Requeue(key, demand);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            var reason = error.GetType().Name;
            WorkflowLifecycleLog.ProjectionWorkerFailed(
                _logger,
                workerIndex,
                reason);
            QylTelemetry.RecordWorkflowLifecycleOutcome(
                "failed",
                $"worker_{reason}");
        }
    }

    private async Task<WorkflowProjectionCheckpoint> WaitAfterRetirementAsync(
        WorkflowProjectionKey key,
        ulong desiredSequence,
        ulong forcePersistThroughSequence,
        Task retirement,
        CancellationToken ct)
    {
        await retirement.WaitAsync(ct).ConfigureAwait(false);
        if (!await _store.IsWorkflowProjectionGenerationCurrentAsync(key, ct)
                .ConfigureAwait(false))
        {
            throw Gone(key);
        }
        return await WaitForAsync(
                key,
                desiredSequence,
                forcePersistThroughSequence,
                ct)
            .ConfigureAwait(false);
    }

    private void CompleteSuccessfulQuantum(
        WorkflowProjectionKey key,
        Demand demand,
        WorkflowProjectionState state)
    {
        List<Waiter> completed;
        lock (_sync)
        {
            if (!_demands.TryGetValue(key, out var current) ||
                !ReferenceEquals(current, demand) ||
                current.Retired)
                return;
            AddCachedLocked(key, state);
            completed = current.TakeWaitersAt(state.Checkpoint.JournalSequence);
            current.TransientAttempts = 0;
            if (state.Checkpoint.JournalSequence >= current.DesiredSequence &&
                state.DurableSequence >= current.ForcePersistThroughSequence &&
                current.WaiterCount is 0)
            {
                _demands.Remove(key);
                current.Joined.TrySetResult(true);
                TrimIdleCacheLocked();
                QylTelemetry.RecordWorkflowLifecycleOutcome(
                    "completed",
                    "projection_head");
            }
        }

        foreach (var waiter in completed)
            waiter.Complete(state.Checkpoint);
    }

    private void Requeue(WorkflowProjectionKey key, Demand expected)
    {
        List<Waiter>? rejected = null;
        lock (_sync)
        {
            if (!_demands.TryGetValue(key, out var demand) ||
                !ReferenceEquals(demand, expected) ||
                demand.Retired ||
                demand.Enqueued ||
                demand.Processing)
                return;
            demand.Enqueued = _ready.Writer.TryWrite(new ReadyDemand(key, demand));
            if (!demand.Enqueued)
            {
                rejected = demand.TakeAllWaiters();
                _demands.Remove(key);
                demand.Joined.TrySetResult(true);
            }
        }
        if (rejected is null)
            return;
        var error = new InvalidOperationException(
            "An admitted workflow projection lost its reserved ready-queue capacity.");
        WorkflowLifecycleLog.ProjectionFailed(
            _logger,
            "ready_queue_invariant");
        QylTelemetry.RecordWorkflowLifecycleOutcome(
            "failed",
            "ready_queue_invariant");
        foreach (var waiter in rejected)
            waiter.Fail(error);
    }

    private bool RetryDemand(
        WorkflowProjectionKey key,
        Demand expected,
        Exception error)
    {
        lock (_sync)
        {
            if (!_demands.TryGetValue(key, out var demand) ||
                !ReferenceEquals(demand, expected) ||
                demand.Retired)
                return false;
            demand.TransientAttempts++;
            var reason = error.GetType().Name;
            if (demand.TransientAttempts <= MaximumTransientAttempts)
            {
                WorkflowLifecycleLog.ProjectionRetry(
                    _logger,
                    demand.TransientAttempts,
                    reason);
                QylTelemetry.RecordWorkflowLifecycleOutcome(
                    "retry",
                    reason);
            }
            return demand.TransientAttempts <= MaximumTransientAttempts;
        }
    }

    private void CompleteDemandAsGone(WorkflowProjectionKey key, Demand expected)
    {
        List<Waiter> waiters;
        Demand demand;
        lock (_sync)
        {
            RemoveCachedLocked(key);
            if (!_demands.TryGetValue(key, out demand) ||
                !ReferenceEquals(demand, expected))
                return;
            _demands.Remove(key);
            waiters = demand.TakeAllWaiters();
            demand.Joined.TrySetResult(true);
        }
        CompleteGone(waiters, key);
        QylTelemetry.RecordWorkflowLifecycleOutcome("gone", "generation");
    }

    private void CompleteDemandWithCancellation(
        WorkflowProjectionKey key,
        Demand expected,
        CancellationToken token)
    {
        List<Waiter> waiters;
        Demand demand;
        lock (_sync)
        {
            if (!_demands.TryGetValue(key, out demand) ||
                !ReferenceEquals(demand, expected))
                return;
            _demands.Remove(key);
            waiters = demand.TakeAllWaiters();
            demand.Joined.TrySetResult(true);
        }
        foreach (var waiter in waiters)
            waiter.Cancel(token);
        QylTelemetry.RecordWorkflowLifecycleOutcome(
            "cancelled",
            "projection_shutdown");
    }

    private void CompleteDemandWithError(
        WorkflowProjectionKey key,
        Demand expected,
        Exception error)
    {
        List<Waiter> waiters;
        Demand demand;
        lock (_sync)
        {
            if (!_demands.TryGetValue(key, out demand) ||
                !ReferenceEquals(demand, expected))
                return;
            _demands.Remove(key);
            waiters = demand.TakeAllWaiters();
            demand.Joined.TrySetResult(true);
            TrimIdleCacheLocked();
        }
        var reason = error.GetType().Name;
        WorkflowLifecycleLog.ProjectionFailed(_logger, reason);
        QylTelemetry.RecordWorkflowLifecycleOutcome("failed", reason);
        foreach (var waiter in waiters)
            waiter.Fail(error);
    }

    private void CancelWaiter(
        WorkflowProjectionKey key,
        Waiter waiter,
        CancellationToken token)
    {
        lock (_sync)
        {
            if (_demands.TryGetValue(key, out var demand))
            {
                var removed = demand.Remove(waiter);
                var hasCachedState = TryGetCachedLocked(key, out var state);
                var cachedSequence = hasCachedState
                    ? state.Checkpoint.JournalSequence
                    : 0;
                var durableSequence = hasCachedState
                    ? state.DurableSequence
                    : 0;
                if (removed &&
                    !demand.Processing &&
                    !demand.Enqueued &&
                    demand.WaiterCount is 0 &&
                    demand.DesiredSequence <= cachedSequence &&
                    demand.ForcePersistThroughSequence <= durableSequence)
                {
                    _demands.Remove(key);
                    demand.Joined.TrySetResult(true);
                }
            }
        }
        waiter.Cancel(token);
    }

    private bool TryGetCachedLocked(
        WorkflowProjectionKey key,
        out WorkflowProjectionState state)
    {
        if (!_cache.TryGetValue(key, out var entry))
        {
            state = null!;
            return false;
        }
        _lru.Remove(entry.Node);
        _lru.AddFirst(entry.Node);
        state = entry.State;
        return true;
    }

    private void AddCachedLocked(
        WorkflowProjectionKey key,
        WorkflowProjectionState state)
    {
        if (state.EstimatedBytes > _limits.MaxRuntimeCacheBytes)
        {
            throw new WorkflowProjectionLimitExceededException(
                "Workflow projection state exceeds the runtime cache byte budget.");
        }
        RemoveCachedLocked(key);
        while (_cachedBytes + state.EstimatedBytes > _limits.MaxRuntimeCacheBytes)
        {
            var candidate = _lru.Last;
            while (candidate is not null && _demands.ContainsKey(candidate.Value))
                candidate = candidate.Previous;
            if (candidate is null)
            {
                throw new QylStoreUnavailableException(
                    "Workflow projection runtime cache has no idle state available for eviction.");
            }
            RemoveCachedLocked(candidate.Value);
        }
        var node = _lru.AddFirst(key);
        _cache.Add(key, new CacheEntry(state, node));
        _cachedBytes = checked(_cachedBytes + state.EstimatedBytes);
    }

    private void RemoveCachedLocked(WorkflowProjectionKey key)
    {
        if (!_cache.Remove(key, out var entry))
            return;
        _lru.Remove(entry.Node);
        _cachedBytes -= entry.State.EstimatedBytes;
    }

    private void TrimIdleCacheLocked()
    {
        while (_cachedBytes > _limits.MaxRuntimeCacheBytes && _lru.Last is not null)
        {
            var candidate = _lru.Last;
            while (candidate is not null && _demands.ContainsKey(candidate.Value))
                candidate = candidate.Previous;
            if (candidate is null)
                return;
            RemoveCachedLocked(candidate.Value);
        }
    }

    private static bool IsTransient(Exception error) =>
        error is IOException or DuckDBException or QylStoreUnavailableException;

    private void RecordAdmissionRejection(string reason)
    {
        WorkflowLifecycleLog.ProjectionAdmissionRejected(_logger, reason);
        QylTelemetry.RecordWorkflowLifecycleOutcome("rejected", reason);
    }

    private static void CompleteGone(
        IEnumerable<Waiter> waiters,
        WorkflowProjectionKey key)
    {
        var error = Gone(key);
        foreach (var waiter in waiters)
            waiter.Fail(error);
    }

    private static KeyNotFoundException Gone(WorkflowProjectionKey key) =>
        new($"Workflow run '{key.RunId}' generation '{key.RunGeneration}' is gone.");

    private sealed class Demand(
        ulong desiredSequence,
        ulong forcePersistThroughSequence)
    {
        private readonly SortedDictionary<ulong, List<Waiter>> _waiters = [];

        public ulong DesiredSequence { get; set; } = desiredSequence;

        public ulong ForcePersistThroughSequence { get; set; } =
            forcePersistThroughSequence;

        public bool Enqueued { get; set; }

        public bool Processing { get; set; }

        public bool Retired { get; set; }

        public int TransientAttempts { get; set; }

        public int WaiterCount { get; private set; }

        public TaskCompletionSource<bool> Joined { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Add(Waiter waiter)
        {
            if (!_waiters.TryGetValue(waiter.Sequence, out var sequenceWaiters))
            {
                sequenceWaiters = [];
                _waiters.Add(waiter.Sequence, sequenceWaiters);
            }
            sequenceWaiters.Add(waiter);
            WaiterCount++;
        }

        public bool Remove(Waiter waiter)
        {
            if (!_waiters.TryGetValue(waiter.Sequence, out var sequenceWaiters) ||
                !sequenceWaiters.Remove(waiter))
                return false;
            WaiterCount--;
            if (sequenceWaiters.Count is 0)
                _waiters.Remove(waiter.Sequence);
            return true;
        }

        public ulong? FirstWaiter() =>
            _waiters.Count is 0 ? null : _waiters.First().Key;

        public ulong? FirstWaiterAtOrAfter(ulong sequence)
        {
            foreach (var candidate in _waiters.Keys)
            {
                if (candidate >= sequence)
                    return candidate;
            }
            return null;
        }

        public List<Waiter> TakeWaitersAt(ulong sequence)
        {
            var completed = new List<Waiter>();
            if (_waiters.Remove(sequence, out var sequenceWaiters))
            {
                completed.AddRange(sequenceWaiters);
                WaiterCount -= sequenceWaiters.Count;
            }
            return completed;
        }

        public List<Waiter> TakeAllWaiters()
        {
            var waiters = _waiters.Values.SelectMany(static value => value).ToList();
            _waiters.Clear();
            WaiterCount = 0;
            return waiters;
        }
    }

    private sealed class Waiter(ulong sequence)
    {
        private readonly TaskCompletionSource<WorkflowProjectionCheckpoint> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _cancellationRegistration;

        public ulong Sequence { get; } = sequence;

        public Task<WorkflowProjectionCheckpoint> Task => _completion.Task;

        public void ArmCancellation(CancellationToken token, Action cancel)
        {
            _cancellationRegistration = token.Register(cancel);
            if (_completion.Task.IsCompleted)
                _cancellationRegistration.Dispose();
        }

        public void Complete(WorkflowProjectionCheckpoint checkpoint)
        {
            _cancellationRegistration.Dispose();
            _completion.TrySetResult(checkpoint);
        }

        public void Fail(Exception error)
        {
            _cancellationRegistration.Dispose();
            _completion.TrySetException(error);
        }

        public void Cancel(CancellationToken token)
        {
            _cancellationRegistration.Dispose();
            _completion.TrySetCanceled(token);
        }
    }

    private sealed record CacheEntry(
        WorkflowProjectionState State,
        LinkedListNode<WorkflowProjectionKey> Node);

    private readonly record struct ReadyDemand(
        WorkflowProjectionKey Key,
        Demand Demand);
}

internal static class WorkflowProjectionMemory
{
    public static long Estimate(WorkflowProjectionCheckpoint checkpoint)
    {
        long bytes = 2048;
        foreach (var node in checkpoint.Graph.Nodes)
        {
            bytes = checked(bytes + 256L +
                Characters(node.NodeId) +
                Characters(node.Label) +
                Characters(node.Status) +
                Characters(node.AttemptId) +
                Characters(node.AgentId) +
                (node.ContentRefs?.Sum(static value => 32L + Characters(value)) ?? 0));
        }
        foreach (var edge in checkpoint.Graph.Edges)
        {
            bytes = checked(bytes + 192L +
                Characters(edge.EdgeId) +
                Characters(edge.SourceNodeId) +
                Characters(edge.TargetNodeId) +
                ProvenanceBytes(edge.Provenance));
        }
        foreach (var path in checkpoint.ReplayState.PathWrites)
        {
            bytes = checked(bytes + 128L + Characters(path.PathKey) +
                path.Witnesses.Sum(static witness =>
                    64L + Characters(witness.NodeId) + Characters(witness.EventId)));
        }
        return bytes;
    }

    private static long Characters(string? value) => value?.Length * 2L ?? 0;

    private static long ProvenanceBytes(WorkflowEdgeProvenance provenance) =>
        provenance switch
        {
            RecordedWorkflowEdgeProvenance recorded =>
                recorded.EventIds.Sum(static value => 32L + Characters(value)),
            DerivedWorkflowEdgeProvenance derived =>
                derived.EventIds.Sum(static value => 32L + Characters(value)) +
                Characters(derived.Evidence),
            _ => 0
        };
}
