using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;

namespace Components;

/// <summary>
///     Base component providing shared Solution and path properties.
///     All other components should inherit from this to access common infrastructure.
/// </summary>
internal interface IHasSolution : INukeBuild
{
	[Solution(GenerateProjects = true)]
	Solution Solution => TryGetValue(() => Solution)!;

	/// <summary>Artifacts output directory.</summary>
	AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";

	/// <summary>Test results directory.</summary>
	AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";

	/// <summary>Coverage reports directory.</summary>
	AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";

	/// <summary>Docker compose file path.</summary>
	AbsolutePath ComposeFile => SourceDirectory / "compose.yaml";

	/// <summary>Environment file for Docker Compose.</summary>
	AbsolutePath EnvFile => RootDirectory / ".env";

	/// <summary>Safe accessor for Solution.Path with fallback.</summary>
	AbsolutePath GetSolutionPath() =>
		Solution.Path ?? RootDirectory.GlobFiles("*.sln", "*.slnx").FirstOrDefault()
		?? throw new System.InvalidOperationException("Unable to locate solution file");

	// ══════════════════════════════════════════════════════════════════════════
	// SOURCE DIRECTORIES
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>Source directory containing all projects.</summary>
	AbsolutePath SourceDirectory => RootDirectory / "src";

	/// <summary>OTLP Receiver + REST API project directory.</summary>
	AbsolutePath ReceiverDirectory => SourceDirectory / "dashboard.Receiver";

	/// <summary>Frontend project directory (React SPA).</summary>
	AbsolutePath WebUiDirectory => SourceDirectory / "dashboard.web";

	/// <summary>lol semantic convention tooling directory.</summary>
	AbsolutePath LolDirectory => SourceDirectory / "lol";

	/// <summary>ZeroCode samples root directory.</summary>
	AbsolutePath ZeroCodeDirectory => SourceDirectory / "ZeroCode";

	// ══════════════════════════════════════════════════════════════════════════
	// ZEROCODE POLYGLOT SAMPLES
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>ZeroCode .NET sample directory.</summary>
	AbsolutePath ZeroCodeDotNetDirectory => SourceDirectory / "ZeroCode.DotNet";

	/// <summary>ZeroCode Java sample directory.</summary>
	AbsolutePath ZeroCodeJavaDirectory => SourceDirectory / "ZeroCode.Java";

	/// <summary>ZeroCode Python sample directory.</summary>
	AbsolutePath ZeroCodePythonDirectory => SourceDirectory / "ZeroCode.Python";

	/// <summary>ZeroCode Node.js sample directory.</summary>
	AbsolutePath ZeroCodeNodeDirectory => SourceDirectory / "ZeroCode.Node";

	/// <summary>ZeroCode Go sample directory.</summary>
	AbsolutePath ZeroCodeGoDirectory => SourceDirectory / "ZeroCode.Go";

	/// <summary>ZeroCode PHP sample directory.</summary>
	AbsolutePath ZeroCodePhpDirectory => SourceDirectory / "ZeroCode.PHP";
}
