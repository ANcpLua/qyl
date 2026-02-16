// =============================================================================
// qyl Build System - Code Generation Pipeline
// =============================================================================
// TypeSpec → OpenAPI → C#/DuckDB/TypeScript
// One command: nuke Generate (does everything)
// =============================================================================

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Domain.CodeGen;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
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
interface IPipeline : IHazSourcePaths
{
    // ════════════════════════════════════════════════════════════════════════
    // Parameters
    // ════════════════════════════════════════════════════════════════════════

    [Parameter("Force overwrite of existing generated files")]
    bool? ForceGenerate => TryGetValue<bool?>(() => ForceGenerate);

    [Parameter("Preview changes without writing files")]
    bool? DryRunGenerate => TryGetValue<bool?>(() => DryRunGenerate);

    [Parameter("Source schema version for migration (auto-detected from git if omitted)")]
    int? FromVersion => TryGetValue<int?>(() => FromVersion);

    [Parameter("Target schema version for migration (auto-detected from current DDL if omitted)")]
    int? ToVersion => TryGetValue<int?>(() => ToVersion);

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
    // Semconv Generator Paths (OTel Semantic Conventions)
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath SemconvDirectory => RootDirectory / "eng" / "semconv";

    // ════════════════════════════════════════════════════════════════════════
    // Frontend Paths
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath DashboardDistDirectory => DashboardDirectory / "dist";
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
        .DependsOn(GenerateSemconv)
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
    // Semconv Targets (OTel Semantic Conventions)
    // ════════════════════════════════════════════════════════════════════════

    Target SemconvInstall => d => d
        .Description("Install semconv-generator npm dependencies")
        .OnlyWhenStatic(() => SemconvDirectory.DirectoryExists())
        .Executes(() =>
        {
            Log.Information("Installing semconv-generator dependencies...");

            NpmTasks.NpmInstall(s => s
                .SetProcessWorkingDirectory<NpmInstallSettings>(SemconvDirectory));

            Log.Information("Semconv dependencies installed");
        });

