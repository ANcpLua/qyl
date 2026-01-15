// =============================================================================
// qyl Build System - Infrastructure Components
// =============================================================================
// Docker image builds, Compose orchestration
// =============================================================================



// ════════════════════════════════════════════════════════════════════════════════
// IDocker - Container Build & Orchestration
// ════════════════════════════════════════════════════════════════════════════════

[ParameterPrefix(nameof(IDocker))]
interface IDocker : IHasSolution
{
    // ─── Image Build Parameters ─────────────────────────────────────────────
    [Parameter("Docker image tag")] string ImageTag => "latest";

    [Parameter("Docker registry prefix")] string? Registry => TryGetValue(() => Registry);

    [Parameter("Push images after build")] bool? Push => TryGetValue<bool?>(() => Push);

    [Parameter("Build images in parallel (default: true)")]
    bool ParallelBuild => true;

    // ─── Compose Parameters ─────────────────────────────────────────────────
    [Parameter("Build images before starting (--build)")]
    bool? ComposeBuild => TryGetValue<bool?>(() => ComposeBuild);

    [Parameter("Force recreate containers (--force-recreate)")]
    bool? ForceRecreate => TryGetValue<bool?>(() => ForceRecreate);

    [Parameter("Remove volumes on down (-v)")]
    bool? RemoveVolumes => TryGetValue<bool?>(() => RemoveVolumes);

    [Parameter("Number of log lines to tail")]
    int? LogTail => TryGetValue<int?>(() => LogTail);

    [Parameter("Target specific service")] string? Service => TryGetValue(() => Service);

    // ─── Image Specs ────────────────────────────────────────────────────────
    private (string Name, AbsolutePath Dockerfile, string Tag)[] ImageSpecs =>
    [
        ("qyl-collector", CollectorDirectory / "Dockerfile", FormatImageName("qyl-collector")),
        ("qyl-dashboard", DashboardDirectory / "Dockerfile", FormatImageName("qyl-dashboard"))
    ];

    // ════════════════════════════════════════════════════════════════════════
    // Image Build Targets
    // ════════════════════════════════════════════════════════════════════════

    Target DockerImageBuild => d => d
        .Description("Build all qyl Docker images")
        .Executes(() =>
        {
            Log.Information("Building {Count} Docker images{Parallel}...",
                ImageSpecs.Length,
                ParallelBuild ? " in parallel" : "");

            DockerBuild(s => s
                    .SetPath(RootDirectory)
                    .EnablePull()
                    .SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1")
                    .CombineWith(ImageSpecs, static (settings, img) => settings
                        .SetFile(img.Dockerfile)
                        .SetTag(img.Tag)),
                ParallelBuild ? 2 : 1);

            foreach (var (_, _, tag) in ImageSpecs)
                Log.Information("Built: {Tag}", tag);
        });

    Target DockerBuildCollector => d => d
        .Description("Build qyl-collector Docker image")
        .Executes(() => BuildSingleImage(ImageSpecs[0]));

    Target DockerBuildDashboard => d => d
        .Description("Build qyl-dashboard Docker image")
        .Executes(() => BuildSingleImage(ImageSpecs[1]));

    Target DockerImagePush => d => d
        .Description("Push Docker images to registry")
        .DependsOn(DockerImageBuild)
        .Executes(() =>
        {
            Log.Information("Pushing images to registry: {Registry}", Registry);

            DockerPush(s => s
                    .CombineWith(ImageSpecs, static (settings, img) => settings.SetName(img.Tag)),
                ParallelBuild ? 2 : 1);

            Log.Information("Images pushed successfully");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Compose Targets
    // ════════════════════════════════════════════════════════════════════════

    Target DockerUp => d => d
        .Description("Start qyl Docker Compose stack")
        .Executes(() =>
        {
            Log.Information("Starting qyl Docker Compose stack...");
            Log.Information("  Compose file: {File}", ComposeFile);

            var args = $"-f \"{ComposeFile}\" up -d --remove-orphans";
            if (ComposeBuild == true) args += " --build";
            if (ForceRecreate == true) args += " --force-recreate";
            if (!string.IsNullOrEmpty(Service)) args += $" {Service}";

            RunDockerCompose(args);

            Log.Information("qyl stack started successfully");
            Log.Information("  Collector:  http://localhost:5100 (REST API + SSE)");
            Log.Information("  MCP:        http://localhost:5100/mcp (AI agent queries)");
            Log.Information("  Dashboard:  Run 'nuke FrontendDev' for Vite dev server");
        });

    Target DockerDown => d => d
        .Description("Stop qyl Docker Compose stack")
        .Executes(() =>
        {
            Log.Information("Stopping qyl Docker Compose stack...");

            var args = $"-f \"{ComposeFile}\" down --remove-orphans";
            if (RemoveVolumes == true) args += " -v";

            RunDockerCompose(args);

            Log.Information("qyl stack stopped");
        });

    Target DockerStatus => d => d
        .Description("Show Docker Compose container status")
        .Executes(() => RunDockerCompose($"-f \"{ComposeFile}\" ps"));

    Target DockerLogs => d => d
        .Description("Tail Docker Compose logs")
        .Executes(() =>
        {
            var tail = LogTail ?? 100;
            var args = $"-f \"{ComposeFile}\" logs -f --tail {tail}";
            if (!string.IsNullOrEmpty(Service)) args += $" {Service}";

            RunDockerCompose(args);
        });

    Target DockerRestart => d => d
        .Description("Restart qyl Docker Compose stack (down + up)")
        .DependsOn(DockerDown)
        .DependsOn(DockerUp)
        .Executes(static () => Log.Information("qyl Docker Compose stack restarted"));

    Target DockerPull => d => d
        .Description("Pull latest Docker images")
        .Executes(() =>
        {
            Log.Information("Pulling latest images...");
            RunDockerCompose($"-f \"{ComposeFile}\" pull");
            Log.Information("Images pulled successfully");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Private Helpers
    // ════════════════════════════════════════════════════════════════════════

    private void BuildSingleImage((string Name, AbsolutePath Dockerfile, string Tag) spec)
    {
        Log.Information("Building image: {Name} → {Tag}", spec.Name, spec.Tag);

        DockerBuild(s => s
            .SetPath(RootDirectory)
            .SetFile(spec.Dockerfile)
            .SetTag(spec.Tag)
            .EnablePull()
            .SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1"));
    }

    private string FormatImageName(string name) =>
        string.IsNullOrEmpty(Registry)
            ? $"{name}:{ImageTag}"
            : $"{Registry}/{name}:{ImageTag}";

    private void RunDockerCompose(string arguments) =>
        ProcessTasks.StartProcess(
                "docker",
                $"compose {arguments}",
                SourceDirectory,
                logOutput: true)
            .AssertZeroExitCode();
}
