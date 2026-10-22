using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qyl.Orchestration.Internal;

namespace Qyl.Orchestration;

/// <summary>A composed application supervised by the same process engine as the qyl CLI.</summary>
public sealed class QylApplication : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly IReadOnlyList<QylResource> _resources;
    private readonly QylResourceRegistry _registry;
    private readonly QylLogStore _logs;
    private readonly int _startupTimeoutSeconds;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Lazy<Task> _disposal;
    private bool _started;
    private bool _disposed;

    internal QylApplication(IHost host, IReadOnlyList<QylResource> resources, int startupTimeoutSeconds)
    {
        _host = host;
        _resources = resources;
        _registry = host.Services.GetRequiredService<QylResourceRegistry>();
        _logs = host.Services.GetRequiredService<QylLogStore>();
        _startupTimeoutSeconds = startupTimeoutSeconds;
        _disposal = new Lazy<Task>(StopOnceAsync);
    }

    /// <summary>Starts all services and returns only when each is ready. Failure or cancellation stops every child.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
                throw new InvalidOperationException("This application was already started.");
            _started = true;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(_startupTimeoutSeconds));
            using var subscription = _registry.Subscribe();
            try
            {
                await _host.StartAsync(deadline.Token).ConfigureAwait(false);
                while (true)
                {
                    var ready = 0;
                    foreach (var resource in _resources)
                    {
                        if (!_registry.Snapshot.TryGetValue(resource.Name, out var state))
                            continue;
                        if (state.Lifecycle is ResourceLifecycle.Failed or ResourceLifecycle.Stopped)
                            throw new InvalidOperationException($"{resource.Name} failed to start: {state.LastError}");
                        if (state.Lifecycle == ResourceLifecycle.Ready)
                            ready++;
                    }
                    if (ready == _resources.Count)
                        return;
                    await subscription.Events.ReadAsync(deadline.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await StopCoreAsync().ConfigureAwait(false);
                throw new TimeoutException($"The application did not become ready within {_startupTimeoutSeconds} seconds."
                    + Environment.NewLine + string.Join(Environment.NewLine, _resources.Select(resource =>
                        resource.Name + ": " + string.Join(Environment.NewLine, _logs.Snapshot(resource.Name).Select(static line => line.Line)))));
            }
            catch
            {
                await StopCoreAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>Returns a running service's loopback HTTP endpoint. Unknown or non-ready services fail explicitly.</summary>
    public Uri GetEndpoint(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_registry.Contains(name))
            throw new ArgumentException($"No service named '{name}' belongs to this application.", nameof(name));
        if (_registry.Snapshot.TryGetValue(name, out var state)
            && state is { Lifecycle: ResourceLifecycle.Ready, Endpoint: { } endpoint })
            return endpoint;
        throw new InvalidOperationException($"Service '{name}' is not ready.");
    }

    /// <summary>
    /// Returns a snapshot of a service's most recent standard-output and standard-error lines.
    /// Retained output remains readable after failed startup or disposal.
    /// </summary>
    public IReadOnlyList<string> GetOutput(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_registry.Contains(name))
            throw new ArgumentException($"No service named '{name}' belongs to this application.", nameof(name));
        return _logs.Snapshot(name).Select(static line => line.Line).ToArray();
    }

    /// <summary>Stops every process and releases reserved endpoints. Safe to call more than once.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _disposal.Value.ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Dispose();
        }
    }

    // Runs once, however often DisposeAsync is called: every call awaits the same stop.
    private async Task StopOnceAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_disposed)
                await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        _disposed = true;
        try
        {
            if (_started)
                await _host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await ((IAsyncDisposable)_host).DisposeAsync().ConfigureAwait(false);
            foreach (var resource in _resources)
                resource.Reservation?.Dispose();
        }
    }
}
