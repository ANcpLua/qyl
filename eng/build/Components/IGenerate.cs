using Context;
using Domain.CodeGen;
using Domain.CodeGen.Generators;
using Domain.Utilities;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Components;

/// <summary>
///     Code generation component using <see cref="QylSchema" /> as single source of truth.
///     Generates C# domain models and DuckDB schema.
///     Dashboard TypeScript types are generated separately via openapi-typescript from OpenAPI spec.
/// </summary>
interface IGenerate : IHasSolution
{
    [Parameter("Force overwrite of existing generated files")]
    bool? ForceGenerate => null;

    [Parameter("Preview changes without writing files")]
    bool? DryRunGenerate => null;

    [Parameter("Root namespace for generated C# code")]
    string RootNamespace => TryGetValue(() => RootNamespace) ?? "qyl.protocol";

    /// <summary>
    ///     Generate all code from QylSchema (C# models + DuckDB schema).
    /// </summary>
    Target Generate => d => d
        .Description("Generate code from QylSchema (C# models, DuckDB schema)")
        .Executes(() =>
        {
            var force = ForceGenerate ?? false;
            var dryRun = DryRunGenerate ?? false;

            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Code Generation from QylSchema");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Source: {Source}", GeneratedFileHeaders.SchemaSource);
            Log.Information("  Force:  {Force}", force);
            Log.Information("  DryRun: {DryRun}", dryRun);
            Log.Information("═══════════════════════════════════════════════════════════════");

            var guard = IsServerBuild
                ? GenerationGuard.ForCi()
                : dryRun
                    ? GenerationGuard.ForPreview()
                    : GenerationGuard.ForLocal(force);

            var schema = QylSchema.Instance;
            var paths = BuildPaths.From(this);

            // Define generators (C# for domain models, DuckDB for storage)
            // TypeScript is NOT generated here - use openapi-typescript from OpenAPI spec instead
            IGenerator[] generators =
            [
                new CSharpGenerator(),
                new DuckDbGenerator()
            ];

            // Run each generator
            foreach (var generator in generators)
            {
                Log.Information("");
                Log.Information("Running {Generator}...", generator.Name);

                var outputs = generator.Generate(schema, paths, RootNamespace);

                foreach (var (relativePath, content) in outputs)
                {
                    var absolutePath = GetOutputPath(paths, generator.Name, relativePath);
                    guard.WriteIfAllowed(absolutePath, content, $"{generator.Name}: {relativePath}");
                }
            }

            // Summary
            Log.Information("");
            guard.LogSummary(IsServerBuild);

            Log.Information("");
            Log.Information("Note: Dashboard TypeScript types are generated from OpenAPI spec:");
            Log.Information("  cd src/qyl.dashboard && npm run generate:types");
        });

    /// <summary>
    ///     Generate only C# models/primitives.
    /// </summary>
    Target GenerateCSharp => d => d
        .Description("Generate only C# code from QylSchema")
        .Executes(() =>
        {
            var guard = CreateGuard();
            var schema = QylSchema.Instance;
            var paths = BuildPaths.From(this);

            Log.Information("Generating C# from QylSchema...");

            var generator = new CSharpGenerator();
            var outputs = generator.Generate(schema, paths, RootNamespace);

            foreach (var (relativePath, content) in outputs)
            {
                var absolutePath = paths.Protocol / relativePath;
                guard.WriteIfAllowed(absolutePath, content, relativePath);
            }

            guard.LogSummary(false);
        });

    /// <summary>
    ///     Generate only DuckDB schema.
    /// </summary>
    Target GenerateDuckDb => d => d
        .Description("Generate only DuckDB schema from QylSchema")
        .Executes(() =>
        {
            var guard = CreateGuard();
            var schema = QylSchema.Instance;
            var paths = BuildPaths.From(this);

            Log.Information("Generating DuckDB schema from QylSchema...");

            var generator = new DuckDbGenerator();
            var outputs = generator.Generate(schema, paths, RootNamespace);

            foreach (var (relativePath, content) in outputs)
            {
                var absolutePath = paths.CollectorStorage / relativePath;
                guard.WriteIfAllowed(absolutePath, content, relativePath);
            }

            guard.LogSummary(false);
        });

    private GenerationGuard CreateGuard()
    {
        var force = ForceGenerate ?? false;
        var dryRun = DryRunGenerate ?? false;

        return IsServerBuild
            ? GenerationGuard.ForCi()
            : dryRun
                ? GenerationGuard.ForPreview()
                : GenerationGuard.ForLocal(force);
    }

    private static AbsolutePath GetOutputPath(BuildPaths paths, string generatorName, string relativePath) =>
        generatorName switch
        {
            "CSharp" => paths.Protocol / relativePath,
            "DuckDB" => paths.CollectorStorage / relativePath,
            _ => paths.Generated / relativePath
        };
}
