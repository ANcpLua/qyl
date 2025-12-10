using Nuke.Common;
using Nuke.Common.Tooling;
using Serilog;

namespace Components;

/// <summary>
///     Docker Compose orchestration component.
///     Uses ProcessTasks for docker compose CLI since NUKE doesn't have native DockerCompose tasks.
/// </summary>
[ParameterPrefix(nameof(IDockerCompose))]
internal interface IDockerCompose : IHasSolution
{
	/// <summary>Build images before starting containers.</summary>
	[Parameter("Build images before starting (--build)")]
	bool? DockerBuild => null;

	/// <summary>Force recreate containers.</summary>
	[Parameter("Force recreate containers (--force-recreate)")]
	bool? ForceRecreate => null;

	/// <summary>Remove volumes when stopping.</summary>
	[Parameter("Remove volumes on down (-v)")]
	bool? RemoveVolumes => null;

	/// <summary>Number of log lines to show.</summary>
	[Parameter("Number of log lines to tail")]
	int? LogTail => null;

	/// <summary>Specific service to target (empty = all).</summary>
	[Parameter("Target specific service")]
	string? Service => null;

	// ══════════════════════════════════════════════════════════════════════════
	// TARGETS
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Start the Docker Compose stack.</summary>
	Target DockerUp => d => d
		.Description("Start Docker Compose stack (infrastructure + applications)")
		.Executes(() =>
		{
			Log.Information("Starting Docker Compose stack...");
			Log.Information("  Compose file: {File}", ComposeFile);

			var args = $"-f \"{ComposeFile}\" up -d --remove-orphans";
			if (DockerBuild == true) args += " --build";
			if (ForceRecreate == true) args += " --force-recreate";
			if (!string.IsNullOrEmpty(Service)) args += $" {Service}";

			RunDockerCompose(args);

			Log.Information("Docker Compose stack started successfully");
			Log.Information("  Receiver: http://localhost:5000 (REST API)");
			Log.Information("  OTLP:     http://localhost:4317 (gRPC), :4318 (HTTP)");
			Log.Information("  web:      http://localhost:3000");
		});

	/// <summary>Stop the Docker Compose stack.</summary>
	Target DockerDown => d => d
		.Description("Stop Docker Compose stack")
		.Executes(() =>
		{
			Log.Information("Stopping Docker Compose stack...");

			var args = $"-f \"{ComposeFile}\" down --remove-orphans";
			if (RemoveVolumes == true) args += " -v";

			RunDockerCompose(args);

			Log.Information("Docker Compose stack stopped");
		});

	/// <summary>Show container status.</summary>
	Target DockerStatus => d => d
		.Description("Show Docker Compose container status")
		.Executes(() => RunDockerCompose($"-f \"{ComposeFile}\" ps"));

	/// <summary>Tail container logs.</summary>
	Target DockerLogs => d => d
		.Description("Tail Docker Compose logs")
		.Executes(() =>
		{
			var tail = LogTail ?? 100;
			var args = $"-f \"{ComposeFile}\" logs -f --tail {tail}";
			if (!string.IsNullOrEmpty(Service)) args += $" {Service}";

			RunDockerCompose(args);
		});

	/// <summary>Restart the stack (down + up).</summary>
	Target DockerRestart => d => d
		.Description("Restart Docker Compose stack (down + up)")
		.DependsOn<IDockerCompose>(x => x.DockerDown)
		.DependsOn<IDockerCompose>(x => x.DockerUp)
		.Executes(() => Log.Information("Docker Compose stack restarted"));

	/// <summary>Pull latest images.</summary>
	Target DockerPull => d => d
		.Description("Pull latest Docker images")
		.Executes(() =>
		{
			Log.Information("Pulling latest images...");
			RunDockerCompose($"-f \"{ComposeFile}\" pull");
			Log.Information("Images pulled successfully");
		});

	// ══════════════════════════════════════════════════════════════════════════
	// HELPERS
	// ══════════════════════════════════════════════════════════════════════════

	private void RunDockerCompose(string arguments)
	{
		ProcessTasks.StartProcess(
				"docker",
				$"compose {arguments}",
				SourceDirectory,
				logOutput: true)
			.AssertZeroExitCode();
	}
}
