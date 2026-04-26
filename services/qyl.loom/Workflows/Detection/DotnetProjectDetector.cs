// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Qyl.Loom.Workflows.Detection;

/// <summary>
///     Scans a .NET repo root and classifies the app shape per the framework → package
///     mapping the Loom setup workflow implements. Pure filesystem reads — no MSBuild,
///     no network, no LLM. Output is structured evidence; recommendations flow from
///     evidence only.
/// </summary>
/// <remarks>
///     <para>
///         Detection-first is a non-negotiable invariant of the setup workflow. Callers
///         must feed the <see cref="DotnetProjectEvidence" /> into the setup prompt; the prompt
///         refuses to recommend without evidence.
///     </para>
///     <para>
///         Static + file-IO-only so it can be unit-tested against a temp directory
///         without any DI or hosting scaffolding.
///     </para>
/// </remarks>
public static partial class DotnetProjectDetector
{
    private const int MaxProjectScan = 50;
    private const int MaxProjectFileBytes = 256 * 1024;

    private const long
        MaxBackgroundServiceScanFileBytes =
            1024 * 1024; // 1 MiB — any .cs file bigger is not a BackgroundService declaration.

    [GeneratedRegex(@"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", RegexOptions.IgnoreCase, 250)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""", RegexOptions.IgnoreCase, 250)]
    private static partial Regex PackageReferenceRegex();

    /// <summary>
    ///     Classify <paramref name="repoRoot" />. Returns a <see cref="DotnetProjectEvidence" />
    ///     with <see cref="DotnetFramework.Unknown" /> if no <c>*.csproj</c> was found; never throws.
    /// </summary>
    /// <remarks>
    ///     Path hardening: <paramref name="repoRoot" /> is normalised via
    ///     <see cref="Path.GetFullPath(string)" /> before any IO, so caller-supplied
    ///     <c>..</c> segments collapse into an absolute path. The detector never reads
    ///     outside the normalised root except for <see cref="DetectSiblingFrontends" />
    ///     which deliberately walks one directory up (scoped to a name allowlist).
    ///     Reparse points (symlinks, junctions) are skipped so a hostile repo cannot
    ///     escape via a link loop.
    /// </remarks>
    public static DotnetProjectEvidence Detect(string repoRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        var normalised = Path.GetFullPath(repoRoot);
        if (!Directory.Exists(normalised))
            return Empty(normalised, ["Repo root does not exist."]);

        if (IsReparsePoint(normalised))
            return Empty(normalised, ["Repo root is a symlink / junction — refusing to scan to avoid loops."]);

        repoRoot = normalised;

        var projectFiles = FindProjectFiles(repoRoot);
        if (projectFiles.Length is 0)
            return Empty(repoRoot, ["No *.csproj files found under repo root."]);

        var inspected = InspectProjects(repoRoot, projectFiles);
        var primary = ChoosePrimary(inspected);

        var framework = ClassifyFramework(primary);
        var supportsProfiling = SupportsProfiling(primary.TargetFrameworks, framework);
        var requiresGlobalMode = framework is DotnetFramework.Wpf
            or DotnetFramework.WinForms
            or DotnetFramework.ConsoleOrWorker;
        var requiresFlush = framework is DotnetFramework.AwsLambda
            or DotnetFramework.AzureFunctions;

        var loggingLibs = DetectLoggingLibraries(inspected);
        var schedulers = DetectSchedulers(inspected, repoRoot);
        var aiSdks = DetectAiSdks(inspected);
        var frontends = DetectSiblingFrontends(repoRoot);

        var existingSentry = inspected
            .SelectMany(static p => p.PackageReferences)
            .Where(static pkg => pkg.StartsWithIgnoreCase("Sentry"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static s => s, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        var recommended = RecommendFeatures(framework, loggingLibs, schedulers, supportsProfiling);

        return new DotnetProjectEvidence
        {
            RepoRoot = repoRoot,
            ProjectFiles = projectFiles,
            Framework = framework,
            TargetFrameworks = primary.TargetFrameworks,
            ExistingSentryPackages = existingSentry,
            RecommendedPackage = RecommendPackage(framework),
            RecommendedInitFile = RecommendInitFile(framework, primary),
            LoggingLibraries = loggingLibs,
            SchedulerLibraries = schedulers,
            AiSdks = aiSdks,
            SiblingFrontendDirs = frontends,
            RequiresGlobalMode = requiresGlobalMode,
            RequiresFlushOnCompletedRequest = requiresFlush,
            SupportsProfiling = supportsProfiling,
            Recommendations = recommended,
            Notes = BuildNotes(framework, primary, existingSentry)
        };
    }

    private static ImmutableArray<string> FindProjectFiles(string repoRoot)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        try
        {
            foreach (var path in Directory.EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories))
            {
                if (path.ContainsOrdinal($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                    path.ContainsOrdinal($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                builder.Add(Path.GetRelativePath(repoRoot, path));
                if (builder.Count >= MaxProjectScan) break;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Unreadable subtree during enumeration — return whatever was collected.
        }
        catch (DirectoryNotFoundException)
        {
            // repoRoot disappeared mid-scan (e.g. CI cleanup) — return partial results.
        }

        builder.Sort(StringComparer.OrdinalIgnoreCase);
        return builder.ToImmutable();
    }

    private static ImmutableArray<InspectedProject> InspectProjects(string repoRoot,
        ImmutableArray<string> projectFiles)
    {
        var builder = ImmutableArray.CreateBuilder<InspectedProject>(projectFiles.Length);
        foreach (var relative in projectFiles)
            builder.Add(InspectProject(repoRoot, relative, Path.Combine(repoRoot, relative)));
        return builder.ToImmutable();
    }

    private static InspectedProject InspectProject(string repoRoot, string relative, string absolute)
    {
        string xml;
        try
        {
            var info = new FileInfo(absolute);
            if (info.Length > MaxProjectFileBytes)
                return new InspectedProject(relative, "", "", [], []);

            xml = File.ReadAllText(absolute);
        }
        catch (IOException) { return new InspectedProject(relative, "", "", [], []); }
        catch (UnauthorizedAccessException) { return new InspectedProject(relative, "", "", [], []); }

        var tfmMatch = TargetFrameworkRegex().Match(xml);
        var tfms = tfmMatch.Success
            ? tfmMatch.Groups[1].Value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToImmutableArray()
            : ImmutableArray<string>.Empty;

        var sdk = ExtractAttribute(xml, "Sdk");
        var outputType = ExtractXmlValue(xml, "OutputType");

        var packageBuilder = ImmutableArray.CreateBuilder<string>();
        foreach (Match m in PackageReferenceRegex().Matches(xml))
            packageBuilder.Add(m.Groups[1].Value);

        var startupFiles = DiscoverStartupFiles(Path.GetDirectoryName(absolute) ?? repoRoot);

        return new InspectedProject(
            relative,
            sdk,
            outputType,
            tfms,
            packageBuilder.ToImmutable(),
            startupFiles);
    }

    private static ImmutableArray<string> DiscoverStartupFiles(string projectDir)
    {
        if (!Directory.Exists(projectDir)) return [];

        var candidates = new[]
        {
            "Program.cs", "App.xaml.cs", "MauiProgram.cs", "Startup.cs", "Global.asax.cs", "LambdaEntryPoint.cs"
        };

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var name in candidates)
        {
            var path = Path.Combine(projectDir, name);
            if (File.Exists(path)) builder.Add(name);
        }

        return builder.ToImmutable();
    }

    private static InspectedProject ChoosePrimary(ImmutableArray<InspectedProject> inspected) =>
        inspected.FirstOrDefault(static p =>
            p.Sdk.ContainsIgnoreCase("Microsoft.NET.Sdk.Web")) ??
        inspected.FirstOrDefault(static p => p.StartupFiles.Contains("MauiProgram.cs")) ??
        inspected.FirstOrDefault(static p => p.StartupFiles.Contains("App.xaml.cs")) ??
        inspected.FirstOrDefault(static p => p.StartupFiles.Contains("Program.cs")) ??
        inspected[0];

    private static DotnetFramework ClassifyFramework(InspectedProject primary)
    {
        var sdk = primary.Sdk;
        var packages = primary.PackageReferences;
        var startups = primary.StartupFiles;

        if (sdk.ContainsIgnoreCase("Microsoft.NET.Sdk.Web") ||
            packages.Any(static p => p.StartsWithIgnoreCase("Microsoft.AspNetCore")))
        {
            if (packages.Any(static p => p.StartsWithIgnoreCase("Amazon.Lambda")))
                return DotnetFramework.AwsLambda;

            if (packages.Any(static p => p.ContainsIgnoreCase("Microsoft.Azure.Functions.Worker")))
                return DotnetFramework.AzureFunctions;

            if (packages.Any(static p => p.EqualsIgnoreCase("Microsoft.AspNetCore.Components.WebAssembly")))
                return DotnetFramework.BlazorWasm;

            return DotnetFramework.AspNetCore;
        }

        if (startups.Contains("MauiProgram.cs") ||
            packages.Any(static p => p.StartsWithIgnoreCase("Microsoft.Maui")))
            return DotnetFramework.Maui;

        if (startups.Contains("App.xaml.cs") ||
            primary.OutputType.EqualsIgnoreCase("WinExe"))
        {
            var isWpf = packages.Any(static p =>
                p.StartsWithIgnoreCase("Microsoft.Toolkit.Mvvm") ||
                p.EqualsIgnoreCase("CommunityToolkit.Mvvm"));
            return isWpf || startups.Contains("App.xaml.cs")
                ? DotnetFramework.Wpf
                : DotnetFramework.WinForms;
        }

        if (startups.Contains("Global.asax.cs") ||
            packages.Any(static p => p.EqualsIgnoreCase("Microsoft.AspNet.Mvc")))
            return DotnetFramework.ClassicAspNet;

        if (packages.Any(static p => p.EqualsIgnoreCase("Microsoft.Azure.Functions.Worker")))
            return DotnetFramework.AzureFunctions;

        if (packages.Any(static p => p.StartsWithIgnoreCase("Amazon.Lambda")))
            return DotnetFramework.AwsLambda;

        if (primary.OutputType.EqualsIgnoreCase("Exe") ||
            packages.Any(static p => p.EqualsIgnoreCase("Microsoft.Extensions.Hosting")))
            return DotnetFramework.ConsoleOrWorker;

        return DotnetFramework.Unknown;
    }

    private static string RecommendPackage(DotnetFramework framework) => framework switch
    {
        DotnetFramework.AspNetCore => "Sentry.AspNetCore",
        DotnetFramework.Wpf => "Sentry",
        DotnetFramework.WinForms => "Sentry",
        DotnetFramework.Maui => "Sentry.Maui",
        DotnetFramework.BlazorWasm => "Sentry.AspNetCore.Blazor.WebAssembly",
        DotnetFramework.AzureFunctions => "Sentry.Extensions.Logging (+ Sentry.OpenTelemetry)",
        DotnetFramework.AwsLambda => "Sentry.AspNetCore",
        DotnetFramework.ClassicAspNet => "Sentry.AspNet",
        DotnetFramework.ConsoleOrWorker => "Sentry.Extensions.Logging",
        _ => "Sentry"
    };

    private static string RecommendInitFile(DotnetFramework framework, InspectedProject primary)
    {
        var initName = framework switch
        {
            DotnetFramework.AspNetCore or DotnetFramework.BlazorWasm
                or DotnetFramework.ConsoleOrWorker or DotnetFramework.AzureFunctions
                or DotnetFramework.WinForms or DotnetFramework.AwsLambda => "Program.cs",
            DotnetFramework.Wpf => "App.xaml.cs",
            DotnetFramework.Maui => "MauiProgram.cs",
            DotnetFramework.ClassicAspNet => "Global.asax.cs",
            _ => "Program.cs"
        };

        var projectDir = Path.GetDirectoryName(primary.RelativePath) ?? "";
        var candidate = Path.Combine(projectDir, initName);
        return primary.StartupFiles.Contains(initName)
            ? candidate.Replace('\\', '/')
            : $"{candidate} (not present — create it)".Replace('\\', '/');
    }

    private static bool SupportsProfiling(ImmutableArray<string> tfms, DotnetFramework framework)
    {
        if (framework is DotnetFramework.BlazorWasm or DotnetFramework.ClassicAspNet)
            return false;

        foreach (var tfm in tfms)
        {
            if (tfm.StartsWithIgnoreCase("net") && tfm.Length >= 4)
            {
                var digits = tfm.AsSpan(3);
                var dot = digits.IndexOf('.');
                if (dot < 0) continue;
                var major = digits[..dot];
                if (int.TryParse(major, out var n) && n >= 8) return true;
            }
        }

        return false;
    }

    private static ImmutableArray<string> DetectLoggingLibraries(ImmutableArray<InspectedProject> inspected)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in inspected)
        {
            foreach (var pkg in p.PackageReferences)
            {
                var hit = pkg switch
                {
                    _ when pkg.StartsWithIgnoreCase("Serilog") => "Serilog",
                    _ when pkg.StartsWithIgnoreCase("NLog") => "NLog",
                    _ when pkg.StartsWithIgnoreCase("log4net") => "log4net",
                    _ when pkg.EqualsIgnoreCase("Microsoft.Extensions.Logging")
                           || pkg.StartsWithIgnoreCase("Microsoft.Extensions.Logging.") => "ILogger",
                    _ => null
                };
                if (hit is not null && seen.Add(hit)) builder.Add(hit);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> DetectSchedulers(ImmutableArray<InspectedProject> inspected, string repoRoot)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in inspected)
        {
            foreach (var pkg in p.PackageReferences)
            {
                var hit = pkg switch
                {
                    _ when pkg.StartsWithIgnoreCase("Hangfire") => "Hangfire",
                    _ when pkg.StartsWithIgnoreCase("Quartz") => "Quartz.NET",
                    _ => null
                };
                if (hit is not null && seen.Add(hit)) builder.Add(hit);
            }
        }

        if (!seen.Contains("BackgroundService"))
        {
            foreach (var p in inspected)
            {
                var projectDir = Path.Combine(repoRoot, Path.GetDirectoryName(p.RelativePath) ?? "");
                if (HasBackgroundServiceFile(projectDir))
                {
                    builder.Add("BackgroundService");
                    break;
                }
            }
        }

        return builder.ToImmutable();
    }

    private static bool HasBackgroundServiceFile(string projectDir)
    {
        if (!Directory.Exists(projectDir)) return false;

        try
        {
            foreach (var path in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories).Take(200))
            {
                if (path.ContainsOrdinal($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    path.ContainsOrdinal($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                FileInfo info;
                try { info = new FileInfo(path); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }

                if (info.Length > MaxBackgroundServiceScanFileBytes) continue;
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;

                string text;
                try { text = File.ReadAllText(path); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }

                if (text.ContainsOrdinal(": BackgroundService") ||
                    text.ContainsOrdinal("IHostedService"))
                {
                    return true;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Unreadable subtree — assume no BackgroundService/IHostedService; best-effort detector.
        }
        catch (DirectoryNotFoundException)
        {
            // Directory vanished mid-scan — treat as "no hosted service detected".
        }

        return false;
    }

    private static ImmutableArray<string> DetectAiSdks(ImmutableArray<InspectedProject> inspected)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in inspected)
        {
            foreach (var pkg in p.PackageReferences)
            {
                var hit = pkg switch
                {
                    "OpenAI" => "OpenAI",
                    "Azure.AI.OpenAI" => "Azure.AI.OpenAI",
                    _ when pkg.StartsWithIgnoreCase("Anthropic.") => "Anthropic",
                    "Microsoft.Extensions.AI" => "Microsoft.Extensions.AI",
                    _ when pkg.StartsWithIgnoreCase("Microsoft.Extensions.AI.") => "Microsoft.Extensions.AI",
                    _ when pkg.StartsWithIgnoreCase("Microsoft.Agents.AI") => "Microsoft.Agents.AI",
                    _ when pkg.StartsWithIgnoreCase("Microsoft.SemanticKernel") => "SemanticKernel",
                    "OllamaSharp" => "Ollama",
                    "Mscc.GenerativeAI.Microsoft" => "Google GenAI",
                    _ => null
                };
                if (hit is not null && seen.Add(hit)) builder.Add(hit);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> DetectSiblingFrontends(string repoRoot)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var parent = Directory.GetParent(repoRoot)?.FullName;
        if (parent is null) return [];

        foreach (var name in new[] { "frontend", "client", "web", "app", "ui" })
        {
            var sibling = Path.Combine(parent, name);
            if (Directory.Exists(sibling) &&
                File.Exists(Path.Combine(sibling, "package.json")))
            {
                builder.Add(Path.GetRelativePath(repoRoot, sibling));
            }
        }

        return builder.ToImmutable();
    }

    private static DotnetFeatureRecommendations RecommendFeatures(
        DotnetFramework framework,
        ImmutableArray<string> logging,
        ImmutableArray<string> schedulers,
        bool supportsProfiling) =>
        new()
        {
            ErrorMonitoring = true,
            Tracing = framework is DotnetFramework.AspNetCore
                or DotnetFramework.AzureFunctions
                or DotnetFramework.AwsLambda
                or DotnetFramework.BlazorWasm
                or DotnetFramework.ClassicAspNet
                or DotnetFramework.ConsoleOrWorker,
            Logging = logging.Length > 0,
            Profiling = supportsProfiling &&
                        framework is not DotnetFramework.Maui
                            and not DotnetFramework.BlazorWasm,
            Metrics = false,
            Crons = schedulers.Length > 0
        };

    private static ImmutableArray<string> BuildNotes(
        DotnetFramework framework,
        InspectedProject primary,
        ImmutableArray<string> existingSentry)
    {
        var notes = ImmutableArray.CreateBuilder<string>();
        if (existingSentry.Length > 0)
        {
            notes.Add($"Existing Sentry packages detected: {string.Join(", ", existingSentry)}. " +
                      "Skip install — proceed to feature config.");
        }

        if (framework is DotnetFramework.Wpf)
        {
            notes.Add("WPF: initialise SentrySdk in the App() constructor, NOT OnStartup(). " +
                      "Hook DispatcherUnhandledException alongside the init call.");
        }

        if (framework is DotnetFramework.WinForms)
        {
            notes.Add("WinForms: call Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException) " +
                      "BEFORE SentrySdk.Init so unhandled exceptions surface.");
        }

        if (framework is DotnetFramework.AwsLambda or DotnetFramework.AzureFunctions)
        {
            notes.Add("Serverless: set options.FlushOnCompletedRequest = true so events ship before the " +
                      "container freezes. Omit and events are silently lost.");
        }

        if (framework is DotnetFramework.ConsoleOrWorker)
        {
            notes.Add("Console/Worker: set options.IsGlobalModeEnabled = true. Dispose the SentrySdk.Init " +
                      "result on exit to flush pending events (SDK 3.31.0+ handles this automatically).");
        }

        if (framework is DotnetFramework.BlazorWasm)
        {
            notes.Add("Blazor WASM: profiling is NOT supported. Use Sentry.AspNetCore.Blazor.WebAssembly " +
                      "and call builder.Logging.AddSentry(o => o.InitializeSdk = false).");
        }

        if (primary.TargetFrameworks.Any(static t =>
                t.StartsWithIgnoreCase("netstandard") ||
                t.StartsWithIgnoreCase("net4")))
        {
            notes.Add("Target framework is older than .NET 8 — profiling (Sentry.Profiling) is unavailable. " +
                      "Error monitoring, tracing, logging, metrics, and crons still work.");
        }

        return notes.ToImmutable();
    }

    private static DotnetProjectEvidence Empty(string repoRoot, ImmutableArray<string> notes) =>
        new()
        {
            RepoRoot = repoRoot,
            ProjectFiles = [],
            Framework = DotnetFramework.Unknown,
            TargetFrameworks = [],
            ExistingSentryPackages = [],
            RecommendedPackage = "Sentry",
            RecommendedInitFile = "Program.cs (to be created)",
            LoggingLibraries = [],
            SchedulerLibraries = [],
            AiSdks = [],
            SiblingFrontendDirs = [],
            RequiresGlobalMode = false,
            RequiresFlushOnCompletedRequest = false,
            SupportsProfiling = false,
            Recommendations = new DotnetFeatureRecommendations
            {
                ErrorMonitoring = false,
                Tracing = false,
                Logging = false,
                Profiling = false,
                Metrics = false,
                Crons = false
            },
            Notes = notes
        };

    private static string ExtractAttribute(string xml, string attribute)
    {
        var needle = $"{attribute}=\"";
        var idx = xml.IndexOfIgnoreCase(needle);
        if (idx < 0) return "";

        var start = idx + needle.Length;
        var end = xml.IndexOf('"', start);
        return end > start ? xml.AsSpan(start, end - start).ToString() : "";
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static string ExtractXmlValue(string xml, string tagName)
    {
        var open = $"<{tagName}>";
        var close = $"</{tagName}>";
        var i = xml.IndexOfIgnoreCase(open);
        if (i < 0) return "";
        var start = i + open.Length;
        var end = xml.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        return end > start ? xml.AsSpan(start, end - start).Trim().ToString() : "";
    }

    private sealed record InspectedProject(
        string RelativePath,
        string Sdk,
        string OutputType,
        ImmutableArray<string> TargetFrameworks,
        ImmutableArray<string> PackageReferences,
        ImmutableArray<string> StartupFiles = default)
    {
        public ImmutableArray<string> StartupFiles { get; } =
            StartupFiles.IsDefault ? ImmutableArray<string>.Empty : StartupFiles;
    }
}
