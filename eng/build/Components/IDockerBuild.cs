using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Serilog;
using static Nuke.Common.Tools.Docker.DockerTasks;

namespace Components;

/// <summary>
///     Docker image build component.
///     Refactored to use NUKE's native DockerTasks with CombineWith for parallel builds.
/// </summary>
/// <remarks>
///     Key improvements:
///     - Fluent DockerBuild API with type-safe settings
///     - CombineWith enables parallel image builds
///     - Automatic BuildKit environment handling
///     - Better error context from NUKE's process handling
/// </remarks>
[ParameterPrefix(nameof(IDockerBuild))]
internal interface IDockerBuild : IHasSolution
{
	/// <summary>Docker image tag.</summary>
	[Parameter("Docker image tag")]
	string ImageTag => "latest";

	/// <summary>Docker registry prefix (e.g., ghcr.io/username).</summary>
	[Parameter("Docker registry prefix")]
	string? Registry => null;

	/// <summary>Push images after building.</summary>
	[Parameter("Push images after build")]
	bool? Push => null;

	/// <summary>Build images in parallel.</summary>
	[Parameter("Build images in parallel (default: true)")]
	bool ParallelBuild => true;

	// ══════════════════════════════════════════════════════════════════════════
	// IMAGE CONFIGURATION
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Image build specifications for this project.</summary>
	private (string Name, AbsolutePath Dockerfile, string Tag)[] ImageSpecs =>
	[
		("dashboard-receiver", ReceiverDirectory / "Dockerfile", FormatImageName("dashboard-receiver")),
		("dashboard-web", WebUiDirectory / "Dockerfile", FormatImageName("dashboard-web"))
	];

	private string FormatImageName(string name) =>
		string.IsNullOrEmpty(Registry)
			? $"{name}:{ImageTag}"
			: $"{Registry}/{name}:{ImageTag}";

	// ══════════════════════════════════════════════════════════════════════════
	// TARGETS
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Build all application Docker images (parallel by default).</summary>
	Target DockerImageBuild => d => d
		.Description("Build all application Docker images")
		.Executes(() =>
		{
			Log.Information("Building {Count} Docker images{Parallel}...",
				ImageSpecs.Length,
				ParallelBuild ? " in parallel" : "");

			// NUKE's CombineWith creates multiple invocations from a single fluent chain
			DockerBuild(s => s
					.SetPath(RootDirectory)
					.EnablePull()
					.SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1")
					.CombineWith(ImageSpecs, (settings, img) => settings
						.SetFile(img.Dockerfile)
						.SetTag(img.Tag)),
				degreeOfParallelism: ParallelBuild ? 2 : 1);

			foreach (var img in ImageSpecs)
			{
				Log.Information("Built: {Tag}", img.Tag);
			}
		});

	/// <summary>Build only the dashboard.Receiver image.</summary>
	Target DockerBuildReceiver => d => d
		.Description("Build dashboard-receiver Docker image")
		.Executes(() =>
		{
			var spec = ImageSpecs[0];
			Log.Information("Building image: {Name} → {Tag}", spec.Name, spec.Tag);

			DockerBuild(s => s
				.SetPath(RootDirectory)
				.SetFile(spec.Dockerfile)
				.SetTag(spec.Tag)
				.EnablePull()
				.SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1"));
		});

	/// <summary>Build only the dashboard.web image.</summary>
	Target DockerBuildweb => d => d
		.Description("Build dashboard-web Docker image")
		.Executes(() =>
		{
			var spec = ImageSpecs[1];
			Log.Information("Building image: {Name} → {Tag}", spec.Name, spec.Tag);

			DockerBuild(s => s
				.SetPath(RootDirectory)
				.SetFile(spec.Dockerfile)
				.SetTag(spec.Tag)
				.EnablePull()
				.SetProcessEnvironmentVariable("DOCKER_BUILDKIT", "1"));
		});

	/// <summary>Push all images to registry.</summary>
	Target DockerImagePush => d => d
		.Description("Push Docker images to registry")
		.DependsOn<IDockerBuild>(x => x.DockerImageBuild)
		.Requires(() => Registry)
		.Executes(() =>
		{
			Log.Information("Pushing images to registry: {Registry}", Registry);

			// Push can also use CombineWith for parallel pushing
			DockerPush(s => s
					.CombineWith(ImageSpecs, (settings, img) => settings.SetName(img.Tag)),
				degreeOfParallelism: ParallelBuild ? 2 : 1);

			Log.Information("Images pushed successfully");
		});
}
