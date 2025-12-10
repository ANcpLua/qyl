using Components;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;

/// <summary>
///     Kiota TypeScript client generation component.
///     Uses [PathVariable] Tool delegate since Kiota isn't in NUKE's supported CLI tools.
/// </summary>
/// <remarks>
///     The Tool delegate pattern is NUKE's recommended approach for CLIs without
///     dedicated *Tasks classes. It provides:
///     - Automatic PATH resolution
///     - Process lifecycle management
///     - Exit code assertion
///     - Clean invocation syntax
///
///     NOTE: This component is currently not used in Paperless.Telemetry.
///     It's retained as a reference for projects that need OpenAPI client generation.
/// </remarks>
[ParameterPrefix(nameof(IKiota))]
internal interface IKiota : IHasSolution
{
	/// <summary>Kiota CLI tool injected via PATH resolution.</summary>
	[PathVariable("kiota")]
	Tool? KiotaTool => TryGetValue(() => KiotaTool);

	/// <summary>OpenAPI specification file path.</summary>
	[Parameter("OpenAPI spec file path")]
	AbsolutePath? OpenApiSpec => null;

	/// <summary>Kiota configuration file.</summary>
	AbsolutePath KiotaConfigFile => WebUiDirectory / "kiota.json";

	/// <summary>Generated TypeScript client output directory.</summary>
	AbsolutePath KiotaOutputDirectory => WebUiDirectory / "src" / "api";

	/// <summary>Generated client class name.</summary>
	[Parameter("Generated client class name")]
	string ClientClassName => "TelemetryClient";

	/// <summary>Generated client namespace.</summary>
	[Parameter("Generated client namespace")]
	string ClientNamespace => "Telemetry.Api";

	// ══════════════════════════════════════════════════════════════════════════
	// TARGETS
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Generate TypeScript API client from OpenAPI spec.</summary>
	Target Kiota => d => d
		.Description("Generate TypeScript API client from OpenAPI spec")
		.Requires(() => OpenApiSpec)
		.Requires(() => KiotaTool)
		.OnlyWhenStatic(() => OpenApiSpec?.FileExists() == true)
		.Produces(KiotaOutputDirectory / "**/*")
		.Executes(() =>
		{
			Log.Information("Generating Kiota TypeScript client...");
			Log.Information("  OpenAPI: {Spec}", OpenApiSpec);
			Log.Information("  Output:  {Output}", KiotaOutputDirectory);

			// Tool delegate - clean invocation, NUKE handles process lifecycle
			KiotaTool!(
				$"generate " +
				$"--openapi \"{OpenApiSpec}\" " +
				$"--output \"{KiotaOutputDirectory}\" " +
				$"--language TypeScript " +
				$"--class-name {ClientClassName} " +
				$"--namespace-name {ClientNamespace} " +
				$"--clean-output",
				workingDirectory: RootDirectory);

			Log.Information("Kiota client generated successfully");
		});

	/// <summary>Clean generated Kiota client files.</summary>
	Target KiotaClean => d => d
		.Description("Clean generated Kiota client files")
		.Executes(() =>
		{
			if (KiotaOutputDirectory.DirectoryExists())
			{
				Log.Information("Cleaning Kiota output: {Directory}", KiotaOutputDirectory);
				KiotaOutputDirectory.DeleteDirectory();
				Log.Information("Kiota output cleaned");
			}
			else
			{
				Log.Information("Kiota output directory does not exist, nothing to clean");
			}
		});

	/// <summary>Show Kiota configuration and version.</summary>
	Target KiotaInfo => d => d
		.Description("Show Kiota configuration and status")
		.Executes(() =>
		{
			Log.Information("══════════════════════════════════════════════════════════════");
			Log.Information("  Kiota Configuration");
			Log.Information("══════════════════════════════════════════════════════════════");
			Log.Information("  OpenAPI Spec : {Path} ({Exists})",
				OpenApiSpec?.ToString() ?? "(not configured)",
				OpenApiSpec?.FileExists() == true ? "exists" : "MISSING");
			Log.Information("  Config File  : {Path} ({Exists})",
				KiotaConfigFile,
				KiotaConfigFile.FileExists() ? "exists" : "MISSING");
			Log.Information("  Output Dir   : {Path} ({Exists})",
				KiotaOutputDirectory,
				KiotaOutputDirectory.DirectoryExists() ? "exists" : "not generated");
			Log.Information("  Class Name   : {ClassName}", ClientClassName);
			Log.Information("  Namespace    : {Namespace}", ClientNamespace);
			Log.Information("══════════════════════════════════════════════════════════════");

			// Tool delegate also works for simple version checks
			if (KiotaTool is not null)
			{
				KiotaTool("--version");
			}
			else
			{
				Log.Warning("Kiota CLI not found in PATH");
			}
		});
}