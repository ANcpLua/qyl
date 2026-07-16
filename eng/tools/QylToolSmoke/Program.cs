using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

var installedMode = args is ["--installed", _, "--version", _];
var packageMode = args is [_] or [_, "--skip-live"];
if (!installedMode && !packageMode)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  dotnet run --project eng/tools/QylToolSmoke -- <package-directory> [--skip-live]");
    Console.Error.WriteLine("  dotnet run --project eng/tools/QylToolSmoke -- --installed <qyl-path> --version <version>");
    return 2;
}

var rid = RuntimeInformation.RuntimeIdentifier;
var skipLive = args is [_, "--skip-live"];
var expectedVersion = installedMode ? args[3] : string.Empty;
var installedTool = installedMode ? Path.GetFullPath(args[1]) : null;
string? packageDirectory = null;
PackageInfo? implementation = null;
if (packageMode)
{
    packageDirectory = Path.GetFullPath(args[0]);
    if (!Directory.Exists(packageDirectory))
        throw new DirectoryNotFoundException($"Package directory '{packageDirectory}' does not exist.");

    var packages = Directory.GetFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
        .Select(PackageInfo.Read)
        .ToArray();
    var outer = packages.Single(static package => string.Equals(package.Id, "qyl", StringComparison.OrdinalIgnoreCase));
    implementation = packages.Single(package =>
        string.Equals(package.Id, $"qyl.{rid}", StringComparison.OrdinalIgnoreCase));
    expectedVersion = outer.Version;

    outer.RequireEntry("tools/net10.0/any/DotnetToolSettings.xml");
    outer.RequireText("tools/net10.0/any/DotnetToolSettings.xml", $"RuntimeIdentifier=\"{rid}\"");
    implementation.RequireEntry($"tools/net10.0/{rid}/DotnetToolSettings.xml");
    implementation.RequireEntry($"tools/net10.0/{rid}/collector/qyl.collector.dll");
    implementation.RequireEntry($"tools/net10.0/{rid}/collector/qyl.collector.deps.json");
    implementation.RequireEntry($"tools/net10.0/{rid}/collector/qyl.collector.runtimeconfig.json");
    implementation.RequireEntry($"tools/net10.0/{rid}/collector/{NativeDuckDbName()}");
}
else if (!File.Exists(installedTool))
{
    throw new FileNotFoundException("Installed qyl command was not found.", installedTool);
}

var scratch = Path.Combine(Path.GetTempPath(), $"qyl-tool-smoke-{Guid.NewGuid():N}");
var toolDirectory = Path.Combine(scratch, "tool");
var dataDirectory = Path.Combine(scratch, "data");
Directory.CreateDirectory(dataDirectory);

