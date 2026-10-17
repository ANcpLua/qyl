using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.Orchestration.Internal;

internal sealed partial class QylOrchestrator(
    IReadOnlyList<QylResource> resources,
    QylResourceRegistry registry,
    OrchestratorOptions options,
    IHttpClientFactory httpClientFactory,
    QylProcessLauncher launcher,
    QylResourceActions resourceActions,
    TimeProvider time,
    ILogger<QylOrchestrator> logger) : BackgroundService
{
    private static readonly CompositeFormat s_urlFormat =
        CompositeFormat.Parse(OrchestrationDefaults.Network.LocalhostUrlTemplate);

    private static readonly TimeSpan s_processExitTimeout = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, TaskCompletionSource> _completions = resources.ToDictionary(
        resource => resource.Name,
        _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, Process> _processes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _stopRequests = new(StringComparer.Ordinal);

    // Names whose next exit is a user-requested restart, not a crash: supervision consumes the marker,
    // relaunches on the same port, and resets the crash budget instead of burning it.
    private readonly ConcurrentDictionary<string, byte> _userRestarts = new(StringComparer.Ordinal);

    // A requested stop is terminal for this supervision loop. Without this marker, a killed child
    // would be indistinguishable from a crash and would be relaunched by the normal bounded policy.
    private readonly ConcurrentDictionary<string, byte> _userStops = new(StringComparer.Ordinal);

    [LoggerMessage(EventId = OrchestrationDefaults.LogEvents.OrchestratorStarted, Level = LogLevel.Information,
        Message = "qyl runtime orchestrator booting with {Count} resource(s)")]
    private static partial void LogBoot(ILogger logger, int count);

    [LoggerMessage(EventId = OrchestrationDefaults.LogEvents.ResourceReady, Level = LogLevel.Information,
        Message = "Resource '{Name}' ready on {Endpoint}")]
    private static partial void LogReady(ILogger logger, string name, Uri endpoint);

    [LoggerMessage(EventId = OrchestrationDefaults.LogEvents.ResourceFailed, Level = LogLevel.Error,
        Message = "Resource '{Name}' failed to start: {Reason}")]
    private static partial void LogFailed(ILogger logger, string name, string reason, Exception? ex);

    [LoggerMessage(EventId = OrchestrationDefaults.LogEvents.ResourceRestarting, Level = LogLevel.Warning,
        Message = "Resource '{Name}' exited (code {ExitCode}); restarting (attempt {Attempt})")]
    private static partial void LogRestarting(ILogger logger, string name, int exitCode, int attempt);

    [LoggerMessage(EventId = OrchestrationDefaults.LogEvents.ResourceUserRestart, Level = LogLevel.Information,
        Message = "Resource '{Name}' restarting on request")]
    private static partial void LogUserRestart(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Resource '{Name}' process termination failed: {Reason}")]
    private static partial void LogTerminationFailed(ILogger logger, string name, string reason, Exception? ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogBoot(logger, resources.Count);
        foreach (var r in resources) registry.Publish(r.Name, ResourceLifecycle.Pending);

        var tasks = resources.Select(r => StartResourceAsync(r, stoppingToken))
            .Append(ListenForResourceActionsAsync(stoppingToken)).ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            registry.Complete();
        }
    }

    private async Task StartResourceAsync(QylResource resource, CancellationToken stoppingToken)
    {
        try
        {
            if (!await WaitForDependenciesAsync(resource, stoppingToken).ConfigureAwait(false)) return;

            registry.Publish(resource.Name, ResourceLifecycle.Starting);

            var port = resource.Port == OrchestrationDefaults.Ports.DynamicAllocation
                ? PortAllocator.ClaimFreePort(OrchestrationDefaults.Network.Loopback)
                : resource.Port;

            var endpoint =
                new Uri(string.Format(CultureInfo.InvariantCulture, s_urlFormat, OrchestrationDefaults.Network.Loopback, port));

            var process = launcher.Launch(resource, endpoint);
            _processes[resource.Name] = process;
            await SuperviseProcessAsync(resource, process, port, endpoint, stoppingToken).ConfigureAwait(false);
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
        finally
        {
            _ = _completions[resource.Name].TrySetResult();
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
                            Task.Delay(TimeSpan.FromMilliseconds(OrchestrationDefaults.Orchestrator.HealthPollIntervalMs),
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
        Process? current = process;
        var restarts = 0;
        var stoppedByUser = false;

        try
        {
            if (!await ProbeReadinessAsync(resource, current, port, endpoint, stoppingToken).ConfigureAwait(false))
            {
                await PublishStartupFailureAsync(resource, current, port, endpoint, stoppingToken).ConfigureAwait(false);
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                using var request = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _stopRequests[resource.Name] = request;
                registry.Publish(resource.Name, ResourceLifecycle.Ready, port, endpoint);
                LogReady(logger, resource.Name, endpoint);
                try
                {
                    await current.WaitForExitAsync(request.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (OperationCanceledException) when (request.IsCancellationRequested)
                {
                    await StopProcessAsync(resource.Name, current).ConfigureAwait(false);
                }
                finally
                {
                    _stopRequests.TryRemove(resource.Name, out _);
                }

                if (stoppingToken.IsCancellationRequested) return;

                var exitCode = current.ExitCode;

                if (_userStops.TryRemove(resource.Name, out _))
                {
                    stoppedByUser = true;
                    return;
                }

                if (_userRestarts.TryRemove(resource.Name, out _))
                {
                    restarts = 0;
                    LogUserRestart(logger, resource.Name);
                    registry.Publish(resource.Name, ResourceLifecycle.Starting, port, endpoint,
                        "Restart requested from TUI");
                }
                else if (restarts >= options.MaxRestarts)
                {
                    registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint,
                        $"Process exited (code {exitCode}); restart limit ({options.MaxRestarts}) reached");
                    return;
                }
                else
                {
                    restarts++;
                    LogRestarting(logger, resource.Name, exitCode, restarts);
                    registry.Publish(resource.Name, ResourceLifecycle.Starting, port, endpoint,
                        $"Process exited (code {exitCode}); restarting ({restarts}/{options.MaxRestarts})");
                }

                _processes.TryRemove(resource.Name, out _);
                current.Dispose();
                current = null;

                try
                {
                    current = launcher.Launch(resource, endpoint);
                    _processes[resource.Name] = current;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
                {
                    registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint,
                        $"Restart failed: {ex.Message}");
                    return;
                }

                if (!await ProbeReadinessAsync(resource, current, port, endpoint, stoppingToken).ConfigureAwait(false))
                {
                    await PublishStartupFailureAsync(resource, current, port, endpoint, stoppingToken).ConfigureAwait(false);
                    return;
                }

            }
        }
        finally
        {
            if (stoppingToken.IsCancellationRequested)
            {
                // Dependents flush while their declared prerequisites still accept requests.
                await Task.WhenAll(resources.Where(dependent => dependent.WaitsFor.Contains(resource.Name))
                    .Select(dependent => _completions[dependent.Name].Task)).ConfigureAwait(false);
            }
            await TerminateProcessAsync(
                    resource.Name,
                    current,
                    port,
                    endpoint,
                    stoppedByUser || stoppingToken.IsCancellationRequested)
                .ConfigureAwait(false);
        }
    }

    private async Task TerminateProcessAsync(
        string name,
        Process? process,
        int port,
        Uri endpoint,
        bool publishStopped)
    {
        _processes.TryRemove(name, out _);
        _userRestarts.TryRemove(name, out _);
        _userStops.TryRemove(name, out _);

        if (publishStopped) registry.Publish(name, ResourceLifecycle.Stopping, port, endpoint);

        var exited = process is null;
        if (process is not null)
        {
            try
            {
                await StopProcessAsync(name, process).ConfigureAwait(false);
                exited = true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                LogTerminationFailed(logger, name, ex.Message, ex);
            }
            catch (OperationCanceledException)
            {
                LogTerminationFailed(
                    logger,
                    name,
                    $"did not exit within {s_processExitTimeout.TotalSeconds} seconds",
                    null);
            }
            finally
            {
                process.Dispose();
            }
        }

        if (publishStopped)
        {
            registry.Publish(
                name,
                exited ? ResourceLifecycle.Stopped : ResourceLifecycle.Failed,
                port,
                endpoint,
                exited ? null : "Process termination timed out");
        }
    }

    private async Task StopProcessAsync(string name, Process process)
    {
        if (!process.HasExited)
        {
            // ConsoleLifetime handles SIGTERM on Unix. Give the host time to drain requests
            // and flush exporters; Process.Kill bypasses all hosted-service cleanup.
            var requested = OperatingSystem.IsWindows()
                ? process.CloseMainWindow()
                : SendSignal(process.Id, 15) == 0;
            if (requested)
            {
                using var graceful = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await process.WaitForExitAsync(graceful.Token).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException)
                {
                    LogTerminationFailed(logger, name, "graceful shutdown exceeded 10 seconds; terminating the process tree", null);
                }
            }
            if (!process.HasExited) process.Kill(true);
        }
        using var timeout = new CancellationTokenSource(s_processExitTimeout);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
    }

    [LibraryImport("libc", EntryPoint = "kill")]
    private static partial int SendSignal(int processId, int signal);

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
            // Normal shutdown.
        }
    }

    private QylResourceActionResult HandleResourceAction(QylResourceActionRequest request)
    {
        if (resources.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, request.ResourceName, StringComparison.Ordinal)) is not { } resource)
            return new QylResourceActionResult(QylResourceActionStatus.NotFound, "Resource does not exist.");

        if (!registry.Snapshot.TryGetValue(resource.Name, out var state) ||
            state.Lifecycle != ResourceLifecycle.Ready ||
            !_processes.TryGetValue(resource.Name, out var process) ||
            !_stopRequests.TryGetValue(resource.Name, out var stopRequest))
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
            stopRequest.Cancel();
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

    private async Task PublishStartupFailureAsync(QylResource resource, Process process, int port,
        Uri endpoint, CancellationToken cancellationToken)
    {
        if (process.HasExited)
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var reason = process.HasExited
            ? $"Process exited with code {process.ExitCode} before becoming ready"
            : "Health probe timed out";
        var output = string.Join(Environment.NewLine, launcher.Output(resource.Name).Select(static line => line.Line));
        registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint,
            output.Length == 0 ? reason : reason + Environment.NewLine + output);
    }

    private Task<bool> ProbeReadinessAsync(QylResource resource, Process process, int? port, Uri? endpoint,
        CancellationToken stoppingToken)
    {
        var probe = new HttpHealthProbe(
            httpClientFactory,
            resource.Launch.HealthPath,
            TimeSpan.FromSeconds(options.StartupTimeoutSeconds),
            time);

        return probe.IsReadyAsync(new QylResourceState
        {
            Name = resource.Name,
            Lifecycle = ResourceLifecycle.Starting,
            Timestamp = time.GetUtcNow(),
            AllocatedPort = port,
            Endpoint = endpoint
        }, stoppingToken, process, resource.Launch.RequireProcessId);
    }

}
