using DuckDB.NET.Data;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Workflow;

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

// One projection quantum ends in exactly one of three states. Rotated carries
// the successor key so the runtime can hand its waiters to the live generation
// instead of reporting a rotation as a missing run.
internal abstract record WorkflowProjectionStep
{
    private WorkflowProjectionStep()
    {
    }

    internal sealed record Advanced(WorkflowProjectionState State) : WorkflowProjectionStep;

    internal sealed record Rotated(WorkflowProjectionKey Successor) : WorkflowProjectionStep;

    internal sealed record Gone : WorkflowProjectionStep
    {
        public static readonly Gone Instance = new();
    }
}

internal readonly record struct WorkflowProjectionRuntimeSnapshot(
    int WorkerCount,
    int ActiveWorkers,
    int AdmittedDemands,
    int CachedStates,
    long CachedBytes);

internal sealed class WorkflowProjectionRuntime : IAsyncDisposable
{
    private const int MaximumTransientAttempts = 3;

    private readonly Lock _sync = new();
    private readonly DuckDbStore _store;
    private readonly WorkflowProjectionLimits _limits;
    private readonly ILogger<WorkflowProjectionRuntime> _logger;
    private readonly CancellationTokenSource _shutdown;
    private readonly Channel<ReadyDemand> _ready;
    private readonly Task[] _workers;
    private readonly Dictionary<WorkflowProjectionKey, Demand> _demands = [];
    private readonly Dictionary<WorkflowProjectionKey, CacheEntry> _cache = [];
    private readonly LinkedList<WorkflowProjectionKey> _lru = [];
    private readonly List<Task> _resumes = [];
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
            .Select(_ => Task.Run(WorkerLoopAsync))
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
                return true;
            }

            demand.DesiredSequence = Math.Max(demand.DesiredSequence, desiredSequence);
            demand.ForcePersistThroughSequence = Math.Max(
                demand.ForcePersistThroughSequence,
                forcePersistThroughSequence);
            return true;
        }
    }

    public Task<WorkflowProjectionCheckpoint?> WaitForAsync(
        WorkflowProjectionKey key,
        ulong desiredSequence,
        CancellationToken ct) =>
        WaitForAsync(
            key,
            desiredSequence,
            forcePersistThroughSequence: 0,
            ct);

    public Task<WorkflowProjectionCheckpoint?> WaitForAsync(
        WorkflowProjectionKey key,
        ulong desiredSequence,
        ulong forcePersistThroughSequence,
        CancellationToken ct)
    {
        desiredSequence = Math.Max(
            desiredSequence,
            forcePersistThroughSequence);
        ct.ThrowIfCancellationRequested();
        Waiter? waiter = null;
        Task? retirement = null;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) is not 0, this);
            if (TryGetCachedLocked(key, out var cached) &&
                cached.Checkpoint.JournalSequence == desiredSequence &&
                cached.DurableSequence >= forcePersistThroughSequence)
            {
                return Task.FromResult<WorkflowProjectionCheckpoint?>(cached.Checkpoint);
            }

            if (!_demands.TryGetValue(key, out var demand))
            {
                if (_demands.Count >= Math.Max(1, _limits.MaxRuntimeDemands))
                {
                    RecordAdmissionRejection("demand_capacity");
                    return Task.FromException<WorkflowProjectionCheckpoint?>(
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
                    return Task.FromException<WorkflowProjectionCheckpoint?>(
                        new QylStoreUnavailableException(
                            "Workflow projection admission is at capacity."));
                }
            }
            else if (demand.Retired)
            {
                retirement = demand.Joined.Task;
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
                waiter = new Waiter(desiredSequence, ct);
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
        var armed = waiter!;
        armed.ArmCancellation(() => CancelWaiter(key, armed, ct));
        return armed.Task;
    }

    public Task RetireAsync(WorkflowProjectionKey key)
    {
        List<Waiter> waiters;
        Task joined;
        lock (_sync)
        {
            RemoveCachedLocked(key);
            if (!_demands.TryGetValue(key, out var demand))
                return Task.CompletedTask;
            demand.Retired = true;
            waiters = demand.TakeAllWaiters();
            if (!demand.Processing && !demand.Enqueued)
            {
                _demands.Remove(key);
                demand.Joined.TrySetResult(true);
            }
            joined = demand.Joined.Task;
        }

        // Retirement drops the in-flight demand so a repair can rebuild from a
        // clean slate. The waiters belong to the run, not to the demand, so they
        // are re-admitted once retirement settles rather than failed.
        ResumeWaiters(waiters, key, joined);
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
            // Shutdown cancels the workers by design; their cancellation is the
            // expected outcome here, not a fault to report.
        }

        Task[] resumes;
        List<Waiter> waiters;
        lock (_sync)
        {
            resumes = [.. _resumes];
            _resumes.Clear();
            waiters = _demands.Values.SelectMany(static demand => demand.TakeAllWaiters()).ToList();
            foreach (var demand in _demands.Values)
                demand.Joined.TrySetResult(true);
            _demands.Clear();
            _cache.Clear();
            _lru.Clear();
            _cachedBytes = 0;
        }
        // Resumed waiters own their own completion, so shutdown waits for them
        // rather than leaving their tasks unobserved.
        try
        {
            await Task.WhenAll(resumes).WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is TimeoutException or OperationCanceledException)
        {
            // Shutdown is bounded; a waiter that has not settled is cancelled below.
        }
        foreach (var waiter in waiters)
            waiter.Cancel(_shutdown.Token);
        _shutdown.Dispose();
    }

    // Claims the next quantum for a ready token, or returns null when the token
    // is stale, retired or already being processed. Kept separate so the worker
    // can resolve a failure here against the demand that caused it.
    private QuantumStart? TryBeginQuantum(ReadyDemand ready)
    {
        lock (_sync)
        {
            if (!_demands.TryGetValue(ready.Key, out var demand) ||
                !ReferenceEquals(demand, ready.Demand))
                return null;
            demand.Enqueued = false;
            if (demand.Retired)
            {
                _demands.Remove(ready.Key);
                demand.Joined.TrySetResult(true);
                return null;
            }
            if (demand.Processing)
                return null;
            demand.Processing = true;
            var prior = TryGetCachedLocked(ready.Key, out var cached) ? cached : null;
            var earliestWaiter = demand.FirstWaiter();
            if (prior is not null &&
                earliestWaiter.HasValue &&
                prior.Checkpoint.JournalSequence > earliestWaiter.Value)
            {
                prior = null;
            }
            var current = prior?.Checkpoint.JournalSequence ?? 0;
            var target = Math.Min(
                demand.DesiredSequence,
                checked(current + (ulong)Math.Max(1, _limits.RuntimeEventQuantum)));
            var waiterSequence = demand.FirstWaiterAtOrAfter(current);
            if (waiterSequence.HasValue)
                target = Math.Min(target, waiterSequence.Value);
            return new QuantumStart(
                demand,
                prior,
                target,
                demand.ForcePersistThroughSequence);
        }
    }

    // Every failure is resolved against the demand that produced it, so nothing
    // can escape to the loop and strand this worker. The only exit is the
    // channel read being cancelled or completed at shutdown.
    private async Task WorkerLoopAsync()
    {
        try
        {
            await foreach (var ready in _ready.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                var key = ready.Key;
                QuantumStart? claimed;
                try
                {
                    claimed = TryBeginQuantum(ready);
                }
                catch (Exception error)
                {
                    CompleteDemandWithError(key, ready.Demand, error);
                    continue;
                }
                if (claimed is not { } start)
                    continue;
                var demand = start.Demand;
                var prior = start.Prior;
                var target = start.Target;
                var forcePersistThroughSequence = start.ForcePersistThroughSequence;

                Interlocked.Increment(ref _activeWorkers);
                try
                {
                    var step = await _store.ProjectWorkflowQuantumAsync(
                        key,
                        prior,
                        target,
                        forcePersistThroughSequence,
                        _shutdown.Token).ConfigureAwait(false);
                    switch (step)
                    {
                        case WorkflowProjectionStep.Gone:
                            CompleteDemandAsGone(key, demand);
                            continue;
                        case WorkflowProjectionStep.Rotated rotated:
                            HandOffDemandToSuccessor(key, demand, rotated.Successor);
                            continue;
                        case WorkflowProjectionStep.Advanced advanced:
                            CompleteSuccessfulQuantum(key, demand, advanced.State);
                            break;
                    }
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
            // Shutdown cancels the channel read; DisposeAsync settles the
            // waiters that are still outstanding.
        }
    }

    private readonly record struct QuantumStart(
        Demand Demand,
        WorkflowProjectionState? Prior,
        ulong Target,
        ulong ForcePersistThroughSequence);

    private async Task<WorkflowProjectionCheckpoint?> WaitAfterRetirementAsync(
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
            return null;
        }
        return await WaitForAsync(
                key,
                desiredSequence,
                forcePersistThroughSequence,
                ct)
            .ConfigureAwait(false);
    }

    // Re-admits waiters whose demand disappeared under them. `settled` is the
    // retirement task to await first when the key was retired; a rotation passes
    // null and re-admits immediately on the successor key.
    private void ResumeWaiters(
        IReadOnlyList<Waiter> waiters,
        WorkflowProjectionKey key,
        Task? settled)
    {
        if (waiters.Count is 0)
            return;
        lock (_sync)
        {
            _resumes.RemoveAll(static resume => resume.IsCompleted);
            foreach (var waiter in waiters)
                _resumes.Add(ResumeWaiterAsync(waiter, key, settled));
        }
    }

    private async Task ResumeWaiterAsync(
        Waiter waiter,
        WorkflowProjectionKey key,
        Task? settled)
    {
        try
        {
            if (settled is not null)
                await settled.WaitAsync(waiter.Token).ConfigureAwait(false);
            if (!await _store
                    .IsWorkflowProjectionGenerationCurrentAsync(key, waiter.Token)
                    .ConfigureAwait(false))
            {
                waiter.CompleteGone();
                return;
            }
            waiter.Complete(
                await WaitForAsync(key, waiter.Sequence, waiter.Token)
                    .ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            waiter.Cancel(waiter.Token);
        }
        catch (Exception error)
        {
            waiter.Fail(error);
        }
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
        WorkflowLifecycleLog.ProjectionFailed(_logger, error);
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
            if (demand.TransientAttempts <= MaximumTransientAttempts)
            {
                WorkflowLifecycleLog.ProjectionRetry(
                    _logger,
                    demand.TransientAttempts,
                    error);
            }
            return demand.TransientAttempts <= MaximumTransientAttempts;
        }
    }

    private void CompleteDemandAsGone(WorkflowProjectionKey key, Demand expected)
    {
        if (!TryDetachDemand(key, expected, out var waiters))
            return;
        foreach (var waiter in waiters)
            waiter.CompleteGone();
    }

    private void HandOffDemandToSuccessor(
        WorkflowProjectionKey key,
        Demand expected,
        WorkflowProjectionKey successor)
    {
        if (!TryDetachDemand(key, expected, out var waiters))
            return;
        ResumeWaiters(waiters, successor, settled: null);
    }

    // Each detach reason has its own cache consequence: a dead key drops its
    // entry, a failure trims idle entries, and a cancellation leaves the cache
    // untouched because the projection it holds is still valid.
    private enum DemandCacheAction
    {
        Keep,
        RemoveKey,
        TrimIdle
    }

    private bool TryDetachDemand(
        WorkflowProjectionKey key,
        Demand expected,
        out List<Waiter> waiters,
        DemandCacheAction cacheAction = DemandCacheAction.RemoveKey)
    {
        lock (_sync)
        {
            switch (cacheAction)
            {
                case DemandCacheAction.RemoveKey:
                    RemoveCachedLocked(key);
                    break;
                case DemandCacheAction.TrimIdle:
                    TrimIdleCacheLocked();
                    break;
            }
            if (!_demands.TryGetValue(key, out var demand) ||
                !ReferenceEquals(demand, expected))
            {
                waiters = [];
                return false;
            }
            _demands.Remove(key);
            waiters = demand.TakeAllWaiters();
            demand.Joined.TrySetResult(true);
            return true;
        }
    }

    private void CompleteDemandWithCancellation(
        WorkflowProjectionKey key,
        Demand expected,
        CancellationToken token)
    {
        if (!TryDetachDemand(key, expected, out var waiters, DemandCacheAction.Keep))
            return;
        foreach (var waiter in waiters)
            waiter.Cancel(token);
    }

    private void CompleteDemandWithError(
        WorkflowProjectionKey key,
        Demand expected,
        Exception error)
    {
        if (!TryDetachDemand(key, expected, out var waiters, DemandCacheAction.TrimIdle))
            return;
        WorkflowLifecycleLog.ProjectionFailed(_logger, error);
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
                // An outstanding ready token does not keep the demand alive: the
                // worker compares the token's demand identity on pickup and
                // discards it when the demand is gone, so a token left behind by
                // the last cancelled waiter is inert rather than executable.
                if (removed &&
                    !demand.Processing &&
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
        DuckDbTransientErrors.IsTransient(error);

    private void RecordAdmissionRejection(string reason)
    {
        WorkflowLifecycleLog.ProjectionAdmissionRejected(_logger, reason);
    }

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

    private sealed class Waiter(ulong sequence, CancellationToken token)
    {
        private readonly TaskCompletionSource<WorkflowProjectionCheckpoint?> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _cancellationRegistration;

        public ulong Sequence { get; } = sequence;

        public CancellationToken Token { get; } = token;

        public Task<WorkflowProjectionCheckpoint?> Task => _completion.Task;

        public void ArmCancellation(Action cancel)
        {
            _cancellationRegistration = Token.Register(cancel);
            if (_completion.Task.IsCompleted)
                _cancellationRegistration.Dispose();
        }

        public void Complete(WorkflowProjectionCheckpoint? checkpoint)
        {
            _cancellationRegistration.Dispose();
            _completion.TrySetResult(checkpoint);
        }

        // The run itself no longer exists; callers map this to "not found".
        public void CompleteGone()
        {
            _cancellationRegistration.Dispose();
            _completion.TrySetResult(null);
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
