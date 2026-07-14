
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.Host.Internal;

internal sealed partial class QylOrchestrator(
    IReadOnlyList<QylResource> resources,
    QylResourceRegistry registry,
    QylAppOptions options,
    IHttpClientFactory httpClientFactory,
    QylProcessLauncher launcher,
    QylResourceActions resourceActions,
    TimeProvider time,
    ILogger<QylOrchestrator> logger) : BackgroundService
{
    private static readonly CompositeFormat s_urlFormat =
        CompositeFormat.Parse(QylConstants.Network.LocalhostUrlTemplate);

    private readonly ConcurrentDictionary<string, Process> _processes = new(StringComparer.Ordinal);

    // Names whose next exit is a user-requested restart, not a crash: supervision consumes the marker,
    // relaunches on the same port, and resets the crash budget instead of burning it.
    private readonly ConcurrentDictionary<string, byte> _userRestarts = new(StringComparer.Ordinal);

    // A requested stop is terminal for this supervision loop. Without this marker, a killed child
    // would be indistinguishable from a crash and would be relaunched by the normal bounded policy.
    private readonly ConcurrentDictionary<string, byte> _userStops = new(StringComparer.Ordinal);

    [LoggerMessage(EventId = QylConstants.LogEvents.OrchestratorStarted, Level = LogLevel.Information,
        Message = "qyl.host orchestrator booting with {Count} resource(s)")]
    private static partial void LogBoot(ILogger logger, int count);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceReady, Level = LogLevel.Information,
        Message = "Resource '{Name}' ready on {Endpoint}")]
    private static partial void LogReady(ILogger logger, string name, Uri endpoint);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceReady, Level = LogLevel.Information,
        Message = "Connection-only resource '{Name}' ready")]
    private static partial void LogConnectionReady(ILogger logger, string name);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceFailed, Level = LogLevel.Error,
        Message = "Resource '{Name}' failed to start: {Reason}")]
    private static partial void LogFailed(ILogger logger, string name, string reason, Exception? ex);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceRestarting, Level = LogLevel.Warning,
        Message = "Resource '{Name}' exited (code {ExitCode}); restarting (attempt {Attempt})")]
    private static partial void LogRestarting(ILogger logger, string name, int exitCode, int attempt);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceUserRestart, Level = LogLevel.Information,
        Message = "Resource '{Name}' restarting on request")]
    private static partial void LogUserRestart(ILogger logger, string name);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogBoot(logger, resources.Count);
        foreach (var r in resources) registry.Publish(r.Name, ResourceLifecycle.Pending);

        var tasks = resources.Select(r => StartResourceAsync(r, stoppingToken))
            .Append(ListenForResourceActionsAsync(stoppingToken)).ToArray();

        try
        {
            // WhenAll stays inside the try so that a cancellation observed during startup — a dependency
            // wait or health poll seeing stoppingToken — still runs StopAllAsync instead of escaping past
            // the finally and leaving already-started child processes orphaned.
            await Task.WhenAll(tasks).ConfigureAwait(false);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown signaled via stoppingToken — fall through to StopAllAsync.
        }
        finally
        {
            await StopAllAsync().ConfigureAwait(false);
        }
    }

    private async Task StartResourceAsync(QylResource resource, CancellationToken stoppingToken)
    {
        try
        {
            if (!await WaitForDependenciesAsync(resource, stoppingToken).ConfigureAwait(false)) return;

            registry.Publish(resource.Name, ResourceLifecycle.Starting);

            // A connection-only resource has no runner-owned listener. Its SDK transport may be
            // stdio, remote HTTP, or in-process, so publishing an allocated localhost endpoint
            // would be fabricated client-visible data.
            if (resource.Launch is null)
            {
                var connected = await ProbeReadinessAsync(resource, null, null, stoppingToken).ConfigureAwait(false);
                registry.Publish(resource.Name,
                    connected ? ResourceLifecycle.Ready : ResourceLifecycle.Failed,
                    lastError: connected ? null : "Readiness probe timed out");
                if (connected) LogConnectionReady(logger, resource.Name);
                return;
            }

            var port = resource.Port == QylConstants.Ports.DynamicAllocation
                ? PortAllocator.ClaimFreePort(QylConstants.Network.Loopback)
                : resource.Port;

            var endpoint =
                new Uri(string.Format(CultureInfo.InvariantCulture, s_urlFormat, QylConstants.Network.Loopback, port));

            // The launcher spawns a child process whose lifecycle we own via _processes.
            var process = launcher.Launch(resource, endpoint);
            _processes[resource.Name] = process;

            if (await ProbeReadinessAsync(resource, port, endpoint, stoppingToken).ConfigureAwait(false))
            {
                registry.Publish(resource.Name, ResourceLifecycle.Ready, port, endpoint);
                LogReady(logger, resource.Name, endpoint);

                // Own a launched process for the rest of its life: restart it if it crashes (bounded).
                await SuperviseProcessAsync(resource, process, port, endpoint, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint, "Health probe timed out");
            }
        }
        catch (SocketException ex)
        {
            FailResource(resource, ex);
        }
        catch (Win32Exception ex)
        {
            FailResource(resource, ex);
        }
        catch (FileNotFoundException ex)
        {
            FailResource(resource, ex);
        }
        catch (InvalidOperationException ex)
        {
            FailResource(resource, ex);
        }
    }

    // Holds this resource in Pending until every WaitsFor dependency reports Ready. A dependency
    // reaching a terminal non-Ready state (Failed/Stopped) fails this resource instead of hanging it —
    // every dependency reaches a terminal state on its own (health-probe timeout or restart budget),
    // so no extra timeout is layered here. Cancellation means shutdown, not failure.
    private async Task<bool> WaitForDependenciesAsync(QylResource resource, CancellationToken stoppingToken)
    {
        foreach (var dep in resource.WaitsFor)
        {
            var ready = registry.WhenReady(dep);
            while (!ready.IsCompleted)
            {
                if (stoppingToken.IsCancellationRequested) return false;

                if (registry.Snapshot.TryGetValue(dep, out var state) &&
                    state.Lifecycle is ResourceLifecycle.Failed or ResourceLifecycle.Stopped)
                {
                    registry.Publish(resource.Name, ResourceLifecycle.Failed,
                        lastError: $"Dependency '{dep}' reached state {state.Lifecycle} before becoming ready");
                    return false;
                }

                try
                {
                    await Task.WhenAny(ready,
                            Task.Delay(TimeSpan.FromMilliseconds(QylConstants.Orchestrator.HealthPollIntervalMs),
                                time, stoppingToken))
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void FailResource(QylResource resource, Exception ex)
    {
        LogFailed(logger, resource.Name, ex.Message, ex);
        registry.Publish(resource.Name, ResourceLifecycle.Failed, lastError: ex.Message);
    }

    // Restart-on-crash for a launched process: while the runner is up, an unexpected exit is a crash — relaunch
    // it (bounded by MaxRestarts) and re-probe health. Cancellation means shutdown, not a crash.
    private async Task SuperviseProcessAsync(QylResource resource, Process process, int port, Uri endpoint,
        CancellationToken stoppingToken)
    {
        var current = process;
        var restarts = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await current.WaitForExitAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (stoppingToken.IsCancellationRequested) return;

            var exitCode = current.ExitCode;
            current.Dispose();

            if (_userStops.TryRemove(resource.Name, out _))
            {
                _processes.TryRemove(resource.Name, out _);
                registry.Publish(resource.Name, ResourceLifecycle.Stopped, port, endpoint);
                return;
            }

            if (_userRestarts.TryRemove(resource.Name, out _))
            {
                // Deliberate restart from a first-party control surface: same port, same endpoint (bind-with-retry — the health
                // poll below gives the relaunched child the full startup window to reclaim the socket),
                // and a fresh crash budget.
                restarts = 0;
                LogUserRestart(logger, resource.Name);
                registry.Publish(resource.Name, ResourceLifecycle.Starting, port, endpoint,
                    "Restart requested from TUI");
            }
            else if (restarts >= QylConstants.Orchestrator.MaxRestarts)
            {
                _processes.TryRemove(resource.Name, out _);
                registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint,
                    $"Process exited (code {exitCode}); restart limit ({QylConstants.Orchestrator.MaxRestarts}) reached");
                return;
            }
            else
            {
                restarts++;
                LogRestarting(logger, resource.Name, exitCode, restarts);
                registry.Publish(resource.Name, ResourceLifecycle.Starting, port, endpoint,
                    $"Process exited (code {exitCode}); restarting ({restarts}/{QylConstants.Orchestrator.MaxRestarts})");
            }

            try
            {
                current = launcher.Launch(resource, endpoint);
                _processes[resource.Name] = current;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                _processes.TryRemove(resource.Name, out _);
                registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint, $"Restart failed: {ex.Message}");
                return;
            }

            var healthy = await ProbeReadinessAsync(resource, port, endpoint, stoppingToken).ConfigureAwait(false);
            registry.Publish(resource.Name,
                healthy ? ResourceLifecycle.Ready : ResourceLifecycle.Failed, port, endpoint,
                healthy ? null : "Health probe timed out after restart");
        }
    }

    private async Task ListenForResourceActionsAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in resourceActions.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                request.Complete(HandleResourceAction(request));
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown — nothing to drain
        }
    }

    private QylResourceActionResult HandleResourceAction(QylResourceActionRequest request)
    {
        var resource = resources.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, request.ResourceName, StringComparison.Ordinal));
        if (resource is null)
            return new QylResourceActionResult(QylResourceActionStatus.NotFound, "Resource does not exist.");

        if (resource.Launch is null)
            return new QylResourceActionResult(
                QylResourceActionStatus.Conflict,
                "Connection-only resources are controlled by their transport.");

        if (!registry.Snapshot.TryGetValue(resource.Name, out var state) ||
            state.Lifecycle != ResourceLifecycle.Ready ||
            !_processes.TryGetValue(resource.Name, out var process))
        {
            return new QylResourceActionResult(
                QylResourceActionStatus.Conflict,
                "Resource must be a running Ready process.");
        }

        var marker = request.Action == QylResourceAction.Restart ? _userRestarts : _userStops;
        marker[resource.Name] = 0;

        try
        {
            if (process.HasExited)
            {
                marker.TryRemove(resource.Name, out _);
                return new QylResourceActionResult(
                    QylResourceActionStatus.Conflict,
                    "Resource process has already exited.");
            }

            registry.Publish(
                resource.Name,
                request.Action == QylResourceAction.Restart
                    ? ResourceLifecycle.Starting
                    : ResourceLifecycle.Stopping,
                state.AllocatedPort,
                state.Endpoint,
                request.Action == QylResourceAction.Restart ? "Restart requested" : null);
            process.Kill(true);
            return new QylResourceActionResult(QylResourceActionStatus.Accepted);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            marker.TryRemove(resource.Name, out _);
            var transition = request.Action == QylResourceAction.Restart
                ? ResourceLifecycle.Starting
                : ResourceLifecycle.Stopping;
            if (registry.Snapshot.GetValueOrDefault(resource.Name)?.Lifecycle == transition)
            {
                registry.Publish(
                    resource.Name,
                    state.Lifecycle,
                    state.AllocatedPort,
                    state.Endpoint,
                    state.LastError);
            }

            return new QylResourceActionResult(QylResourceActionStatus.Failed, ex.Message);
        }
    }

    // Readiness is a per-resource strategy (IReadinessProbe); HTTP health polling is the default.
    // The probe receives a snapshot state carrying the endpoint it
    // must judge — deliberately not the registry's published state, which may lag the launch.
    private Task<bool> ProbeReadinessAsync(QylResource resource, int? port, Uri? endpoint,
        CancellationToken stoppingToken)
    {
        // Launch-less resources always carry an explicit probe (Build() enforces it); the default
        // HTTP probe exists only for launched processes with a health path.
        var probe = resource.ReadinessProbe ?? new HttpHealthProbe(
            httpClientFactory,
            resource.Launch!.HealthPath,
            TimeSpan.FromSeconds(options.StartupTimeoutSeconds),
            time);

        return probe.IsReadyAsync(new QylResourceState
        {
            Name = resource.Name,
            Lifecycle = ResourceLifecycle.Starting,
            Timestamp = time.GetUtcNow(),
            AllocatedPort = port,
            Endpoint = endpoint
        }, stoppingToken);
    }

    private async Task StopAllAsync()
    {
        foreach (var (name, process) in _processes)
        {
            registry.Publish(name, ResourceLifecycle.Stopping);
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited between HasExited and Kill — nothing to clean up.
            }

            registry.Publish(name, ResourceLifecycle.Stopped);
            process.Dispose();
        }

        registry.Complete();
    }
}
