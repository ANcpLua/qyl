using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;

namespace Components.Theory;

/// <summary>
/// Extension paths for IHasSolution used by TypeSpec compilation and code generation.
/// These paths follow the qyl repository structure:
///
/// qyl/
/// ├── core/
/// │   ├── specs/           # TypeSpec source files
/// │   ├── generated/       # All generated output
/// │   │   ├── openapi/     # OpenAPI spec
/// │   │   ├── duckdb/      # DuckDB schema
/// │   │   ├── csharp/      # Generated C# 
/// │   │   └── typescript/  # Generated TypeScript
/// │   └── primitives/      # Kiota polyglot output
/// │       ├── dotnet/
/// │       ├── python/
/// │       └── typescript/
/// ├── src/
/// │   ├── qyl.protocol/    # Shared contracts
/// │   ├── qyl.collector/   # Backend
/// │   └── qyl.dashboard/   # Frontend
/// └── eng/
///     └── build/           # NUKE build
/// </summary>
internal interface IHasSolution : Components.IHasSolution
{
    // ════════════════════════════════════════════════════════════════════════
    // Core Directory (Specs & Generated)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Core directory containing specs and generated artifacts.</summary>
    AbsolutePath CoreDirectory => RootDirectory / "core";

    /// <summary>TypeSpec specification files.</summary>
    AbsolutePath SpecsDirectory => CoreDirectory / "specs";

    /// <summary>All generated output (openapi, duckdb, csharp, typescript).</summary>
    AbsolutePath GeneratedDirectory => CoreDirectory / "generated";

    /// <summary>Generated OpenAPI specification.</summary>
    AbsolutePath OpenApiDirectory => GeneratedDirectory / "openapi";

    /// <summary>Kiota-generated polyglot primitives.</summary>
    AbsolutePath PrimitivesDirectory => CoreDirectory / "primitives";

    /// <summary>Generated .NET primitives.</summary>
    AbsolutePath PrimitivesDotNetDirectory => PrimitivesDirectory / "dotnet";

    /// <summary>Generated Python primitives.</summary>
    AbsolutePath PrimitivesPythonDirectory => PrimitivesDirectory / "python";

    /// <summary>Generated TypeScript primitives.</summary>
    AbsolutePath PrimitivesTypeScriptDirectory => PrimitivesDirectory / "typescript";

    // ════════════════════════════════════════════════════════════════════════
    // Protocol Project Paths
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>qyl.protocol shared contracts project.</summary>
    AbsolutePath ProtocolDirectory => SourceDirectory / "qyl.protocol";

    // ════════════════════════════════════════════════════════════════════════
    // Dashboard Paths
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Dashboard source directory.</summary>
    AbsolutePath DashboardSrcDirectory => DashboardDirectory / "src";

    // ════════════════════════════════════════════════════════════════════════
    // Engineering Paths
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Engineering configuration root.</summary>
    AbsolutePath EngDirectory => RootDirectory / "eng";

    /// <summary>.NET configuration (editorconfig, BannedSymbols).</summary>
    AbsolutePath EngConfigDotNetDirectory => EngDirectory / "dotnet";

    // ════════════════════════════════════════════════════════════════════════
    // Build Project Reference
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>NUKE build project.</summary>
    AbsolutePath BuildProjectDirectory => EngDirectory / "build";
}
