// Copyright (c) 2025-2026 ancplua

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Qyl.Run.Internal;

/// <summary>
/// Spawns every declared resource's subprocess, allocates ports for port-0 resources, polls
/// <see cref="QylLaunchSpec.HealthPath"/> on each, and publishes lifecycle events into the
/// <see cref="QylResourceRegistry"/>. <see cref="QylConsoleUi"/> renders off that feed.
/// </summary>
internal sealed partial class QylOrchestrator(
    IReadOnlyList<QylResource> resources,
    QylResourceRegistry registry,
    IOptions<QylAppOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider time,
    ILogger<QylOrchestrator> logger) : BackgroundService
{
    [LoggerMessage(EventId = QylConstants.LogEvents.OrchestratorStarted, Level = LogLevel.Information, Message = "qyl.run orchestrator booting with {Count} resource(s)")]
    private static partial void LogBoot(ILogger logger, int count);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceReady, Level = LogLevel.Information, Message = "Resource '{Name}' ready on {Endpoint}")]
    private static partial void LogReady(ILogger logger, string name, Uri endpoint);

    [LoggerMessage(EventId = QylConstants.LogEvents.ResourceFailed, Level = LogLevel.Error, Message = "Resource '{Name}' failed to start: {Reason}")]
    private static partial void LogFailed(ILogger logger, string name, string reason, Exception? ex);

    private static readonly CompositeFormat s_urlFormat = CompositeFormat.Parse(QylConstants.Network.LocalhostUrlTemplate);

    private readonly ConcurrentDictionary<string, Process> _processes = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogBoot(logger, resources.Count);
        foreach (var r in resources) registry.Publish(r.Name, ResourceLifecycle.Pending);

        // Ordered start — resources get spawned in declaration order, but each one awaits its
        // `WaitForNames` deps before racing to Ready. Simple topological traversal; cycle
        // detection is implicit (a cycle deadlocks, we fail after the startup timeout).
        var tasks = resources.Select(r => StartResourceAsync(r, stoppingToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* expected on host shutdown */ }
        finally { await StopAllAsync().ConfigureAwait(false); }
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

            var endpoint = new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, s_urlFormat, options.Value.RunnerHost, port));

            if (string.IsNullOrEmpty(resource.Launch.Executable))
            {
                // External endpoint — treat as already-running and just health-poll.
                if (await PollHealthAsync(endpoint, resource.Launch.HealthPath, stoppingToken).ConfigureAwait(false))
                {
                    registry.Publish(resource.Name, ResourceLifecycle.Ready, port, endpoint);
                    LogReady(logger, resource.Name, endpoint);
                }
                else
                {
                    registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint, "Health probe timed out for external endpoint");
                }
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = resource.Launch.Executable,
                WorkingDirectory = resource.Launch.WorkingDirectory ?? string.Empty,
                RedirectStandardOutput = options.Value.CaptureChildOutput,
                RedirectStandardError = options.Value.CaptureChildOutput,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in resource.Launch.Args) startInfo.ArgumentList.Add(arg);
            foreach (var kv in resource.Launch.Env) startInfo.Environment[kv.Key] = kv.Value;
            startInfo.Environment[QylConstants.Env.AspNetCoreUrls] = endpoint.ToString();

            var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Process.Start returned null for '{resource.Name}'");
            _processes[resource.Name] = process;

            if (await PollHealthAsync(endpoint, resource.Launch.HealthPath, stoppingToken).ConfigureAwait(false))
            {
                registry.Publish(resource.Name, ResourceLifecycle.Ready, port, endpoint);
                LogReady(logger, resource.Name, endpoint);
            }
            else
            {
                registry.Publish(resource.Name, ResourceLifecycle.Failed, port, endpoint, "Health probe timed out");
            }
        }
        // Typed catches — each maps to one concrete subprocess-launch failure mode. Anything
        // outside this set is a programmer bug and must surface with a full stack trace.
        // SocketException : Win32Exception — keep the subclass first so port-claim failures
        // report as "port in use" rather than the generic Win32 message.
        catch (SocketException ex) { FailResource(resource, ex); }           // port claim failure
        catch (Win32Exception ex) { FailResource(resource, ex); }            // executable not found / permission denied
        catch (FileNotFoundException ex) { FailResource(resource, ex); }     // project path wrong
        catch (InvalidOperationException ex) { FailResource(resource, ex); } // Process.Start returned null or state error
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
            await Task.Delay(TimeSpan.FromMilliseconds(QylConstants.Orchestrator.HealthPollIntervalMs), time, stoppingToken).ConfigureAwait(false);
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
            catch (HttpRequestException) { /* probe again */ }
            catch (TaskCanceledException) { /* probe again */ }

            await Task.Delay(TimeSpan.FromMilliseconds(QylConstants.Orchestrator.HealthPollIntervalMs), time, stoppingToken).ConfigureAwait(false);
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
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException) { /* already gone */ }
            registry.Publish(name, ResourceLifecycle.Stopped);
            process.Dispose();
        }
        registry.Complete();
    }
}
