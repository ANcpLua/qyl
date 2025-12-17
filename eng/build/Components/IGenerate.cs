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
///     Generates C#, TypeScript, and DuckDB schema files.
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
    ///     Generate all code from QylSchema.
    /// </summary>
    Target Generate => d => d
        .Description("Generate code from QylSchema (C#, TypeScript, DuckDB)")
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

            // Define generators
            IGenerator[] generators =
            [
                new CSharpGenerator(),
                new TypeScriptGenerator(),
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
                    // C# goes to qyl.protocol, TS goes to dashboard, DuckDB to collector
                    var absolutePath = GetOutputPath(paths, generator.Name, relativePath);
                    guard.WriteIfAllowed(absolutePath, content, $"{generator.Name}: {relativePath}");
                }
            }

            // Summary
            Log.Information("");
            guard.LogSummary(IsServerBuild);
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
    ///     Generate only TypeScript types.
    /// </summary>
    Target GenerateTypeScript => d => d
        .Description("Generate only TypeScript from QylSchema")
        .Executes(() =>
        {
            var guard = CreateGuard();
            var schema = QylSchema.Instance;
            var paths = BuildPaths.From(this);

            Log.Information("Generating TypeScript from QylSchema...");

            var generator = new TypeScriptGenerator();
            var outputs = generator.Generate(schema, paths, RootNamespace);

            foreach (var (relativePath, content) in outputs)
            {
                var absolutePath = paths.DashboardTypes / relativePath;
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
            "TypeScript" => paths.DashboardTypes / relativePath,
            "DuckDB" => paths.CollectorStorage / relativePath,
            _ => paths.Generated / relativePath
        };
}