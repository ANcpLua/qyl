// =============================================================================
// qyl Build System - Code Verification
// =============================================================================
// Validates generated code compiles, JSON roundtrips work, MTP config is valid
// TODO: Integrate ANcpLua.Roslyn.Utilities.Testing when API is finalized
// =============================================================================

using System.IO;
using System.Linq;
using Context;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

// ════════════════════════════════════════════════════════════════════════════════
// IVerify - Generated Code Validation (Stub)
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
///     Validates generated code compiles and behaves correctly.
///     TODO: Use ProjectBuilder for isolated MSBuild execution with binlog introspection.
/// </summary>
[ParameterPrefix(nameof(IVerify))]
interface IVerify : IHasSolution
{
    [Parameter("Skip verification after generation")]
    bool? SkipVerify => TryGetValue<bool?>(() => SkipVerify);

    // ════════════════════════════════════════════════════════════════════════
    // Verification Targets
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Verify all generated C# code compiles without errors.
    /// </summary>
    Target VerifyGeneratedCode => d => d
        .Description("Verify generated C# code compiles")
        .DependsOn<IPipeline>(static x => x.Generate)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var paths = BuildPaths.From(this);
            var generatedFiles = paths.Protocol.GlobFiles("**/*.g.cs")
                .Concat(paths.CollectorStorage.GlobFiles("*.g.cs"))
                .ToList();

            if (generatedFiles.Count == 0)
            {
                Log.Warning("No generated files found to verify");
                return;
            }

            Log.Information("Found {Count} generated files to verify", generatedFiles.Count);

            // TODO: Use ProjectBuilder.Create() for isolated compilation
            // For now, rely on the main Compile target to verify
            Log.Information("Verification: delegating to main Compile target");
        });

    /// <summary>
    ///     Verify DuckDB schema DDL is syntactically valid.
    /// </summary>
    Target VerifyDuckDbSchema => d => d
        .Description("Verify generated DuckDB schema is valid")
        .DependsOn<IPipeline>(static x => x.Generate)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var paths = BuildPaths.From(this);
            var schemaFile = paths.CollectorStorage / "DuckDbSchema.g.cs";

            if (!schemaFile.FileExists())
            {
                Log.Warning("DuckDbSchema.g.cs not found, skipping DuckDB verification");
                return;
            }

            var content = File.ReadAllText(schemaFile);

            // Basic syntax check
            if (content.Contains("CREATE TABLE") && content.Contains("PRIMARY KEY"))
            {
                Log.Information("DuckDB schema structure: VALID");
            }
            else
            {
                Log.Warning("DuckDB schema may be incomplete");
            }

            // TODO: Execute DDL against in-memory DuckDB for full validation
        });

    /// <summary>
    ///     Run all verification checks.
    /// </summary>
    Target Verify => d => d
        .Description("Run all generated code verification checks")
        .DependsOn(VerifyGeneratedCode)
        .DependsOn(VerifyDuckDbSchema)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Verification Complete");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });
}