try
{
    var tool = installedTool;
    if (tool is null)
    {
        Progress($"installing qyl {expectedVersion} from {packageDirectory}");
        var install = await RunAsync(
            "dotnet",
            [
                "tool", "install", "qyl", "--tool-path", toolDirectory,
                "--version", expectedVersion, "--add-source", packageDirectory!,
                "--ignore-failed-sources", "--no-cache"
            ],
            scratch,
            TimeSpan.FromMinutes(2));
        install.RequireExitCode(0, "clean tool install");
        tool = ResolveInstalledTool(toolDirectory);
    }

    if (!File.Exists(tool)) throw new FileNotFoundException("Installed qyl command was not created.", tool);

    Progress($"exercising {tool}: --version");
    var version = await RunAsync(tool, ["--version"], dataDirectory, TimeSpan.FromSeconds(20));
    version.RequireExitCode(0, "qyl --version");
    if (!string.Equals(version.Stdout.Trim(), expectedVersion, StringComparison.Ordinal))
        throw new InvalidOperationException($"Installed qyl reported '{version.Stdout.Trim()}', expected '{expectedVersion}'.");

    Progress("exercising: removed 'run' command fails closed");
    var invalid = await RunAsync(tool, ["run"], dataDirectory, TimeSpan.FromSeconds(20));
    invalid.RequireExitCode(2, "removed qyl run command");

    TcpListener? collision = null;
    try
    {
        collision = new TcpListener(IPAddress.Loopback, 5100);
        try
        {
            collision.Start();
        }
        catch (SocketException)
        {
            // A developer may already have qyl running. The existing listener is
            // sufficient to prove that the packaged command fails closed instead
            // of attaching its health checks to an unrelated process.
            collision.Dispose();
            collision = null;
        }

        Progress("exercising: port-collision fails closed");
        var collided = await RunAsync(tool, ["up"], dataDirectory, TimeSpan.FromSeconds(20));
        if (collided.ExitCode == 0 || !collided.Stderr.Contains("127.0.0.1:5100 is already in use", StringComparison.Ordinal))
            throw new InvalidOperationException($"Port-collision smoke failed.{Environment.NewLine}{collided}");
    }
    finally
    {
        collision?.Dispose();
    }

    if (!skipLive)
    {
        Progress("exercising: live 'qyl up' end-to-end");
        await RunLiveAsync(tool, dataDirectory);
    }

    var source = implementation is null ? tool : implementation.Path;
    Console.WriteLine($"qyl {expectedVersion} installed-tool smoke passed for {rid} ({source}).");
    return 0;
}
finally
{
    if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
}

// Timestamped phase markers: the CI leg has hung without any output before, so make it
// obvious from a cancelled step's log which phase never returned.
static void Progress(string message) =>
    Console.WriteLine($"[{DateTimeOffset.UtcNow:HH:mm:ss}] {message}");

static string ResolveInstalledTool(string toolDirectory)
{
    // .NET 10 SDK tool-path installs shim Windows tools as qyl.cmd (no .exe shim).
    string[] commandNames = OperatingSystem.IsWindows() ? ["qyl.exe", "qyl.cmd", "qyl"] : ["qyl", "qyl.exe"];
    foreach (var commandName in commandNames)
    {
        var candidate = Path.Combine(toolDirectory, commandName);
        if (File.Exists(candidate)) return candidate;
    }

    var contents = Directory.Exists(toolDirectory)
        ? string.Join(", ", Directory.EnumerateFileSystemEntries(toolDirectory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal))
        : "<directory missing>";
    throw new FileNotFoundException(
        $"The qyl tool installation did not create a supported command shim in '{toolDirectory}'. " +
        $"Top-level contents: {contents}.");
}

static async Task RunLiveAsync(string tool, string workingDirectory)
{
    var stdout = new ConcurrentQueue<string>();
    var stderr = new ConcurrentQueue<string>();
    using var process = Start(tool, ["up"], workingDirectory, stdout, stderr);
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        await WaitUntilAsync(async () =>
        {
            if (!await IsHealthyAsync(client, "http://127.0.0.1:5100/health")) return false;
            if (!await IsHealthyAsync(client, "http://127.0.0.1:5200/health")) return false;

            using var response = await client.GetAsync("http://127.0.0.1:18888/runner/resources");
            if (!response.IsSuccessStatusCode) return false;
            await using var body = await response.Content.ReadAsStreamAsync();
            using var resources = await JsonDocument.ParseAsync(body);
            var ready = resources.RootElement.EnumerateArray()
                .Where(static resource =>
                    resource.TryGetProperty("lifecycle", out var lifecycle) &&
                    string.Equals(lifecycle.GetString(), "ready", StringComparison.OrdinalIgnoreCase))
                .Select(static resource => resource.GetProperty("name").GetString())
                .ToHashSet(StringComparer.Ordinal);
            return ready.SetEquals(["collector", "diagnostics"]);
        }, TimeSpan.FromSeconds(90), "qyl collectors and runner API did not become ready");

        Progress("live: collectors and runner API ready");
        var dashboard = await client.GetStringAsync("http://127.0.0.1:5100/");
        if (!dashboard.Contains("id=\"root\"", StringComparison.Ordinal))
            throw new InvalidOperationException("The packaged collector did not serve the embedded dashboard.");

        await VerifyIngestAndReadbackAsync(client);
        Progress("live: ingest and readback verified, shutting down");

        if (OperatingSystem.IsWindows())
        {
            process.Kill(entireProcessTree: true);
        }
        else
        {
            var signal = await RunAsync("/bin/kill", ["-INT", process.Id.ToString(CultureInfo.InvariantCulture)],
                workingDirectory, TimeSpan.FromSeconds(10));
            signal.RequireExitCode(0, "send Ctrl-C to qyl");
        }

        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(shutdown.Token);
        if (!OperatingSystem.IsWindows() && process.ExitCode != 0)
            throw new InvalidOperationException($"qyl exited {process.ExitCode} after Ctrl-C.");

        await WaitUntilAsync(
            () => Task.FromResult(ArePortsFree([5100, 5200, 4317, 4318, 18888])),
            TimeSpan.FromSeconds(30),
            "qyl left a collector or runner listener behind after shutdown");
        Progress("live: shutdown clean, all ports released");
    }
    catch (Exception exception)
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        throw new InvalidOperationException(
            $"Installed-tool live smoke failed: {exception.Message}{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{string.Join(Environment.NewLine, stdout)}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{string.Join(Environment.NewLine, stderr)}", exception);
    }
}

