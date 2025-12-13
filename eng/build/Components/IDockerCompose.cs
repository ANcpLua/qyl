using Nuke.Common;
using Nuke.Common.Tooling;
using Serilog;

namespace Components;

[ParameterPrefix(nameof(IDockerCompose))]
interface IDockerCompose : IHasSolution
{
    [Parameter("Build images before starting (--build)")] bool? DockerBuild => null;

    [Parameter("Force recreate containers (--force-recreate)")] bool? ForceRecreate => null;

    [Parameter("Remove volumes on down (-v)")] bool? RemoveVolumes => null;

    [Parameter("Number of log lines to tail")] int? LogTail => null;

    [Parameter("Target specific service")] string? Service => null;

    Target DockerUp => d => d
        .Description("Start qyl Docker Compose stack")
        .Executes(() =>
        {
            Log.Information("Starting qyl Docker Compose stack...");
            Log.Information("  Compose file: {File}", ComposeFile);

            var args = $"-f \"{ComposeFile}\" up -d --remove-orphans";
            if (DockerBuild == true) args += " --build";
            if (ForceRecreate == true) args += " --force-recreate";
            if (!string.IsNullOrEmpty(Service)) args += $" {Service}";

            RunDockerCompose(args);

            Log.Information("qyl stack started successfully");
            Log.Information("  Collector:  http://localhost:5100 (REST API + SSE)");
            Log.Information("  MCP:        http://localhost:5100/mcp (AI agent queries)");
            Log.Information("  Dashboard:  Run 'nuke frontend-dev' for Vite dev server");
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
        .DependsOn<IDockerCompose>(x => x.DockerDown)
        .DependsOn<IDockerCompose>(x => x.DockerUp)
        .Executes(() => Log.Information("qyl Docker Compose stack restarted"));

    Target DockerPull => d => d
        .Description("Pull latest Docker images")
        .Executes(() =>
        {
            Log.Information("Pulling latest images...");
            RunDockerCompose($"-f \"{ComposeFile}\" pull");
            Log.Information("Images pulled successfully");
        });

    private void RunDockerCompose(string arguments) =>
        ProcessTasks.StartProcess(
                "docker",
                $"compose {arguments}",
                SourceDirectory,
                logOutput: true)
            .AssertZeroExitCode();
}