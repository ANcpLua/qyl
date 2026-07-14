
using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Serilog;

namespace Qyl.Build;

[ParameterPrefix(nameof(IDocker))]
interface IDocker : IHazSourcePaths
{
    [PathVariable]
    Tool Docker => TryGetValue(() => Docker)
                   ?? throw new InvalidOperationException(
                       "docker not found on PATH. Install Docker Desktop or add docker to PATH.");

    [Parameter("Docker image tag (default: latest)")]
    string ImageTag => "latest";

    [Parameter("Docker registry prefix (e.g. ghcr.io/ancplua)")]
    string? Registry => TryGetValue(() => Registry);

    [Parameter("Compose service name to target (used by DockerLogs)")]
    string? Service => TryGetValue(() => Service);

    private (string Name, AbsolutePath Dockerfile, string Tag)[] ImageSpecs =>
    [
        ("qyl-collector", CollectorDirectory / "Dockerfile", FormatImageName("qyl-collector"))
    ];


    Target DockerImageBuild => d => d
        .Description("Build all qyl Docker images in parallel")
        .Executes(() =>
        {
            Log.Information("Building {Count} images in parallel", ImageSpecs.Length);

            DockerTasks.DockerBuild(s => s
                    .SetPath<DockerBuildSettings>(RootDirectory)
                    .SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1")
                    .CombineWith(ImageSpecs, static (settings, img) => settings
                        .SetFile(img.Dockerfile)
                        .SetTag(img.Tag)),
                degreeOfParallelism: 4);

            foreach (var (_, _, tag) in ImageSpecs)
                Log.Information("Built: {Tag}", tag);
        });

    Target DockerImagePush => d => d
        .Description("Push all qyl Docker images to the configured registry")
        .Requires(() => Registry)
        .DependsOn(DockerImageBuild)
        .Executes(() =>
        {
            Log.Information("Pushing {Count} images to {Registry}", ImageSpecs.Length, Registry);

            DockerTasks.DockerPush(s => s
                    .CombineWith(ImageSpecs, static (settings, img) => settings.SetName<DockerPushSettings>(img.Tag)),
                degreeOfParallelism: 2);
        });


    Target DockerUp => d => d
        .Description("Start the qyl Compose stack (docker compose up -d)")
        .Executes(() =>
        {
            Compose("up", "-d", "--remove-orphans");
            Log.Information("qyl stack started");
            Log.Information("  Dashboard:    http://localhost:5100");
            Log.Information("  OTLP HTTP:    http://localhost:4318/v1/traces");
            Log.Information("  OTLP gRPC:    http://localhost:4317");
            Log.Information("  Frontend dev: nuke FrontendDev");
        });

    Target DockerDown => d => d
        .Description("Stop the qyl Compose stack (docker compose down)")
        .Executes(() => Compose("down", "--remove-orphans"));

    Target DockerLogs => d => d
        .Description("Tail Compose logs; pass --service <name> to filter to one service")
        .Executes(() =>
        {
            if (string.IsNullOrEmpty(Service))
                Compose("logs", "-f");
            else
                Compose("logs", "-f", Service);
        });


    private void Compose(params string[] composeArgs)
    {
        var argv = new[] { "compose", "-f", ComposeFile.ToString() }.Concat(composeArgs);
        Docker(string.Join(" ", argv.Select(QuoteIfNeeded)), workingDirectory: RootDirectory);
    }

    private static string QuoteIfNeeded(string arg) =>
        arg.Length == 0 || arg.IndexOfAny([' ', '\t']) < 0
            ? arg
            : $"\"{arg.Replace("\"", "\\\"")}\"";

    private string FormatImageName(string name) =>
        string.IsNullOrEmpty(Registry)
            ? $"{name}:{ImageTag}"
            : $"{Registry}/{name}:{ImageTag}";
}
