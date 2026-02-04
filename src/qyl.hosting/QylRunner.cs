using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Qyl.Hosting.Resources;

namespace Qyl.Hosting;

/// <summary>
/// Orchestrates the startup and management of qyl resources.
/// </summary>
internal sealed class QylRunner : IDisposable
{
    private readonly QylAppBuilder _builder;
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly ConcurrentDictionary<string, ResourceState> _states = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public QylRunner(QylAppBuilder builder)
    {
        _builder = builder;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdownCts.Token);
        var token = linkedCts.Token;

        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            PrintBanner();

            // Start qyl collector first (if dashboard enabled)
            if (_builder.Options.GenAI || _builder.Options.CostTracking)
            {
                await StartCollectorAsync(token);
            }

            // Sort resources by dependencies (topological sort)
            var orderedResources = TopologicalSort(_builder.Resources.Values);

            // Start resources
            foreach (var resource in orderedResources)
            {
                await StartResourceAsync(resource, token);
            }

            PrintStatus();

            // Wait for shutdown
            await WaitForShutdownAsync(token);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            await ShutdownAsync();
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _shutdownCts.Cancel();
    }

    private async Task StartCollectorAsync(CancellationToken ct)
    {
        PrintResourceStart("qyl", "collector");

        // Check if qyl.collector is available locally, otherwise use Docker
        var collectorPath = FindCollectorExecutable();

        if (collectorPath is not null)
        {
            // Run locally
            await StartProcessAsync("qyl", collectorPath, "", new Dictionary<string, string>
            {
                ["QYL_PORT"] = _builder.Options.DashboardPort.ToString(),
                ["QYL_GRPC_PORT"] = _builder.Options.OtlpPort.ToString(),
                ["QYL_TOKEN"] = _builder.Options.Token ?? GenerateToken(),
                ["QYL_DATA_PATH"] = Path.Combine(_builder.Options.DataPath, "qyl.duckdb")
            }, ct);
        }
        else
        {
            // Use Docker
            var args = $"run --rm -p {_builder.Options.DashboardPort}:5100 -p {_builder.Options.OtlpPort}:4317 " +
                       $"-e QYL_TOKEN={_builder.Options.Token ?? GenerateToken()} " +
                       $"-v qyl-data:/app/data ghcr.io/ancplua/qyl:latest";

            await StartProcessAsync("qyl", "docker", args, new Dictionary<string, string>(), ct);
        }

        _states["qyl"] = ResourceState.Running;
        PrintResourceReady("qyl", $"http://localhost:{_builder.Options.DashboardPort}");
    }

    private async Task StartResourceAsync(IQylResource resource, CancellationToken ct)
    {
        // Wait for dependencies
        foreach (var dep in resource.Dependencies)
        {
            while (_states.GetValueOrDefault(dep.Name) != ResourceState.Running)
            {
                if (ct.IsCancellationRequested) return;
                await Task.Delay(100, ct);
            }
        }

        PrintResourceStart(resource.Name, resource.Type);

        var (command, args, workingDir) = GetStartCommand(resource);

        await StartProcessAsync(resource.Name, command, args, resource.Environment, ct, workingDir);

        // Wait for health check if configured
        if (resource.HealthEndpoint is not null)
        {
            var port = resource.Ports.Count > 0 ? resource.Ports[0].HostPort : 5000;
            await WaitForHealthAsync(resource.Name, port, resource.HealthEndpoint, ct);
        }
        else
        {
            // Give it a moment to start
            await Task.Delay(500, ct);
        }

        _states[resource.Name] = ResourceState.Running;

        // Find external port without FirstOrDefault
        PortBinding? externalPort = null;
        foreach (var port in resource.Ports)
        {
            if (port.External)
            {
                externalPort = port;
                break;
            }
        }

        var url = externalPort is not null ? $"http://localhost:{externalPort.HostPort}" : null;
        PrintResourceReady(resource.Name, url);
    }

    private static (string command, string args, string? workingDir) GetStartCommand(IQylResource resource)
    {
        return resource switch
        {
            ProjectResource<IProjectMetadata> project =>
                ("dotnet", $"run --project \"{project.ProjectPath}\" --no-launch-profile", null),

            ViteResource vite =>
                ("npm", $"run {vite.RunScript}", vite.WorkingDirectory),

            NodeResource node =>
                (node.PackageManager == "bun" ? "bun" : "node", node.ScriptPath, node.WorkingDirectory),

            PythonResource python =>
                (python.UseUv ? "uv" : "python", python.UseUv ? $"run {python.ScriptPath}" : python.ScriptPath, python.WorkingDirectory),

            UvicornResource uvicorn =>
                (uvicorn.UseUv ? "uv" : "uvicorn",
                 uvicorn.UseUv
                     ? $"run uvicorn {uvicorn.AppModule} --host 0.0.0.0 --port {(uvicorn.Ports.Count > 0 ? uvicorn.Ports[0].HostPort : 8000)}"
                     : $"{uvicorn.AppModule} --host 0.0.0.0 --port {(uvicorn.Ports.Count > 0 ? uvicorn.Ports[0].HostPort : 8000)}",
                 uvicorn.WorkingDirectory),

            ContainerResource container =>
                ("docker", BuildDockerArgs(container), null),

            PostgresResource postgres =>
                ("docker", BuildPostgresArgs(postgres), null),

            _ => throw new NotSupportedException($"Resource type {resource.GetType().Name} is not supported")
        };
    }

    private static string BuildDockerArgs(ContainerResource container)
    {
        var args = new List<string> { "run", "--rm", "--name", container.Name };

        foreach (var port in container.Ports)
            args.Add($"-p {port.HostPort}:{port.ContainerPort}");

        foreach (var (key, value) in container.Environment)
            args.Add($"-e {key}={value}");

        foreach (var volume in container.Volumes)
            args.Add($"-v {volume}");

        args.Add(container.Image);
        args.AddRange(container.Args);

        return string.Join(" ", args);
    }

    private static string BuildPostgresArgs(PostgresResource postgres)
    {
        var args = new List<string> { "run", "--rm", "--name", postgres.Name };

        foreach (var port in postgres.Ports)
            args.Add($"-p {port.HostPort}:{port.ContainerPort}");

        foreach (var (key, value) in postgres.Environment)
            args.Add($"-e {key}={value}");

        args.Add("postgres:16-alpine");

        return string.Join(" ", args);
    }

    private async Task StartProcessAsync(
        string name,
        string command,
        string args,
        IReadOnlyDictionary<string, string> env,
        CancellationToken ct,
        string? workingDir = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (workingDir is not null)
            psi.WorkingDirectory = workingDir;

        foreach (var (key, value) in env)
            psi.Environment[key] = value;

        var process = Process.Start(psi);
        if (process is null)
            throw new InvalidOperationException($"Failed to start {name}");

        _processes[name] = process;

        // Stream output - capture locals for async lambda
        var processStdOut = process.StandardOutput;
        var processStdErr = process.StandardError;

        _ = Task.Run(async () =>
        {
            try
            {
                while (await processStdOut.ReadLineAsync(ct) is { } line)
                {
                    if (ct.IsCancellationRequested) break;
                    PrintLog(name, line);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, ct);

        _ = Task.Run(async () =>
        {
            try
            {
                while (await processStdErr.ReadLineAsync(ct) is { } line)
                {
                    if (ct.IsCancellationRequested) break;
                    PrintLog(name, line, isError: true);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, ct);
    }

    private static async Task WaitForHealthAsync(string name, int port, string endpoint, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var url = $"http://localhost:{port}{endpoint}";

        for (var i = 0; i < 60; i++)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Keep trying
            }

            await Task.Delay(500, ct);
        }

        PrintLog(name, $"Warning: Health check at {endpoint} did not respond", isError: true);
    }

    private static async Task WaitForShutdownAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    private async Task ShutdownAsync()
    {
        Console.WriteLine();
        Console.WriteLine("  Shutting down...");

        foreach (var (_, process) in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }

                process.Dispose();
            }
            catch
            {
                // Best effort
            }
        }

        Console.WriteLine("  Goodbye!");
    }

    private static List<IQylResource> TopologicalSort(IEnumerable<IQylResource> resources)
    {
        var sorted = new List<IQylResource>();
        var visited = new HashSet<string>();

        void Visit(IQylResource resource)
        {
            if (visited.Contains(resource.Name)) return;
            visited.Add(resource.Name);

            foreach (var dep in resource.Dependencies)
                Visit(dep);

            sorted.Add(resource);
        }

        foreach (var resource in resources)
            Visit(resource);

        return sorted;
    }

    private static string? FindCollectorExecutable()
    {
        // Check for local qyl.collector in common locations
        string[] paths =
        [
            Path.Combine(AppContext.BaseDirectory, "qyl.collector"),
            Path.Combine(AppContext.BaseDirectory, "qyl.collector.exe"),
            "/usr/local/bin/qyl",
            Environment.ExpandEnvironmentVariables("%USERPROFILE%/.qyl/bin/qyl.exe")
        ];

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string GenerateToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var result = new char[32];
        for (var i = 0; i < 32; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }
        return new string(result);
    }

    #region Console Output

    private static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("  \u001b[38;5;141m┌─────────────────────────────────────────┐\u001b[0m");
        Console.WriteLine("  \u001b[38;5;141m│\u001b[0m  \u001b[1;38;5;147mqyl\u001b[0m - GenAI-native orchestration       \u001b[38;5;141m│\u001b[0m");
        Console.WriteLine("  \u001b[38;5;141m└─────────────────────────────────────────┘\u001b[0m");
        Console.WriteLine();
    }

    private static void PrintResourceStart(string name, string type)
    {
        Console.WriteLine($"  \u001b[38;5;245m▸\u001b[0m Starting \u001b[1m{name}\u001b[0m \u001b[38;5;245m({type})\u001b[0m");
    }

    private static void PrintResourceReady(string name, string? url)
    {
        if (url is not null)
            Console.WriteLine($"  \u001b[32m✓\u001b[0m \u001b[1m{name}\u001b[0m ready at \u001b[4;36m{url}\u001b[0m");
        else
            Console.WriteLine($"  \u001b[32m✓\u001b[0m \u001b[1m{name}\u001b[0m ready");
    }

    private void PrintStatus()
    {
        Console.WriteLine();
        Console.WriteLine($"  \u001b[38;5;141mDashboard:\u001b[0m  \u001b[4;36mhttp://localhost:{_builder.Options.DashboardPort}\u001b[0m");
        Console.WriteLine();
        Console.WriteLine("  Press \u001b[1mCtrl+C\u001b[0m to stop");
        Console.WriteLine();
    }

    private static void PrintLog(string name, string message, bool isError = false)
    {
        var color = isError ? "\u001b[31m" : "\u001b[38;5;245m";
        var reset = "\u001b[0m";
        var nameColor = GetResourceColor(name);

        Console.WriteLine($"  {nameColor}[{name}]{reset} {color}{message}{reset}");
    }

    private static string GetResourceColor(string name)
    {
        var hash = name.GetHashCode(StringComparison.Ordinal);
        ReadOnlySpan<int> colors = [141, 147, 153, 159, 165, 171, 177, 183, 189, 195];
        var color = colors[Math.Abs(hash) % colors.Length];
        return $"\u001b[38;5;{color}m";
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Dispose();

        foreach (var (_, process) in _processes)
        {
            try
            {
                process.Dispose();
            }
            catch
            {
                // Best effort
            }
        }
    }
}

internal enum ResourceState
{
    Pending,
    Starting,
    Running,
    Failed,
    Stopped
}