static async Task VerifyIngestAndReadbackAsync(HttpClient client)
{
    const string traceId = "0af7651916cd43dd8448eb211c80319c";
    const string spanId = "b7ad6b7169203331";
    var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
    var payload = JsonSerializer.Serialize(new
    {
        resourceSpans = new[]
        {
            new
            {
                resource = new
                {
                    attributes = new[]
                    {
                        new { key = "service.name", value = new { stringValue = "qyl-tool-smoke" } }
                    }
                },
                scopeSpans = new[]
                {
                    new
                    {
                        scope = new { name = "qyl-tool-smoke" },
                        spans = new[]
                        {
                            new
                            {
                                // OTLP/JSON deliberately uses hex ids (unlike generic protojson,
                                // which uses base64 for bytes). This exercises the collector's
                                // standards-compatible JSON normalization path.
                                traceId,
                                spanId,
                                name = "installed-tool-span",
                                kind = 1,
                                startTimeUnixNano = start.ToString(CultureInfo.InvariantCulture),
                                endTimeUnixNano = (start + 1_000_000).ToString(CultureInfo.InvariantCulture),
                                status = new { code = 1 }
                            }
                        }
                    }
                }
            }
        }
    });
    using var ingestContent = new StringContent(payload, Encoding.UTF8, "application/json");
    using var ingest = await client.PostAsync("http://127.0.0.1:4318/v1/traces", ingestContent);
    if (!ingest.IsSuccessStatusCode)
    {
        var error = await ingest.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"Installed collector rejected OTLP trace ingest with {(int)ingest.StatusCode}: {error}");
    }

    await WaitUntilAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:5100/api/v1/traces?limit=100");
        request.Headers.Add("X-Qyl-Project", "default");
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return false;
        var body = await response.Content.ReadAsStringAsync();
        return body.Contains(traceId, StringComparison.Ordinal) && body.Contains("qyl-tool-smoke", StringComparison.Ordinal);
    }, TimeSpan.FromSeconds(20), "OTLP trace was not readable from the product API");
}

static async Task<bool> IsHealthyAsync(HttpClient client, string uri)
{
    try
    {
        using var response = await client.GetAsync(uri);
        return response.IsSuccessStatusCode;
    }
    catch (HttpRequestException)
    {
        return false;
    }
    catch (TaskCanceledException)
    {
        return false;
    }
}

