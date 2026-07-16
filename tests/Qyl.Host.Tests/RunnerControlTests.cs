using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Qyl.Host.Internal;

namespace Qyl.Host.Tests;

[Collection(RunnerNetworkTestGroup.Name)]
public sealed class RunnerControlTests
{
    [Fact]
    public async Task Loopback_actions_restart_and_stop_a_real_supervised_process()
    {
        if (OperatingSystem.IsWindows()) return;

        var python = FindExecutable("python3");
        Assert.NotNull(python);

        // The first python3 exec on a fresh macOS CI runner can take tens of seconds
        // (Gatekeeper/XProtect scan). Pay that cost here so the readiness window below
        // measures orchestration, not the AV scan.
        using (var warmup = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(python!)
               {
                   ArgumentList = { "-c", "pass" },
                   RedirectStandardOutput = true,
                   RedirectStandardError = true
               }))
        {
            Assert.NotNull(warmup);
            await warmup.WaitForExitAsync(TestContext.Current.CancellationToken);
        }

        var processResource = new QylResource
        {
            Name = "worker",
            Kind = QylResourceKind.Command,
            Port = QylConstants.Ports.DynamicAllocation,
            Launch = new QylLaunchSpec
            {
                Executable = python,
                Args = new ReadOnlyCollection<string>(
                [
                    "-u",
                    "-c",
                    "import http.server, os, urllib.parse\n" +
                    "u = urllib.parse.urlparse(os.environ['ASPNETCORE_URLS'])\n" +
                    "class ReusableServer(http.server.ThreadingHTTPServer):\n" +
                    "    allow_reuse_address = True\n" +
                    "server = ReusableServer((u.hostname, u.port), http.server.SimpleHTTPRequestHandler)\n" +
                    "print('started', flush=True)\n" +
                    "server.serve_forever()"
                ]),
                HealthPath = "/"
            }
        };
        QylResource[] resources = [processResource];

        var port = ClaimLoopbackPort();
        // 30s, not 5: python3 cold-start on CI macOS runners regularly blew a 5s readiness window.
        var options = new QylAppOptions { RunnerPort = port, StartupTimeoutSeconds = 30 };
        var registry = new QylResourceRegistry(resources, TimeProvider.System);
        var logs = new QylLogStore();
        var actions = new QylResourceActions();
        var launcher = new QylProcessLauncher(logs, NullLogger<QylProcessLauncher>.Instance);
        await using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var orchestrator = new QylOrchestrator(
            resources,
            registry,
            options,
            services.GetRequiredService<IHttpClientFactory>(),
            launcher,
            actions,
            TimeProvider.System,
            NullLogger<QylOrchestrator>.Instance);
        var api = new QylRunnerApi(
            registry,
            logs,
            actions,
            options,
            [],
            NullLogger<QylRunnerApi>.Instance);

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        // 60s, not 20: must exceed the 30s readiness window above, and CI macOS runners
        // are slow enough that the restart/stop round-trips need real headroom.
        lifetime.CancelAfter(TimeSpan.FromSeconds(60));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        await orchestrator.StartAsync(lifetime.Token);
        await api.StartAsync(lifetime.Token);
        try
        {
            await WaitForStateAsync(registry, logs, "worker", ResourceLifecycle.Ready, lifetime.Token);
            await WaitForLogCountAsync(logs, "worker", 1, lifetime.Token);

            using var restart = await PostBodylessAsync(
                client,
                $"http://127.0.0.1:{port}/runner/resources/worker/restart",
                lifetime.Token);
            Assert.Equal(HttpStatusCode.Accepted, restart.StatusCode);

            await WaitForLogCountAsync(logs, "worker", 2, lifetime.Token);
            await WaitForStateAsync(registry, logs, "worker", ResourceLifecycle.Ready, lifetime.Token);

            using var unknown = await PostBodylessAsync(
                client,
                $"http://127.0.0.1:{port}/runner/resources/missing/stop",
                lifetime.Token);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

            using var stop = await PostBodylessAsync(
                client,
                $"http://127.0.0.1:{port}/runner/resources/worker/stop",
                lifetime.Token);
            Assert.Equal(HttpStatusCode.Accepted, stop.StatusCode);
            await WaitForStateAsync(registry, logs, "worker", ResourceLifecycle.Stopped, lifetime.Token);

            var startsAfterStop = logs.Snapshot("worker").Count(static line => line.Line == "started");
            await Task.Delay(250, lifetime.Token);
            Assert.Equal(startsAfterStop,
                logs.Snapshot("worker").Count(static line => line.Line == "started"));
        }
        finally
        {
            lifetime.Cancel();
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await api.StopAsync(stopTimeout.Token);
            await orchestrator.StopAsync(stopTimeout.Token);
            api.Dispose();
            orchestrator.Dispose();
        }
    }

    [Fact]
    public async Task Resource_action_queue_is_bounded_and_reports_saturation_immediately()
    {
        var actions = new QylResourceActions();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var pending = Enumerable.Range(0, QylResourceActions.Capacity)
            .Select(index => actions.RequestAsync(
                $"resource-{index}",
                QylResourceAction.Restart,
                cancellation.Token))
            .ToArray();

        var saturated = await actions.RequestAsync(
            "overflow",
            QylResourceAction.Restart,
            TestContext.Current.CancellationToken);

        Assert.Equal(QylResourceActionStatus.Conflict, saturated.Status);
        Assert.Contains("capacity", saturated.Reason, StringComparison.OrdinalIgnoreCase);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.WhenAll(pending));
    }

    private static int ClaimLoopbackPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static Task<HttpResponseMessage> PostBodylessAsync(
        HttpClient client,
        string uri,
        CancellationToken cancellationToken) =>
        client.PostAsync(uri, content: null, cancellationToken);

    private static string? FindExecutable(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(directory => Path.Combine(directory, name))
        .FirstOrDefault(File.Exists);

    private static async Task WaitForStateAsync(
        QylResourceRegistry registry,
        QylLogStore logs,
        string resource,
        ResourceLifecycle expected,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (registry.Snapshot.TryGetValue(resource, out var state))
            {
                if (state.Lifecycle == expected) return;
                if (state.Lifecycle == ResourceLifecycle.Failed)
                {
                    var output = string.Join(
                        Environment.NewLine,
                        logs.Snapshot(resource).Select(static line => line.Line));
                    throw new InvalidOperationException(
                        $"Resource '{resource}' failed while waiting for {expected}: {state.LastError}" +
                        $"{Environment.NewLine}process output:{Environment.NewLine}{output}");
                }
            }

            await Task.Delay(20, cancellationToken);
        }
    }

    private static async Task WaitForLogCountAsync(
        QylLogStore logs,
        string resource,
        int expected,
        CancellationToken cancellationToken)
    {
        while (logs.Snapshot(resource).Count(static line => line.Line == "started") < expected)
            await Task.Delay(20, cancellationToken);
    }
}
