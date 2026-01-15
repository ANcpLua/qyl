// =============================================================================
// qyl Build System - Code Generation Pipeline
// =============================================================================
// TypeSpec → OpenAPI → C#/DuckDB/TypeScript
// One command: nuke Generate (does everything)
// =============================================================================

using System.IO;
using Context;
using Domain.CodeGen;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

// ════════════════════════════════════════════════════════════════════════════════
// IPipeline - Unified Code Generation Interface
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
///     Unified pipeline: TypeSpec + Frontend + Code Generation.
///     Single entry point: <c>nuke Generate</c>
/// </summary>
[ParameterPrefix(nameof(IPipeline))]
interface IPipeline : IHasSolution
{
    // ════════════════════════════════════════════════════════════════════════
    // Parameters
    // ════════════════════════════════════════════════════════════════════════

    [Parameter("Force overwrite of existing generated files")]
    bool? ForceGenerate => TryGetValue<bool?>(() => ForceGenerate);

    [Parameter("Preview changes without writing files")]
    bool? DryRunGenerate => TryGetValue<bool?>(() => DryRunGenerate);

    // ════════════════════════════════════════════════════════════════════════
    // TypeSpec Paths (God Schema - Single Source of Truth)
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath TypeSpecDirectory => RootDirectory / "core" / "specs";
    AbsolutePath TypeSpecEntry => TypeSpecDirectory / "main.tsp";
    AbsolutePath TypeSpecConfig => TypeSpecDirectory / "tspconfig.yaml";
    AbsolutePath TypeSpecGenerated => RootDirectory / "core" / "openapi";
    AbsolutePath OpenApiOutput => TypeSpecGenerated / "openapi.yaml";
    AbsolutePath JsonSchemaOutput => TypeSpecGenerated;

    // ════════════════════════════════════════════════════════════════════════
    // Frontend Paths
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath DashboardDistDirectory => DashboardDirectory / "dist";
    AbsolutePath NodeModulesDirectory => DashboardDirectory / "node_modules";
    AbsolutePath DashboardTypesDirectory => DashboardDirectory / "src" / "types";

    // ════════════════════════════════════════════════════════════════════════
    // TypeSpec Targets
    // ════════════════════════════════════════════════════════════════════════

    Target TypeSpecInstall => d => d
        .Description("Install TypeSpec dependencies")
        .OnlyWhenStatic(() => TypeSpecDirectory.DirectoryExists())
        .Executes(() =>
        {
            Log.Information("Installing TypeSpec dependencies...");

            NpmTasks.NpmInstall(s => s
                .SetProcessWorkingDirectory<NpmInstallSettings>(TypeSpecDirectory)
                .AddProcessAdditionalArguments("--legacy-peer-deps"));

            Log.Information("TypeSpec dependencies installed");
        });

