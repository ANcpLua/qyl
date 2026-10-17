using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// release.sh's verification, rerunnable without republishing: wait until
/// nuget.org serves all nine packages of the release, then install the published qyl tool from
/// nuget.org into a clean tool path (its own NuGet packages folder, no HTTP cache) and run
/// QylToolSmoke's installed-tool mode against it.
/// </summary>
interface IVerifyRelease : IPack
{
    [Parameter("VerifyRelease: the released version to verify on nuget.org")]
    string? PublishedVersion => TryGetValue(() => PublishedVersion);

    Target VerifyReleaseIndex => d => d
        .Description("Wait for the published release package indexes")
        .Requires(() => PublishedVersion)
        .Executes(() =>
        {
            var version = PublishedVersion!;
            ReleaseIndex.Wait(
                RootDirectory / "artifacts" / $"release-{version}-index.status",
                TimeSpan.FromMinutes(45),
                [.. ShippablePackageIds.Select(id => ReleaseIndex.NuGet(id, version)),
                    ReleaseIndex.NuGetRegistration("qyl", version)]);
        });

    Target VerifyRelease => d => d
        .Description("Verify the published qyl tool and orchestration package")
        .DependsOn(VerifyReleaseIndex)
        .Executes(() =>
        {
            var version = PublishedVersion!;
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
                ProcessTasks.StartProcess(
                        "bash",
                        $"\"{RootDirectory / "eng" / "scripts" / "smoke-orchestration-package.sh"}\" {version}",
                        RootDirectory)
                    .AssertZeroExitCode();
            }
            finally
            {
                scratch.DeleteDirectory();
            }
        });
}

/// <summary>
/// VerifyRelease's readiness wait. Checks package content indexes and the registration metadata
/// used by tool installation, with a backoff from 5 s to 60 s under one deadline. Records the outcome in
/// artifacts/release-&lt;version&gt;-index.status ("waiting", "indexed" or "timed-out", with the elapsed
/// seconds); release.sh reads it to tell a slow index (retry) from a broken package (patch release)
/// and refreshes the snapshot only after "indexed".
/// </summary>
static class ReleaseIndex
{
    static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(30) };

    internal sealed record Probe(string Name, Func<bool> IsAvailable);

    internal static Probe NuGet(string id, string version) => new(
        $"{id} {version} (nuget.org)",
        () =>
        {
            using var document = ReadIndex(new Uri($"https://api.nuget.org/v3-flatcontainer/{id.ToLowerInvariant()}/index.json"));
            return document.RootElement.GetProperty("versions").EnumerateArray()
                .Any(listed => string.Equals(listed.GetString(), version, StringComparison.OrdinalIgnoreCase));
        });

    internal static Probe NuGetRegistration(string id, string version)
    {
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var expected = NuGetVersion.Parse(version);
        return new Probe($"{id} {version} registration metadata (nuget.org)", () =>
        {
            // A flat-container listing can precede the registration metadata used by
            // dotnet tool install. Use the actual NuGet client for discovery, compressed
            // and paged registrations instead of maintaining another protocol decoder.
            // https://learn.microsoft.com/nuget/api/registration-base-url-resource
            using var cache = new SourceCacheContext { NoCache = true, DirectDownload = true };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var resource = repository.GetResourceAsync<PackageMetadataResource>(timeout.Token).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("NuGet did not provide a package metadata resource.");
            var packages = resource.GetMetadataAsync(id, includePrerelease: true, includeUnlisted: false,
                cache, NullLogger.Instance, timeout.Token).GetAwaiter().GetResult();
            return packages.Any(package => package.Identity.Version == expected);
        });
    }

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

            Write(statusFile, "waiting", clock);
            Thread.Sleep(delay);
            delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
        }
    }

    static JsonDocument ReadIndex(Uri source)
    {
        var url = new UriBuilder(source) { Query = $"qyl-release={Guid.NewGuid():N}" }.Uri;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using var response = s_http.Send(request);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(response.Content.ReadAsStream());
    }

    static bool Serves(Probe probe)
    {
        try
        {
            return probe.IsAvailable();
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
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
        catch (FatalProtocolException)
        {
            return false;
        }
    }

    static void Write(AbsolutePath statusFile, string state, Stopwatch clock) =>
        File.WriteAllText(statusFile,
            $"{state} elapsed={(int)clock.Elapsed.TotalSeconds}s at={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\n");
}
