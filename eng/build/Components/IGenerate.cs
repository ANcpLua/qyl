using Context;
using Domain.CodeGen;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Components;

/// <summary>
///     Code generation from TypeSpec → OpenAPI → C#/DuckDB.
///     One button: nuke Generate
/// </summary>
[ParameterPrefix(nameof(IGenerate))]
interface IGenerate : IHasSolution
{
    [Parameter("Force overwrite of existing generated files")]
    bool? ForceGenerate => TryGetValue<bool?>(() => ForceGenerate);

    [Parameter("Preview changes without writing files")]
    bool? DryRunGenerate => TryGetValue<bool?>(() => DryRunGenerate);

    /// <summary>
    ///     Generate all code from OpenAPI spec.
    ///     TypeSpec → OpenAPI → C#/DuckDB
    /// </summary>
    Target Generate => d => d
        .Description("Generate code from TypeSpec God Schema")
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .Executes(() =>
        {
            var force = ForceGenerate ?? false;
            var dryRun = DryRunGenerate ?? false;

            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  TypeSpec → OpenAPI → C#/DuckDB");
            Log.Information("═══════════════════════════════════════════════════════════════");

            var guard = IsServerBuild
                ? GenerationGuard.ForCi()
                : dryRun
                    ? new GenerationGuard(dryRun: true)
                    : GenerationGuard.ForLocal(force);

            var paths = BuildPaths.From(this);
            var openApiPath = paths.OpenApi / "openapi.yaml";

            if (!openApiPath.FileExists())
            {
                Log.Error("OpenAPI spec not found: {Path}", openApiPath);
                Log.Error("Run 'nuke TypeSpecCompile' first.");
                throw new System.IO.FileNotFoundException("OpenAPI spec not found", openApiPath);
            }

            // One call generates everything
            var result = SchemaGenerator.Generate(openApiPath, paths.Protocol, paths.Collector, guard);

            Log.Information("");
            guard.LogSummary(IsServerBuild);

            Log.Information("");
            Log.Information("Dashboard TypeScript: cd src/qyl.dashboard && npm run generate:ts");
        });
}
