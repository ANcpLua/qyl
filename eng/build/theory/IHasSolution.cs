using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;

namespace Components.Theory;

/// <summary>
/// Core solution component defining the polyrepo structure for qyl.
/// Provides path abstractions for all domains: core, collector, dashboard, sdk, agents, ops.
///
/// NUKE 10.1.0 patterns:
/// - Interface default members for path definitions
/// - TryGetValue() for parameter access
/// - Explicit interface casting for member access
/// </summary>
internal interface IHasSolution : INukeBuild
{
    [Solution(GenerateProjects = true)]
    Solution Solution => TryGetValue(() => Solution)!;

    // ════════════════════════════════════════════════════════════════════════
    // Build Artifacts (Cross-Domain)
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";

    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";

    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";

    AbsolutePath EnvFile => RootDirectory / ".env";

    // ════════════════════════════════════════════════════════════════════════
    // Core (Schema - Single Source of Truth)
    // Publishes: npm @qyl/core (weekly)
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath CoreDirectory => RootDirectory / "core";

    AbsolutePath SpecsDirectory => CoreDirectory / "specs";

    AbsolutePath PrimitivesDirectory => CoreDirectory / "primitives";

    AbsolutePath PrimitivesDotNetDirectory => PrimitivesDirectory / "dotnet";

    AbsolutePath PrimitivesPythonDirectory => PrimitivesDirectory / "python";

    AbsolutePath PrimitivesTypeScriptDirectory => PrimitivesDirectory / "typescript";

    AbsolutePath OpenApiDirectory => CoreDirectory / "openapi";

    AbsolutePath CoreTestsDirectory => CoreDirectory / "tests";

    AbsolutePath CoreDocsDirectory => CoreDirectory / "docs";

    // ════════════════════════════════════════════════════════════════════════
    // Collector (Backend - Telemetry Collector)
    // Publishes: Docker ghcr.io/qyl/collector
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath CollectorDirectory => RootDirectory / "collector";

    AbsolutePath CollectorSrcDirectory => CollectorDirectory / "src";

    AbsolutePath CollectorDockerDirectory => CollectorDirectory / "docker";

    AbsolutePath CollectorTestsDirectory => CollectorDirectory / "tests";

    AbsolutePath CollectorDocsDirectory => CollectorDirectory / "docs";

    // ════════════════════════════════════════════════════════════════════════
    // Dashboard (Frontend - React SPA)
    // Publishes: Docker ghcr.io/qyl/dashboard
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath DashboardDirectory => RootDirectory / "dashboard";

    AbsolutePath DashboardSrcDirectory => DashboardDirectory / "src";

    AbsolutePath DashboardDockerDirectory => DashboardDirectory / "docker";

    AbsolutePath DashboardTestsDirectory => DashboardDirectory / "tests";

    AbsolutePath DashboardDocsDirectory => DashboardDirectory / "docs";

    // ════════════════════════════════════════════════════════════════════════
    // SDK (User-Facing SDKs)
    // Publishes: NuGet Qyl.Sdk, PyPI qyl-sdk, npm @qyl/sdk
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath SdkDirectory => RootDirectory / "sdk";

    AbsolutePath SdkDotNetDirectory => SdkDirectory / "dotnet";

    AbsolutePath SdkDotNetCoreDirectory => SdkDotNetDirectory / "Qyl.Sdk";

    AbsolutePath SdkDotNetAspNetCoreDirectory => SdkDotNetDirectory / "Qyl.Sdk.AspNetCore";

    AbsolutePath SdkPythonDirectory => SdkDirectory / "python";

    AbsolutePath SdkTypeScriptDirectory => SdkDirectory / "typescript";

    AbsolutePath SdkTestsDirectory => SdkDirectory / "tests";

    // ════════════════════════════════════════════════════════════════════════
    // Agents (Orchestration - Container Management)
    // Publishes: qyl CLI binary
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath AgentsDirectory => RootDirectory / "agents";

    AbsolutePath AgentsCliDirectory => AgentsDirectory / "cli";

    AbsolutePath AgentsInstrumentationDirectory => AgentsDirectory / "instrumentation";

