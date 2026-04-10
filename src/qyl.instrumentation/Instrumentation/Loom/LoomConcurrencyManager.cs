using System.Collections.Concurrent;

namespace Qyl.Instrumentation.Instrumentation.Loom;

/// <summary>
///     Disposable slot handle returned by <see cref="LoomConcurrencyManager.AcquireAsync(string, CancellationToken)"/>.
///     Releases the underlying semaphore permit on dispose, even if the caller throws.
///     Use via <c>await using</c> to guarantee release.
/// </summary>
public sealed class LoomConcurrencySlot : IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _released;

    internal LoomConcurrencySlot(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
    }

    public ValueTask DisposeAsync()
    {
        if (_released)
            return ValueTask.CompletedTask;

        _released = true;
        _semaphore.Release();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
///     Per-tool concurrency limiter backed by <see cref="SemaphoreSlim"/>.
///     Each tool name gets its own semaphore whose initial count is either
///     <see cref="LoomPolicyDescriptor.MaxToolCalls"/> or the default limit
///     supplied at construction time.
///     <para>
///         Callers acquire a <see cref="LoomConcurrencySlot"/> via <c>await using</c>;
///         the slot releases the permit automatically when disposed, even on exception paths.
///     </para>
/// </summary>
public sealed class LoomConcurrencyManager : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);
    private readonly int _defaultLimit;
    private bool _disposed;

    public LoomConcurrencyManager(int defaultLimit = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(defaultLimit, 1);
        _defaultLimit = defaultLimit;
    }

    /// <summary>
    ///     Acquires a concurrency slot for <paramref name="toolName"/>.
    ///     Blocks asynchronously when the tool is already at its concurrency limit.
    /// </summary>
    /// <returns>
    ///     An <see cref="IAsyncDisposable"/> that releases the slot when disposed.
    /// </returns>
    public async ValueTask<LoomConcurrencySlot> AcquireAsync(
        string toolName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var semaphore = _semaphores.GetOrAdd(toolName, _ => new SemaphoreSlim(_defaultLimit, _defaultLimit));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new LoomConcurrencySlot(semaphore);
    }

    /// <summary>
    ///     Acquires a concurrency slot for <paramref name="toolName"/> using
    ///     <see cref="LoomPolicyDescriptor.MaxToolCalls"/> as the limit when positive,
    ///     falling back to the default limit otherwise.
    /// </summary>
    /// <returns>
    ///     An <see cref="IAsyncDisposable"/> that releases the slot when disposed.
    /// </returns>
    public async ValueTask<LoomConcurrencySlot> AcquireAsync(
        string toolName,
        LoomPolicyDescriptor policy,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(policy);

        var limit = policy.MaxToolCalls > 0 ? policy.MaxToolCalls : _defaultLimit;
        var semaphore = _semaphores.GetOrAdd(toolName, _ => new SemaphoreSlim(limit, limit));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new LoomConcurrencySlot(semaphore);
    }

    /// <summary>
    ///     Returns the number of available (unacquired) slots for <paramref name="toolName"/>.
    ///     If the tool has never been acquired, returns <see cref="_defaultLimit"/>.
    /// </summary>
    public int GetAvailableSlots(string toolName) =>
        _semaphores.TryGetValue(toolName, out var semaphore)
            ? semaphore.CurrentCount
            : _defaultLimit;

    /// <summary>
    ///     Returns the number of slots currently held for <paramref name="toolName"/>.
    ///     If the tool has never been acquired, returns <c>0</c>.
    /// </summary>
    public int GetInUseCount(string toolName)
    {
        if (!_semaphores.TryGetValue(toolName, out var semaphore))
            return 0;

        return Math.Max(0, _defaultLimit - semaphore.CurrentCount);
    }

    /// <summary>
    ///     Removes and disposes the semaphore for <paramref name="toolName"/>,
    ///     cancelling any pending waits. A subsequent <see cref="AcquireAsync(string, CancellationToken)"/>
    ///     for the same tool will create a fresh semaphore.
    /// </summary>
    public void Reset(string toolName)
    {
        if (_semaphores.TryRemove(toolName, out var semaphore))
            semaphore.Dispose();
    }

    /// <summary>
    ///     Disposes all semaphores, cancelling every pending wait.
    ///     Subsequent calls to <see cref="AcquireAsync(string, CancellationToken)"/> will throw
    ///     <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var kvp in _semaphores)
            kvp.Value.Dispose();

        _semaphores.Clear();
    }
}
