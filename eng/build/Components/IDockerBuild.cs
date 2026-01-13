using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Serilog;
using static Nuke.Common.Tools.Docker.DockerTasks;

namespace Components;

[ParameterPrefix(nameof(IDockerBuild))]
interface IDockerBuild : IHasSolution
{
    [Parameter("Docker image tag")] string ImageTag => "latest";

    [Parameter("Docker registry prefix")] string? Registry => TryGetValue(() => Registry);

    [Parameter("Push images after build")]
    bool? Push => TryGetValue<bool?>(() => Push);

    [Parameter("Build images in parallel (default: true)")]
    bool ParallelBuild => true;

    private (string Name, AbsolutePath Dockerfile, string Tag)[] ImageSpecs =>
    [
        ("qyl-collector", CollectorDirectory / "Dockerfile", FormatImageName("qyl-collector")),
        ("qyl-dashboard", DashboardDirectory / "Dockerfile", FormatImageName("qyl-dashboard"))
    ];

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
                    .CombineWith(ImageSpecs, (settings, img) => settings
                        .SetFile(img.Dockerfile)
                        .SetTag(img.Tag)),
                ParallelBuild ? 2 : 1);

            foreach (var img in ImageSpecs) Log.Information("Built: {Tag}", img.Tag);
        });

    Target DockerBuildCollector => d => d
        .Description("Build qyl-collector Docker image")
        .Executes(() => BuildSingleImage(ImageSpecs[0]));

    Target DockerBuildDashboard => d => d
        .Description("Build qyl-dashboard Docker image")
        .Executes(() => BuildSingleImage(ImageSpecs[1]));

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

    Target DockerImagePush => d => d
        .Description("Push Docker images to registry")
        .DependsOn<IDockerBuild>(x => x.DockerImageBuild)
        .Executes(() =>
        {
            Log.Information("Pushing images to registry: {Registry}", Registry);

            DockerPush(s => s
                    .CombineWith(ImageSpecs, (settings, img) => settings.SetName(img.Tag)),
                ParallelBuild ? 2 : 1);

            Log.Information("Images pushed successfully");
        });

    private string FormatImageName(string name) =>
        string.IsNullOrEmpty(Registry)
            ? $"{name}:{ImageTag}"
            : $"{Registry}/{name}:{ImageTag}";
}