    AbsolutePath AgentsInstrumentationDotNetDirectory => AgentsInstrumentationDirectory / "dotnet";

    AbsolutePath AgentsInstrumentationPythonDirectory => AgentsInstrumentationDirectory / "python";

    AbsolutePath AgentsInstrumentationTypeScriptDirectory => AgentsInstrumentationDirectory / "typescript";

    AbsolutePath AgentsDockerDirectory => AgentsDirectory / "docker";

    AbsolutePath AgentsTestsDirectory => AgentsDirectory / "tests";

    AbsolutePath AgentsDocsDirectory => AgentsDirectory / "docs";

    // ════════════════════════════════════════════════════════════════════════
    // Operations (Cross-Domain Deploys)
    // Publishes: Docker Compose stacks
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath OpsDirectory => RootDirectory / "ops";

    AbsolutePath ComposeDirectory => OpsDirectory / "compose";

    AbsolutePath FullStackComposeFile => ComposeDirectory / "full-stack.yaml";

    AbsolutePath DevComposeFile => ComposeDirectory / "dev.yaml";

    AbsolutePath CiComposeFile => ComposeDirectory / "ci.yaml";

    AbsolutePath MonitoringDirectory => OpsDirectory / "monitoring";

    AbsolutePath ScriptsDirectory => OpsDirectory / "scripts";

    AbsolutePath OpsDocsDirectory => OpsDirectory / "docs";

    // ════════════════════════════════════════════════════════════════════════
    // Engineering (Build Infrastructure)
    // NOT published - internal tooling
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath EngDirectory => RootDirectory / "eng";

    AbsolutePath EngSdkDirectory => EngDirectory / "sdk";

    AbsolutePath EngAnalyzersDirectory => EngDirectory / "analyzers";

    AbsolutePath EngBuildDirectory => EngDirectory / "build";

    AbsolutePath EngConfigDirectory => EngDirectory / "config";

    AbsolutePath EngConfigDotNetDirectory => EngConfigDirectory / "dotnet";

    AbsolutePath EngConfigPythonDirectory => EngConfigDirectory / "python";

    AbsolutePath EngConfigTypeScriptDirectory => EngConfigDirectory / "typescript";

    AbsolutePath EngCiDirectory => EngDirectory / "ci";

    // ════════════════════════════════════════════════════════════════════════
    // Tests (Shared Test Infrastructure)
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath TestsDirectory => RootDirectory / "tests";

    AbsolutePath IntegrationTestsDirectory => TestsDirectory / "integration";

    AbsolutePath E2ETestsDirectory => TestsDirectory / "e2e";

    // ════════════════════════════════════════════════════════════════════════
    // Examples (E2E Test Apps - Simulate Real Users)
    // NOT published - internal testing only
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath ExamplesDirectory => RootDirectory / "examples";

    AbsolutePath ExampleDotNetAspNetCoreDirectory => ExamplesDirectory / "dotnet-aspnetcore";

    AbsolutePath ExamplePythonFastApiDirectory => ExamplesDirectory / "python-fastapi";

    AbsolutePath ExampleTypeScriptExpressDirectory => ExamplesDirectory / "typescript-express";

    AbsolutePath ExampleSemanticKernelDirectory => ExamplesDirectory / "semantic-kernel";

    // ════════════════════════════════════════════════════════════════════════
    // Utilities
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath GetSolutionPath() =>
        Solution.Path ?? RootDirectory.GlobFiles("*.sln", "*.slnx").FirstOrDefault()
        ?? throw new InvalidOperationException("Unable to locate solution file");

    /// <summary>
    /// Get all polyglot directories (dotnet, python, typescript) for a given parent.
    /// Useful for parallel operations across language targets.
    /// </summary>
    AbsolutePath[] GetPolyglotDirectories(AbsolutePath parent) =>
    [
        parent / "dotnet",
        parent / "python",
        parent / "typescript"
    ];

    /// <summary>
    /// Check if any polyglot target exists for the given parent directory.
    /// </summary>
    bool HasPolyglotTargets(AbsolutePath parent) =>
        Array.Exists(GetPolyglotDirectories(parent), d => d.DirectoryExists());
}