    Target TypeSpecCompile => d => d
        .Description("Compile TypeSpec God Schema → OpenAPI 3.1 + JSON Schema")
        .DependsOn(TypeSpecInstall)
        .OnlyWhenStatic(() => TypeSpecEntry.FileExists())
        .Produces(OpenApiOutput)
        .Produces(JsonSchemaOutput / "**/*.json")
        .Executes(() =>
        {
            Log.Information("Compiling TypeSpec God Schema...");
            Log.Information("  Entry:  {Entry}", TypeSpecEntry);
            Log.Information("  Config: {Config}", TypeSpecConfig);

            TypeSpecGenerated.CreateDirectory();

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(TypeSpecDirectory)
                .SetCommand("compile"));

            if (OpenApiOutput.FileExists())
            {
                var size = new FileInfo(OpenApiOutput).Length;
                Log.Information("OpenAPI generated: {Output} ({Size:N0} bytes)", OpenApiOutput, size);
            }
            else
            {
                Log.Warning("OpenAPI output not found at {Output}", OpenApiOutput);
            }
        });

    Target TypeSpecInfo => d => d
        .Description("Show TypeSpec God Schema configuration and status")
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl TypeSpec God Schema Configuration");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Source (God Schema):");
            Log.Information("    TypeSpec Dir : {Path} ({Exists})",
                TypeSpecDirectory, TypeSpecDirectory.DirectoryExists() ? "exists" : "MISSING");
            Log.Information("    Entry Point  : {Path} ({Exists})",
                TypeSpecEntry, TypeSpecEntry.FileExists() ? "exists" : "MISSING");
            Log.Information("    Config       : {Path} ({Exists})",
                TypeSpecConfig, TypeSpecConfig.FileExists() ? "exists" : "MISSING");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Generated Output:");
            Log.Information("    Generated Dir: {Path} ({Exists})",
                TypeSpecGenerated, TypeSpecGenerated.DirectoryExists() ? "exists" : "not generated");
            Log.Information("    OpenAPI      : {Path} ({Exists})",
                OpenApiOutput, OpenApiOutput.FileExists() ? "exists" : "not generated");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Generation Flow:");
            Log.Information("    core/specs/*.tsp → core/openapi/openapi.yaml → C#/DuckDB/TypeScript");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Usage:");
            Log.Information("    nuke Generate  # Generates ALL (TypeSpec + C# + DuckDB + TypeScript)");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    Target TypeSpecClean => d => d
        .Description("Clean TypeSpec generated artifacts")
        .Executes(() =>
        {
            Log.Information("Cleaning TypeSpec artifacts...");

            if (TypeSpecGenerated.DirectoryExists())
            {
                TypeSpecGenerated.DeleteDirectory();
                Log.Information("  Deleted: {Dir}", TypeSpecGenerated);
            }

            Log.Information("TypeSpec artifacts cleaned");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Frontend Targets
    // ════════════════════════════════════════════════════════════════════════

    Target FrontendInstall => d => d
        .Description("Install frontend npm dependencies")
        .Executes(() =>
        {
            Log.Information("Installing npm dependencies in {Directory}", DashboardDirectory);

            NpmTasks.NpmInstall(s => s
                .SetProcessWorkingDirectory<NpmInstallSettings>(DashboardDirectory));
        });

    Target FrontendBuild => d => d
        .Description("Build frontend for production (tsc + vite build)")
        .DependsOn(FrontendInstall)
        .Produces(DashboardDistDirectory / "**/*")
        .Executes(() =>
        {
            Log.Information("Building qyl.dashboard...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("build"));

            Log.Information("Dashboard built → {Output}", DashboardDistDirectory);
        });

    Target FrontendTest => d => d
        .Description("Run frontend tests (Vitest)")
        .DependsOn(FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Running frontend tests...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("test")
                .SetArguments("--", "--run"));
        });

    Target FrontendCoverage => d => d
        .Description("Run frontend tests with coverage")
        .DependsOn(FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Running frontend tests with coverage...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("test:coverage"));
        });

    Target FrontendLint => d => d
        .Description("Lint frontend code (ESLint)")
        .DependsOn(FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Linting frontend code...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("lint"));
        });

    Target FrontendLintFix => d => d
        .Description("Fix frontend linting issues")
        .DependsOn(FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Fixing frontend linting issues...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("lint:fix"));
        });

    Target FrontendDev => d => d
        .Description("Start frontend dev server (Vite)")
        .DependsOn(FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Starting Vite dev server for qyl.dashboard...");
            Log.Information("  URL: http://localhost:5173");
            Log.Information("  API: Proxied to http://localhost:5100 (override with VITE_API_URL)");
            Log.Information("  Press Ctrl+C to stop");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("dev"));
        });

    Target FrontendClean => d => d
        .Description("Clean frontend build artifacts")
        .Executes(() =>
        {
            Log.Information("Cleaning frontend artifacts...");
            DashboardDistDirectory.DeleteDirectory();
            Log.Information("Cleaned: {Directory}", DashboardDistDirectory);
        });

    Target FrontendTypeCheck => d => d
        .Description("Type check frontend (tsc --noEmit)")
        .DependsOn(FrontendInstall)
        .Executes(() =>
        {
            Log.Information("Type checking frontend...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("typecheck"));
        });

    // ════════════════════════════════════════════════════════════════════════
    // Code Generation Targets
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Generate TypeScript types from OpenAPI spec.
    ///     Uses openapi-typescript to generate strongly-typed API client.
    /// </summary>
    Target GenerateTypeScript => d => d
        .Description("Generate TypeScript types from OpenAPI")
        .DependsOn(TypeSpecCompile)
        .DependsOn(FrontendInstall)
        .Produces(DashboardTypesDirectory / "api.ts")
        .Executes(() =>
        {
            if (!OpenApiOutput.FileExists())
            {
                Log.Warning("OpenAPI spec not found: {Path}", OpenApiOutput);
                Log.Warning("Run 'nuke TypeSpecCompile' first.");
                return;
            }

            Log.Information("Generating TypeScript types from OpenAPI...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("generate:ts"));

            Log.Information("TypeScript types generated → {Output}", DashboardTypesDirectory / "api.ts");
        });

    /// <summary>
    ///     Generate all code from OpenAPI spec.
    ///     TypeSpec → OpenAPI → C#/DuckDB/TypeScript
    /// </summary>
    Target Generate => d => d
        .Description("Generate ALL code from TypeSpec God Schema (C# + DuckDB + TypeScript)")
        .DependsOn(TypeSpecCompile)
        .DependsOn(GenerateTypeScript)
        .Executes(() =>
        {
            var force = ForceGenerate ?? false;
            var dryRun = DryRunGenerate ?? false;

            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  TypeSpec → OpenAPI → C#/DuckDB/TypeScript");
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
                throw new FileNotFoundException("OpenAPI spec not found", openApiPath);
            }

            // Generate C#/DuckDB
            _ = SchemaGenerator.Generate(openApiPath, paths.Protocol, paths.Collector, guard);

            Log.Information("");
            guard.LogSummary(IsServerBuild);

            Log.Information("");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Generation Complete");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  TypeScript: dashboard/src/types/api.ts");
            Log.Information("  C# Records: protocol/*.g.cs");
            Log.Information("  DuckDB DDL: collector/Storage/DuckDbSchema.g.cs");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });
}