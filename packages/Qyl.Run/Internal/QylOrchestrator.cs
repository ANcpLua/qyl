
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Qyl.Run.Internal;

internal sealed partial class QylOrchestrator(
    IReadOnlyList<QylResource> resources,
    QylResourceRegistry registry,
    IOptions<QylAppOptions> options,
    IHttpClientFactory httpClientFactory,
    QylProcessLauncher launcher,
    TimeProvider time,
    ILogger<QylOrchestrator> logger) : BackgroundService
{
    private static readonly CompositeFormat s_urlFormat =
        CompositeFormat.Parse(QylConstants.Network.LocalhostUrlTemplate);

    private readonly ConcurrentDictionary<string, Process> _processes = new(StringComparer.Ordinal);

    [LoggerMessage(EventId = QylConstants.LogEvents.OrchestratorStarted, Level = LogLevel.Information,
        Message = "qyl.run orchestrator booting with {Count} resource(s)")]
    private static partial void LogBoot(ILogger logger, int count);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceReady, Level = LogLevel.Information,
        Message = "Resource '{Name}' ready on {Endpoint}")]
    private static partial void LogReady(ILogger logger, string name, Uri endpoint);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceFailed, Level = LogLevel.Error,
        Message = "Resource '{Name}' failed to start: {Reason}")]
    private static partial void LogFailed(ILogger logger, string name, string reason, Exception? ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogBoot(logger, resources.Count);
        foreach (var r in resources) registry.Publish(r.Name, ResourceLifecycle.Pending);

        var tasks = resources.Select(r => StartResourceAsync(r, stoppingToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        try
        {
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
        foreach (var depName in resource.WaitForNames)
        {
            await WaitForReadyAsync(depName, stoppingToken).ConfigureAwait(false);
        }

        try
        {
            registry.Publish(resource.Name, ResourceLifecycle.Starting);

            var port = resource.Port == QylConstants.Ports.DynamicAllocation
                ? PortAllocator.ClaimFreePort(options.Value.RunnerHost)
                : resource.Port;

            var endpoint =
                new Uri(string.Format(CultureInfo.InvariantCulture, s_urlFormat, options.Value.RunnerHost, port));

            // A resource with no executable is an already-running external endpoint we only health-probe;
            // otherwise the launcher spawns a child process whose lifecycle we own via _processes.
            Process? process = null;
            if (!string.IsNullOrEmpty(resource.Launch.Executable))
            {
                process = launcher.Launch(resource, endpoint);
                _processes[resource.Name] = process;
            }

            if (await PollHealthAsync(endpoint, resource.Launch.HealthPath, stoppingToken).ConfigureAwait(false))
            {
                registry.Publish(resource.Name, ResourceLifecycle.Ready, port, endpoint);
                LogReady(logger, resource.Name, endpoint);
            }
            else
            {
                var reason = process is null
                    ? "Health probe timed out for external endpoint"
                    : "Health probe timed out";
                registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint, reason);
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

    private void FailResource(QylResource resource, Exception ex)
    {
        LogFailed(logger, resource.Name, ex.Message, ex);
        registry.Publish(resource.Name, ResourceLifecycle.Failed, lastError: ex.Message);
    }

    private async Task WaitForReadyAsync(string name, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (registry.Snapshot.TryGetValue(name, out var state) && state.Lifecycle == ResourceLifecycle.Ready)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(QylConstants.Orchestrator.HealthPollIntervalMs), time,
                stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> PollHealthAsync(Uri baseEndpoint, string healthPath, CancellationToken stoppingToken)
    {
        using var client = httpClientFactory.CreateClient(QylConstants.HttpClients.HealthProbe);
        var deadline = time.GetUtcNow().AddSeconds(options.Value.StartupTimeoutSeconds);
        var probeUri = new Uri(baseEndpoint, healthPath);

        while (!stoppingToken.IsCancellationRequested && time.GetUtcNow() < deadline)
        {
            try
            {
                using var response = await client.GetAsync(probeUri, stoppingToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return true;
            }
            catch (HttpRequestException)
            {
                // Service not yet listening — keep polling until stoppingToken fires.
            }
            catch (TaskCanceledException)
            {
                // Probe timeout or shutdown — let the next loop iteration handle it.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(QylConstants.Orchestrator.HealthPollIntervalMs), time,
                stoppingToken).ConfigureAwait(false);
        }

        return false;
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
