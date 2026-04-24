// Copyright (c) 2025-2026 ancplua

using System.Collections.Immutable;

namespace Qyl.Loom.Workflows.Detection;

/// <summary>
///     Output of <see cref="DotnetProjectDetector.Detect" />. All recommendations are
///     derived from detected evidence; nothing is guessed. Callers must feed this into the
///     setup prompt — the prompt refuses to recommend without a detection result.
/// </summary>
public sealed record DotnetProjectEvidence
{
    /// <summary>Repo or folder root that was scanned.</summary>
    public required string RepoRoot { get; init; }

    /// <summary>All <c>*.csproj</c> paths found (repo-relative).</summary>
    public required ImmutableArray<string> ProjectFiles { get; init; }

    /// <summary>Classification of the primary project shape.</summary>
    public required DotnetFramework Framework { get; init; }

    /// <summary>Target framework moniker(s) found (e.g. <c>net8.0</c>, <c>net10.0</c>, <c>net472</c>).</summary>
    public required ImmutableArray<string> TargetFrameworks { get; init; }

    /// <summary>Sentry NuGet packages already referenced (empty if none).</summary>
    public required ImmutableArray<string> ExistingSentryPackages { get; init; }

    /// <summary>Recommended Sentry NuGet package for <see cref="Framework" />.</summary>
    public required string RecommendedPackage { get; init; }

    /// <summary>Init file the user should edit (e.g. <c>Program.cs</c>, <c>App.xaml.cs</c>, <c>MauiProgram.cs</c>).</summary>
    public required string RecommendedInitFile { get; init; }

    /// <summary>Logging libraries detected (Serilog, NLog, log4net, ILogger). Empty if none.</summary>
    public required ImmutableArray<string> LoggingLibraries { get; init; }

    /// <summary>Scheduled-job libraries detected (Hangfire, Quartz, BackgroundService).</summary>
    public required ImmutableArray<string> SchedulerLibraries { get; init; }

    /// <summary>AI SDK packages detected (OpenAI, Anthropic, Microsoft.Extensions.AI, Microsoft.Agents.AI, etc.).</summary>
    public required ImmutableArray<string> AiSdks { get; init; }

    /// <summary>Sibling frontend directories found (<c>../frontend</c>, <c>../client</c>, <c>../web</c>, <c>../app</c>).</summary>
    public required ImmutableArray<string> SiblingFrontendDirs { get; init; }

    /// <summary>True when <see cref="Framework" /> requires <c>IsGlobalModeEnabled = true</c> (WPF, WinForms, Console).</summary>
    public required bool RequiresGlobalMode { get; init; }

    /// <summary>True when <see cref="Framework" /> requires <c>FlushOnCompletedRequest = true</c> (AWS Lambda, serverless).</summary>
    public required bool RequiresFlushOnCompletedRequest { get; init; }

    /// <summary>True if <see cref="Framework" /> supports CPU profiling via <c>Sentry.Profiling</c> (.NET 8+, non-WASM, non-Framework).</summary>
    public required bool SupportsProfiling { get; init; }

    /// <summary>Per-feature recommendations.</summary>
    public required DotnetFeatureRecommendations Recommendations { get; init; }

    /// <summary>Freeform notes surfaced by the detector that do not fit a strict slot.</summary>
    public required ImmutableArray<string> Notes { get; init; }
}

/// <summary>
///     Per-feature recommendation flags. "Recommend" does not mean "apply" — the caller
///     still confirms with the user before touching code.
/// </summary>
public sealed record DotnetFeatureRecommendations
{
    /// <summary>Error monitoring: always recommended (non-negotiable baseline).</summary>
    public required bool ErrorMonitoring { get; init; }

    /// <summary>Tracing: always for ASP.NET Core and hosted apps.</summary>
    public required bool Tracing { get; init; }

    /// <summary>Logging: recommend when ILogger / Serilog / NLog / log4net detected.</summary>
    public required bool Logging { get; init; }

    /// <summary>Profiling: recommend only on .NET 8+ with a traceable workload, never on Blazor WASM / .NET Framework.</summary>
    public required bool Profiling { get; init; }

    /// <summary>Metrics: opt-in, recommend only when the app clearly needs custom business metrics.</summary>
    public required bool Metrics { get; init; }

    /// <summary>Crons: recommend when Hangfire / Quartz / BackgroundService detected.</summary>
    public required bool Crons { get; init; }
}
