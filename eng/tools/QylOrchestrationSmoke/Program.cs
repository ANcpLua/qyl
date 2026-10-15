using System.Globalization;
using Qyl.Orchestration;

if (Environment.GetEnvironmentVariable("ORCHESTRATION_SMOKE_WORKER") is { } mode)
{
    if (Environment.GetEnvironmentVariable("ORCHESTRATION_SMOKE_PID") is { } file)
        await File.WriteAllTextAsync(file, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    if (mode == "fail")
    {
        await Console.Error.WriteLineAsync("orchestration smoke startup failure");
        return 17;
    }
    var worker = WebApplication.CreateBuilder(args);
    worker.Logging.ClearProviders();
    await using var app = worker.Build();
    if (worker.Configuration["services:payments:http:0"] is { } dependency)
    {
        using var dependencyClient = new HttpClient();
        using var healthy = await dependencyClient.GetAsync(new Uri(new Uri(dependency), "/health"));
        healthy.EnsureSuccessStatusCode();
    }
    app.MapGet("/health", () => mode == "unhealthy"
        ? Results.StatusCode(503)
        : Results.Text(Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));
    app.MapGet("/dependency", () => worker.Configuration["services:payments:http:0"] ?? "none");
    app.MapGet("/value", () => Environment.GetEnvironmentVariable("ORCHESTRATION_SMOKE_VALUE") ?? "missing");
    await app.RunAsync();
    return 0;
}

if (args is ["--expected-version", var expectedVersion])
    Smoke.Require(typeof(QylApplication).Assembly.GetName().Version?.ToString(3) == expectedVersion,
        "The consumer loaded a different orchestration package version.");

var scratch = Path.Combine(Path.GetTempPath(), $"qyl-orchestration-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(scratch);
try
{
    var assembly = typeof(Smoke).Assembly.Location;
    var builder = new QylApplicationBuilder { StartupTimeoutSeconds = 10, MaxRestarts = 0 };
    var payments = Smoke.Worker(builder, "payments", assembly, "ready");
    Smoke.Worker(builder, "orders", assembly, "ready")
        .WithReference(payments).WaitFor(payments)
        .WithoutEnvironmentPrefix("ORCHESTRATION_SMOKE_")
        .WithEnvironment("ORCHESTRATION_SMOKE_VALUE", "argument with spaces");
    await using (var app = builder.Build())
    {
        await Smoke.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => payments.WithEnvironment("LATE", "invalid")));
        await app.StartAsync();
        using var client = new HttpClient();
        var paymentAddress = app.GetEndpoint("payments");
        var orderAddress = app.GetEndpoint("orders");
        Smoke.Require(paymentAddress.Port != orderAddress.Port, "Service ports collided.");
        Smoke.Require(await client.GetStringAsync(new Uri(orderAddress, "/dependency")) == paymentAddress.ToString(),
            "The reference endpoint did not reach the child.");
        Smoke.Require(await client.GetStringAsync(new Uri(orderAddress, "/value")) == "argument with spaces",
            "Explicit environment must survive inherited-prefix removal.");
        using var paymentProcess = Process.GetProcessById(int.Parse(
            await client.GetStringAsync(new Uri(paymentAddress, "/health")), CultureInfo.InvariantCulture));
        using var orderProcess = Process.GetProcessById(int.Parse(
            await client.GetStringAsync(new Uri(orderAddress, "/health")), CultureInfo.InvariantCulture));
        await app.DisposeAsync();
        Smoke.Require(paymentProcess.HasExited && orderProcess.HasExited, "Dispose left a child alive.");
    }
    Console.WriteLine("PASS: real services, references, dependency readiness, environment, shutdown");

    var failedPeer = Path.Combine(scratch, "failed-peer.pid");
    var failing = new QylApplicationBuilder { StartupTimeoutSeconds = 10, MaxRestarts = 0 };
    var peer = Smoke.Worker(failing, "peer", assembly, "ready").WithEnvironment("ORCHESTRATION_SMOKE_PID", failedPeer);
    Smoke.Worker(failing, "broken", assembly, "fail").WaitFor(peer);
    await using (var app = failing.Build())
    {
        var error = await Smoke.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        Smoke.Require(error.Message.Contains("exited with code 17", StringComparison.Ordinal)
            && error.Message.Contains("orchestration smoke startup failure", StringComparison.Ordinal),
            "Startup failure lost the exit code or child's stderr: " + error.Message);
        Smoke.RequireStopped(failedPeer);
    }
    Console.WriteLine("PASS: early exit diagnostics and peer cleanup");

    var cancelledPid = Path.Combine(scratch, "cancelled.pid");
    var cancelled = new QylApplicationBuilder { StartupTimeoutSeconds = 10, MaxRestarts = 0 };
    var waiting = Smoke.Worker(cancelled, "waiting", assembly, "unhealthy").WithEnvironment("ORCHESTRATION_SMOKE_PID", cancelledPid);
    var deferredPid = Path.Combine(scratch, "deferred.pid");
    Smoke.Worker(cancelled, "deferred", assembly, "ready").WithEnvironment("ORCHESTRATION_SMOKE_PID", deferredPid).WaitFor(waiting);
    await using (var app = cancelled.Build())
    {
        using var cancellation = new CancellationTokenSource();
        var start = app.StartAsync(cancellation.Token);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(cancelledPid) || !int.TryParse(await File.ReadAllTextAsync(cancelledPid, deadline.Token), out _))
            await Task.Delay(20, deadline.Token);
        await cancellation.CancelAsync();
        await Smoke.ThrowsAsync<OperationCanceledException>(() => start);
        Smoke.RequireStopped(cancelledPid);
        Smoke.Require(!File.Exists(deferredPid), "A dependency launched before its prerequisite was ready.");
    }
    Console.WriteLine("PASS: cancellation stops children and prevents deferred startup");

    var timedOutPid = Path.Combine(scratch, "timeout.pid");
    var timedOut = new QylApplicationBuilder { StartupTimeoutSeconds = 3, MaxRestarts = 0 };
    Smoke.Worker(timedOut, "unhealthy", assembly, "unhealthy").WithEnvironment("ORCHESTRATION_SMOKE_PID", timedOutPid);
    await using (var app = timedOut.Build())
    {
        await Smoke.ThrowsAsync<TimeoutException>(() => app.StartAsync());
        Smoke.RequireStopped(timedOutPid);
    }
    Console.WriteLine("PASS: readiness deadline and cleanup");

    var cycle = new QylApplicationBuilder();
    var first = cycle.AddExecutable("first", "unused", []);
    var second = cycle.AddExecutable("second", "unused", []).WaitFor(first);
    first.WaitFor(second);
    var cycleError = await Smoke.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => cycle.Build()));
    Smoke.Require(cycleError.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase), "Cycle was not rejected.");
    var other = new QylApplicationBuilder().AddExecutable("other", "unused", []);
    await Smoke.ThrowsAsync<ArgumentException>(() => Task.Run(() => first.WithReference(other)));
    Console.WriteLine("PASS: dependency cycle and cross-application reference validation");
    Console.WriteLine($"VERIFIED Qyl.Orchestration {typeof(QylApplication).Assembly.GetName().Version}");
    return 0;
}
finally
{
    Directory.Delete(scratch, recursive: true);
}

internal static class Smoke
{
    internal static QylProcessBuilder Worker(QylApplicationBuilder builder, string name, string assembly, string mode) =>
        builder.AddDotnetService(name, assembly)
            .WithEnvironment("ORCHESTRATION_SMOKE_WORKER", mode)
            .WithHealthCheck(requireProcessId: true);

    internal static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    internal static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T exception)
        {
            return exception;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    internal static void RequireStopped(string pidFile)
    {
        Require(File.Exists(pidFile), "The child never wrote its process ID.");
        var id = int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture);
        try
        {
            using var process = Process.GetProcessById(id);
            Require(process.HasExited, $"Child {id} is still alive.");
        }
        catch (ArgumentException)
        {
            // The operating system has already removed the exited process.
        }
    }
}
