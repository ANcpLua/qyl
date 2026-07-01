using Microsoft.Extensions.Logging;

namespace Qyl.Run.Internal;

// Drives an OCI runtime (docker/podman) via its CLI to run a container resource. Deliberately CLI-driven
// rather than a Docker/Kubernetes client SDK: Process.Start is pure BCL and AOT-clean, and it avoids the
// heavy reflection-laden dependency tree (KubernetesClient et al.) that makes Aspire's DCP un-AOT-able.
//
// Host ports are assigned atomically by the runtime (`-p 127.0.0.1:0:<containerPort>`, then read back with
// `port`), which side-steps the allocate-then-bind race that DCP-style pre-allocation is prone to.
internal sealed partial class QylContainerLauncher(ILogger<QylContainerLauncher> logger)
{
    public async Task<QylContainerHandle> StartAsync(QylResource resource,
        IReadOnlyDictionary<string, string> referenceEnv, CancellationToken cancellationToken)
    {
        var spec = resource.Container ??
                   throw new InvalidOperationException($"Resource '{resource.Name}' has no container spec.");
        var runtime = ResolveRuntime();
        var name = QylConstants.Container.NamePrefix + resource.Name;

        // Clear any container left behind by a previous crashed run so `run --name` does not collide.
        await RunAsync(runtime, ["rm", "-f", name], cancellationToken).ConfigureAwait(false);

        var arguments = new List<string>(16)
        {
            "run", "-d", "--name", name,
            "-p",
            string.Create(CultureInfo.InvariantCulture, $"{QylConstants.Network.Loopback}:0:{spec.ContainerPort}")
        };
        foreach (var kv in spec.Env)
        {
            arguments.Add("-e");
            arguments.Add($"{kv.Key}={kv.Value}");
        }

        foreach (var kv in referenceEnv)
        {
            arguments.Add("-e");
            arguments.Add($"{kv.Key}={kv.Value}");
        }

        foreach (var volume in spec.Volumes)
        {
            arguments.Add("-v");
            arguments.Add(volume);
        }

        arguments.Add(spec.Image);
        foreach (var arg in spec.Args) arguments.Add(arg);

        var run = await RunAsync(runtime, arguments, cancellationToken).ConfigureAwait(false);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{runtime} run' failed for container '{resource.Name}': {FirstLine(run.StdErr)}");
        }

        var containerId = run.StdOut.Trim();

        var portQuery = await RunAsync(runtime,
            ["port", name, spec.ContainerPort.ToString(CultureInfo.InvariantCulture)], cancellationToken)
            .ConfigureAwait(false);
        var hostPort = ParseHostPort(portQuery.StdOut) ??
                       throw new InvalidOperationException(
                           $"Could not read the mapped host port for container '{resource.Name}'.");

        LogContainerStarted(logger, resource.Name, runtime, hostPort);
        return new QylContainerHandle(runtime, name, containerId, hostPort);
    }

    // Readiness = the container reached and holds the running state. Generic containers (redis, postgres, …)
    // do not serve HTTP, so container state — not an HTTP /health probe — is the correct readiness signal.
    public static async Task<bool> WaitForRunningAsync(QylContainerHandle handle, int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, timeoutSeconds * 1000 / QylConstants.Container.RunningPollIntervalMs);
        for (var i = 0; i < attempts && !cancellationToken.IsCancellationRequested; i++)
        {
            var inspect = await RunAsync(handle.Runtime,
                ["inspect", "-f", "{{.State.Running}} {{.State.Status}}", handle.ContainerName], cancellationToken)
                .ConfigureAwait(false);
            var state = inspect.StdOut.Trim();

            if (state.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (state.EndsWith("exited", StringComparison.OrdinalIgnoreCase)) return false;

            await Task.Delay(QylConstants.Container.RunningPollIntervalMs, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async Task StopAsync(QylContainerHandle handle, CancellationToken cancellationToken)
    {
        await RunAsync(handle.Runtime, ["rm", "-f", handle.ContainerName], cancellationToken).ConfigureAwait(false);
        LogContainerStopped(logger, handle.ContainerName, handle.Runtime);
    }

    private static string ResolveRuntime()
    {
        var configured = Environment.GetEnvironmentVariable(QylConstants.Env.ContainerRuntime);
        return string.IsNullOrWhiteSpace(configured) ? QylConstants.Container.Docker : configured;
    }

    private static async Task<CliResult> RunAsync(string fileName, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new CliResult(process.ExitCode, await stdOutTask.ConfigureAwait(false),
            await stdErrTask.ConfigureAwait(false));
    }

    // `docker port <c> <port>` prints one or more `<host-ip>:<host-port>` lines (IPv4/IPv6). Take the first.
    private static int? ParseHostPort(string portOutput)
    {
        foreach (var line in portOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.LastIndexOf(':');
            if (separator >= 0 &&
                int.TryParse(line.AsSpan(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            {
                return port;
            }
        }

        return null;
    }

    private static string FirstLine(string text)
    {
        var newline = text.IndexOf('\n');
        return (newline < 0 ? text : text[..newline]).Trim();
    }

    [LoggerMessage(EventId = QylConstants.LogEvents.ContainerStarted, Level = LogLevel.Information,
        Message = "Container '{Name}' started via {Runtime} on host port {HostPort}")]
    private static partial void LogContainerStarted(ILogger logger, string name, string runtime, int hostPort);

    [LoggerMessage(EventId = QylConstants.LogEvents.ContainerStopped, Level = LogLevel.Information,
        Message = "Container '{ContainerName}' removed via {Runtime}")]
    private static partial void LogContainerStopped(ILogger logger, string containerName, string runtime);

    private readonly record struct CliResult(int ExitCode, string StdOut, string StdErr);
}

internal sealed record QylContainerHandle(string Runtime, string ContainerName, string ContainerId, int HostPort);
