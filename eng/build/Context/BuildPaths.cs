using Nuke.Common;
using Nuke.Common.IO;

namespace Context;

/// <summary>
///     Single source of truth for all build paths in the qyl repository.
///     Use <see cref="From" /> to create from a NUKE build instance.
/// </summary>
public sealed record BuildPaths(AbsolutePath Root)
{
    // ════════════════════════════════════════════════════════════════════════
    // Schema (God Schema - Single Source of Truth)
    // ════════════════════════════════════════════════════════════════════════

    public AbsolutePath Schema => Root / "schema";

    public AbsolutePath SchemaGenerated => Schema / "generated";

    public AbsolutePath OpenApi => SchemaGenerated;

    // ════════════════════════════════════════════════════════════════════════
    // Core (Legacy - kept for compatibility)
    // ════════════════════════════════════════════════════════════════════════

    public AbsolutePath Core => Root / "core";

    // ════════════════════════════════════════════════════════════════════════
    // Generated Outputs
    // ════════════════════════════════════════════════════════════════════════

    public AbsolutePath Generated => Core / "generated";

    public AbsolutePath GeneratedOpenApi => Generated / "openapi";

    public AbsolutePath GeneratedCSharp => Generated / "csharp";

    public AbsolutePath GeneratedDuckDb => Generated / "duckdb";

    public AbsolutePath GeneratedTypeScript => Generated / "typescript";

    // ════════════════════════════════════════════════════════════════════════
    // Source Projects
    // ════════════════════════════════════════════════════════════════════════

    public AbsolutePath Src => Root / "src";

    public AbsolutePath Protocol => Src / "qyl.protocol";

    public AbsolutePath ProtocolModels => Protocol / "Models";

    public AbsolutePath ProtocolPrimitives => Protocol / "Primitives";

    public AbsolutePath ProtocolAttributes => Protocol / "Attributes";

    public AbsolutePath ProtocolContracts => Protocol / "Contracts";

    public AbsolutePath Collector => Src / "qyl.collector";

    public AbsolutePath CollectorStorage => Collector / "Storage";

    public AbsolutePath Dashboard => Src / "qyl.dashboard";

    public AbsolutePath DashboardSrc => Dashboard / "src";

    public AbsolutePath DashboardTypes => DashboardSrc / "types" / "generated";

    public AbsolutePath Mcp => Src / "qyl.mcp";

    // ════════════════════════════════════════════════════════════════════════
    // Build Artifacts
    // ════════════════════════════════════════════════════════════════════════

    public AbsolutePath Artifacts => Root / "Artifacts";

    public AbsolutePath TestResults => Root / "TestResults";

    public AbsolutePath Coverage => Artifacts / "coverage";

    // ════════════════════════════════════════════════════════════════════════
    // Tests & Examples
    // ════════════════════════════════════════════════════════════════════════

    public AbsolutePath Tests => Root / "tests";

    public AbsolutePath CollectorTests => Tests / "qyl.collector.tests";

    public AbsolutePath Examples => Root / "examples";

    // ════════════════════════════════════════════════════════════════════════
    // Engineering
    // ════════════════════════════════════════════════════════════════════════

    public AbsolutePath Eng => Root / "eng";

    public AbsolutePath EngBuild => Eng / "build";

    public AbsolutePath EngMsBuild => Eng / "MSBuild";

    // ════════════════════════════════════════════════════════════════════════
    // Factory
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Creates a <see cref="BuildPaths" /> instance from a NUKE build.
    /// </summary>
    public static BuildPaths From(INukeBuild build) => new(build.RootDirectory);
}