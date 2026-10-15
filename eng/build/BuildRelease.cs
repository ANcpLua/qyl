using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// release.sh's verification, rerunnable without republishing: wait until
/// nuget.org serves all eight packages of the release, then install the published qyl tool from
/// nuget.org into a clean tool path (its own NuGet packages folder, no HTTP cache) and run
/// QylToolSmoke's installed-tool mode against it.
/// </summary>
interface IVerifyRelease : IPack
{
    [Parameter("VerifyRelease: the released version to verify on nuget.org")]
    string? PublishedVersion => TryGetValue(() => PublishedVersion);

    Target VerifyRelease => d => d
        .Description("Wait until nuget.org serves --published-version, then smoke the published qyl tool")
        .Requires(() => PublishedVersion)
        .Executes(() =>
        {
            var version = PublishedVersion!;
            ReleaseIndex.Wait(
                RootDirectory / "artifacts" / $"release-{version}-index.status",
                TimeSpan.FromMinutes(45),
                [.. ShippablePackageIds.Select(id => ReleaseIndex.NuGet(id, version))]);

            var scratch = (AbsolutePath)Path.Combine(Path.GetTempPath(), $"qyl-verify-release-{Guid.NewGuid():N}");
            try
            {
                scratch.CreateDirectory();
                var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                    .ToDictionary(static entry => (string)entry.Key, static entry => (string?)entry.Value ?? string.Empty, StringComparer.Ordinal);
                environment["NUGET_PACKAGES"] = scratch / "packages";
                ProcessTasks.StartProcess(
                        "dotnet",
                        $"tool install qyl --tool-path \"{scratch / "tool"}\" --version {version} --source https://api.nuget.org/v3/index.json --no-http-cache",
                        scratch,
                        environment)
                    .AssertZeroExitCode();
                DotNetTasks.DotNetRun(s => s
                    .SetProjectFile(QylToolSmokeProject)
                    .SetConfiguration(Configuration)
                    .SetApplicationArguments("--installed", scratch / "tool" / (OperatingSystem.IsWindows() ? "qyl.exe" : "qyl"), "--version", version));
            }
            finally
            {
                scratch.DeleteDirectory();
            }
        });
}

/// <summary>
/// VerifyRelease's index wait. Polls NuGet's flat-container index, the endpoint a restore reads,
/// with a backoff from 5 s to 60 s under one deadline, and records the outcome in
/// artifacts/release-&lt;version&gt;-index.status ("waiting", "indexed" or "timed-out", with the elapsed
/// seconds); release.sh reads it to tell a slow index (retry) from a broken package (patch release)
/// and refreshes the snapshot only after "indexed".
/// </summary>
static class ReleaseIndex
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    internal sealed record Probe(string Name, Uri Url, Func<JsonElement, bool> Serves);

    internal static Probe NuGet(string id, string version) => new(
        $"{id} {version} (nuget.org)",
        new Uri($"https://api.nuget.org/v3-flatcontainer/{id.ToLowerInvariant()}/index.json"),
        index => index.GetProperty("versions").EnumerateArray()
            .Any(listed => string.Equals(listed.GetString(), version, StringComparison.OrdinalIgnoreCase)));

    internal static void Wait(AbsolutePath statusFile, TimeSpan deadline, IReadOnlyList<Probe> probes)
    {
        var clock = Stopwatch.StartNew();
        var pending = probes.ToList();
        var delay = TimeSpan.FromSeconds(5);
        statusFile.Parent.CreateDirectory();
        Write(statusFile, "waiting", clock);
        while (true)
        {
            pending.RemoveAll(probe =>
            {
                if (!Serves(probe))
                    return false;
                Log.Information("indexed: {Probe} after {Seconds} s", probe.Name, (int)clock.Elapsed.TotalSeconds);
                return true;
            });
            if (pending.Count == 0)
            {
                Write(statusFile, "indexed", clock);
                return;
            }

            if (clock.Elapsed + delay > deadline)
            {
                Write(statusFile, "timed-out", clock);
                throw new TimeoutException(
                    $"not indexed after {(int)clock.Elapsed.TotalSeconds} s: {string.Join(", ", pending.Select(static probe => probe.Name))}");
            }

            Thread.Sleep(delay);
            delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
        }
    }

    static bool Serves(Probe probe)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, probe.Url);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using var response = Http.Send(request);
            if (!response.IsSuccessStatusCode)
                return false;
            using var document = JsonDocument.Parse(response.Content.ReadAsStream());
            return probe.Serves(document.RootElement);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    static void Write(AbsolutePath statusFile, string state, Stopwatch clock) =>
        File.WriteAllText(statusFile,
            $"{state} elapsed={(int)clock.Elapsed.TotalSeconds}s at={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\n");
}