    Target GenerateSemconv => d => d
        .Description("Generate OTel Semantic Conventions (C#, TypeScript, TypeSpec, DuckDB)")
        .DependsOn(SemconvInstall)
        .OnlyWhenStatic(() => SemconvDirectory.DirectoryExists())
        .Executes(() =>
        {
            Log.Information("Generating OTel Semantic Conventions...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(SemconvDirectory)
                .SetCommand("generate"));

            Log.Information("Semconv generated to final destinations:");
            Log.Information("  TypeSpec: core/specs/generated/semconv.g.tsp");
            Log.Information("  C#: src/qyl.servicedefaults/Instrumentation/SemanticConventions.g.cs");
            Log.Information("  C# UTF-8: src/qyl.servicedefaults/Instrumentation/SemanticConventions.Utf8.g.cs");
            Log.Information("  TypeScript: src/qyl.dashboard/src/lib/semconv.ts");
            Log.Information("  DuckDB: src/qyl.collector/Storage/promoted-columns.g.sql");
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
    ///     TypeSpec → OpenAPI → C#/DuckDB/TypeScript + OTel Semconv
    /// </summary>
    Target Generate => d => d
        .Description("Generate ALL code from TypeSpec God Schema (C# + DuckDB + TypeScript + Semconv)")
        .DependsOn(TypeSpecCompile)
        .DependsOn(GenerateTypeScript)
        .DependsOn(GenerateSemconv)
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

            var paths = CodegenPaths.From(this);
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
            Log.Information("  OTel Semconv: servicedefaults/Instrumentation/SemanticConventions.g.cs");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Schema Migration Target
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Generate DuckDB migration SQL by diffing previous and current DDL.
    ///     Auto-detects versions from <c>DuckDbSchema.g.cs</c> (current) and git HEAD (previous).
    ///     Override with <c>--ipipeline-from-version</c> and <c>--ipipeline-to-version</c>.
    /// </summary>
    Target Migrate => d => d
        .Description("Generate DuckDB migration from schema diff (auto-detects versions from git)")
        .DependsOn(Generate)
        .Executes(() =>
        {
            var paths = CodegenPaths.From(this);
            var schemaFile = paths.CollectorStorage / "DuckDbSchema.g.cs";

            if (!schemaFile.FileExists())
            {
                Log.Error("DuckDbSchema.g.cs not found at {Path}", schemaFile);
                Log.Error("Run 'nuke Generate' first.");
                throw new FileNotFoundException("DuckDbSchema.g.cs not found", schemaFile);
            }

            // ── Read current DDL ────────────────────────────────────────────
            var currentContent = File.ReadAllText(schemaFile);
            var currentVersion = ToVersion ?? ExtractSchemaVersion(currentContent);
            var currentDdl = ExtractDdlFromCSharp(currentContent);

            Log.Information("Current schema version: {Version}", currentVersion);

            // ── Read previous DDL from git HEAD ─────────────────────────────
            var relativeSchemaPath = RootDirectory.GetRelativePathTo(schemaFile)
                .ToString().Replace('\\', '/');

            string previousContent;
            try
            {
                var output = GitTasks.Git($"show HEAD:{relativeSchemaPath}",
                    RootDirectory, logOutput: false, logInvocation: false);
                previousContent = string.Join(Environment.NewLine, output.Select(static o => o.Text));
            }
            catch (Exception ex)
            {
                Log.Warning("Could not read previous schema from git: {Message}", ex.Message);
                Log.Warning("This is expected for the first migration. No migration file generated.");
                return;
            }

            var previousVersion = FromVersion ?? ExtractSchemaVersion(previousContent);
            var previousDdl = ExtractDdlFromCSharp(previousContent);

            Log.Information("Previous schema version: {Version}", previousVersion);

            if (previousVersion == currentVersion)
            {
                Log.Information("Schema versions are identical ({Version}). No migration needed.", currentVersion);
                return;
            }

            // ── Generate migration ──────────────────────────────────────────
            var result = SchemaMigrationGenerator.GenerateMigration(
                previousDdl, currentDdl, previousVersion, currentVersion);

            if (result is null)
            {
                Log.Information("No schema changes detected between v{From} and v{To}.",
                    previousVersion, currentVersion);
                return;
            }

            Log.Information("Detected {Count} schema change(s):", result.Changes.Length);
            foreach (var change in result.Changes)
            {
                var detail = change.ColumnName is not null
                    ? $"{change.TableName}.{change.ColumnName}"
                    : change.TableName;
                Log.Information("  [{Kind}] {Detail}", change.Kind, detail);
            }

            // ── Write migration file ────────────────────────────────────────
            var migrationPath = SchemaMigrationGenerator.WriteMigrationFile(
                paths.Migrations, result);

            Log.Information("");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Migration Generated");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  From:    v{FromVersion}", previousVersion);
            Log.Information("  To:      v{ToVersion}", currentVersion);
            Log.Information("  Changes: {Count}", result.Changes.Length);
            Log.Information("  File:    {Path}", migrationPath);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    /// <summary>
    ///     Extracts the <c>Version</c> constant from DuckDbSchema.g.cs content.
    /// </summary>
    sealed int ExtractSchemaVersion(string content)
    {
        var match = Regex.Match(content, @"public\s+const\s+int\s+Version\s*=\s*(?<ver>\d+)\s*;");
        return match.Success
            ? int.Parse(match.Groups["ver"].Value, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException(
                "Could not extract Version constant from DuckDbSchema.g.cs");
    }

    /// <summary>
    ///     Extracts raw DDL statements from the C# string literals in DuckDbSchema.g.cs.
    ///     Returns concatenated DDL suitable for <see cref="SchemaMigrationGenerator.GenerateMigration" />.
    /// </summary>
    sealed string ExtractDdlFromCSharp(string content)
    {
        var ddl = new StringBuilder();

        foreach (Match match in VerifyRegexes.CreateTablePattern().Matches(content))
            ddl.AppendLine(match.Value);

        foreach (Match match in VerifyRegexes.CreateIndexPattern().Matches(content))
            ddl.AppendLine(match.Value);

        return ddl.ToString();
    }
}