static bool ArePortsFree(IEnumerable<int> ports)
{
    var listeners = new List<TcpListener>();
    try
    {
        foreach (var port in ports)
        {
            TcpListener? listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listeners.Add(listener);
                listener = null;
            }
            finally
            {
                listener?.Dispose();
            }
        }

        return true;
    }
    catch (SocketException)
    {
        return false;
    }
    finally
    {
        foreach (var listener in listeners) listener.Stop();
    }
}

static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string failure)
{
    var deadline = DateTimeOffset.UtcNow + timeout;
    while (DateTimeOffset.UtcNow < deadline)
    {
        if (await condition()) return;
        await Task.Delay(TimeSpan.FromMilliseconds(250));
    }

    throw new TimeoutException(failure);
}

static Process Start(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    ConcurrentQueue<string> stdout,
    ConcurrentQueue<string> stderr)
{
    // CreateProcess cannot exec a .cmd shim directly (and .NET refuses since the BatBadBut
    // mitigation), so route shims through cmd.exe. Arguments here are fixed tokens and
    // space-free paths; cmd metacharacter quoting is deliberately not handled.
    var isCmdShim = OperatingSystem.IsWindows() &&
                    (fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));
    var startInfo = new ProcessStartInfo(isCmdShim
        ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
        : fileName)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    if (isCmdShim)
    {
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(fileName);
    }

    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

    var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is { } line) stdout.Enqueue(line);
    };
    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is { } line) stderr.Enqueue(line);
    };
    if (!process.Start()) throw new InvalidOperationException($"Could not start '{fileName}'.");
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static async Task<ProcessResult> RunAsync(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    TimeSpan timeout)
{
    var stdout = new ConcurrentQueue<string>();
    var stderr = new ConcurrentQueue<string>();
    using var process = Start(fileName, arguments, workingDirectory, stdout, stderr);
    using var cancellation = new CancellationTokenSource(timeout);
    try
    {
        await process.WaitForExitAsync(cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException($"'{fileName} {string.Join(' ', arguments)}' did not exit within {timeout}.");
    }

    return new ProcessResult(process.ExitCode, string.Join(Environment.NewLine, stdout),
        string.Join(Environment.NewLine, stderr));
}

static string NativeDuckDbName()
{
    if (OperatingSystem.IsWindows()) return "duckdb.dll";
    if (OperatingSystem.IsMacOS()) return "libduckdb.dylib";
    return "libduckdb.so";
}

internal sealed record PackageInfo(string Path, string Id, string Version, HashSet<string> Entries)
{
    internal static PackageInfo Read(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var nuspec = archive.Entries.Single(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);
        var metadata = document.Root?.Elements().Single(static element => element.Name.LocalName == "metadata")
                       ?? throw new InvalidDataException($"Package '{path}' has no nuspec metadata.");
        var id = metadata.Elements().Single(static element => element.Name.LocalName == "id").Value;
        var version = metadata.Elements().Single(static element => element.Name.LocalName == "version").Value;
        return new PackageInfo(path, id, version,
            archive.Entries.Select(static entry => entry.FullName).ToHashSet(StringComparer.Ordinal));
    }

    internal void RequireEntry(string path)
    {
        if (!Entries.Contains(path))
            throw new InvalidDataException($"Package '{Path}' is missing required entry '{path}'.");
    }

    internal void RequireText(string path, string expected)
    {
        using var archive = ZipFile.OpenRead(Path);
        var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"Package '{Path}' is missing '{path}'.");
        using var reader = new StreamReader(entry.Open());
        var text = reader.ReadToEnd();
        if (!text.Contains(expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Package entry '{path}' does not contain '{expected}'.");
    }
}

internal sealed record ProcessResult(int ExitCode, string Stdout, string Stderr)
{
    internal void RequireExitCode(int expected, string operation)
    {
        if (ExitCode != expected)
            throw new InvalidOperationException($"{operation} exited {ExitCode}, expected {expected}.{Environment.NewLine}{this}");
    }

    public override string ToString() =>
        $"stdout:{Environment.NewLine}{Stdout}{Environment.NewLine}stderr:{Environment.NewLine}{Stderr}";
}
