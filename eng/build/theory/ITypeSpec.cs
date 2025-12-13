using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

namespace Components.Theory;

/// <summary>
/// TypeSpec compilation and Kiota polyglot client generation component.
///
/// Kiota enables:
/// - Single OpenAPI → Multi-language SDK generation
/// - Polyrepo-friendly: generated clients can live in separate repos
/// - Fine-grained generation: filter to specific API surface areas
/// - Build integration via dotnet global-tool
///
/// NUKE 10.1.0 patterns:
/// - ParameterPrefix for namespacing parameters
/// - DependsOn&lt;T&gt;(x => x.Target) for cross-component dependencies
/// - TryDependsOn&lt;T&gt;() for loose dependencies
/// - Async targets (convenience, still runs synchronously)
///
/// Generation Guard integration:
/// - --Force to overwrite existing generated files
/// - --DryRun to preview generation without writing
/// - --SkipExisting to skip all existing files
/// - Interactive prompts in terminal mode
/// </summary>
[ParameterPrefix(nameof(ITypeSpec))]
internal interface ITypeSpec : IHasSolution, IGenerationGuard
{
    // ════════════════════════════════════════════════════════════════════════
    // TypeSpec Source Paths
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath TypeSpecDirectory => SpecsDirectory;

    AbsolutePath TypeSpecEntry => TypeSpecDirectory / "main.tsp";

    AbsolutePath TypeSpecConfig => TypeSpecDirectory / "tspconfig.yaml";

    // ════════════════════════════════════════════════════════════════════════
    // Output Paths (core/openapi, core/primitives)
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath OpenApiOutput => OpenApiDirectory / "openapi.yaml";

    AbsolutePath JsonSchemaOutput => CoreDirectory / "schemas";

    /// <summary>Generated primitives root (TraceId, SpanId, etc.)</summary>
    AbsolutePath GeneratedPrimitivesRoot => PrimitivesDirectory;

    AbsolutePath GeneratedDotNet => PrimitivesDotNetDirectory;

    AbsolutePath GeneratedPython => PrimitivesPythonDirectory;

    AbsolutePath GeneratedTypeScript => PrimitivesTypeScriptDirectory;

    /// <summary>Dashboard types destination for TypeScript sync</summary>
    AbsolutePath DashboardTypesDestination => DashboardSrcDirectory / "types" / "generated";

    // ════════════════════════════════════════════════════════════════════════
    // Kiota Configuration
    // ════════════════════════════════════════════════════════════════════════

    [Parameter("Kiota namespace for C# generation")]
    string KiotaDotNetNamespace => TryGetValue(() => KiotaDotNetNamespace) ?? "Qyl.Core";

    [Parameter("Kiota class name for generated clients")]
    string KiotaClientName => TryGetValue(() => KiotaClientName) ?? "QylClient";

    [Parameter("Include patterns for Kiota generation (comma-separated)")]
    string KiotaIncludePatterns => TryGetValue(() => KiotaIncludePatterns);

    [Parameter("Exclude patterns for Kiota generation (comma-separated)")]
    string KiotaExcludePatterns => TryGetValue(() => KiotaExcludePatterns);

    // ════════════════════════════════════════════════════════════════════════
    // TypeSpec Installation
    // ════════════════════════════════════════════════════════════════════════

    Target TypeSpecInstall => d => d
        .Description("Install TypeSpec dependencies in core/specs")
        .OnlyWhenStatic(() => TypeSpecDirectory.DirectoryExists())
        .Executes(() =>
        {
            Log.Information("Installing TypeSpec dependencies...");
            Log.Information("  Directory: {Dir}", TypeSpecDirectory);

            // Prefer npm ci for deterministic installs in CI
            if ((TypeSpecDirectory / "package-lock.json").FileExists())
            {
                NpmTasks.NpmCi(s => s
                    .SetProcessWorkingDirectory(TypeSpecDirectory));
            }
            else
            {
                NpmTasks.NpmInstall(s => s
                    .SetProcessWorkingDirectory(TypeSpecDirectory));
            }

            Log.Information("TypeSpec dependencies installed");
        });

    // ════════════════════════════════════════════════════════════════════════
    // TypeSpec → OpenAPI Compilation
    // ════════════════════════════════════════════════════════════════════════

    Target TypeSpecCompile => d => d
        .Description("Compile TypeSpec → OpenAPI 3.1 + JSON Schema")
        .DependsOn<ITypeSpec>(x => x.TypeSpecInstall)
        .OnlyWhenStatic(() => TypeSpecEntry.FileExists())
        .Produces(OpenApiOutput)
        .Produces(JsonSchemaOutput / "**/*.json")
        .Executes(() =>
        {
            Log.Information("Compiling TypeSpec...");
            Log.Information("  Entry:  {Entry}", TypeSpecEntry);
            Log.Information("  Config: {Config}", TypeSpecConfig);

            OpenApiDirectory.CreateDirectory();
            JsonSchemaOutput.CreateDirectory();

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory(TypeSpecDirectory)
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

    // ════════════════════════════════════════════════════════════════════════
    // Kiota Client Generation (Polyglot)
    // ════════════════════════════════════════════════════════════════════════

    Target GenerateDotNet => d => d
        .Description("Generate C# client via Kiota → core/primitives/dotnet")
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .OnlyWhenStatic(() => OpenApiOutput.FileExists())
        .Produces(GeneratedDotNet / "**/*.cs")
        .Executes(() =>
        {
            Log.Information("Generating C# client via Kiota...");

            GeneratedDotNet.CreateOrCleanDirectory();

            var args = BuildKiotaArgs("csharp", GeneratedDotNet)
                + $" --namespace-name {KiotaDotNetNamespace}";

            RunKiota(args);

            var fileCount = GeneratedDotNet.GlobFiles("**/*.cs").Count;
            Log.Information("C# client generated: {Output} ({Count} files)", GeneratedDotNet, fileCount);
        });

    Target GeneratePython => d => d
        .Description("Generate Python client via Kiota → core/primitives/python")
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .OnlyWhenStatic(() => OpenApiOutput.FileExists())
        .Produces(GeneratedPython / "**/*.py")
        .Executes(() =>
        {
            Log.Information("Generating Python client via Kiota...");

            GeneratedPython.CreateOrCleanDirectory();

            var args = BuildKiotaArgs("python", GeneratedPython);

            RunKiota(args);

            var fileCount = GeneratedPython.GlobFiles("**/*.py").Count;
            Log.Information("Python client generated: {Output} ({Count} files)", GeneratedPython, fileCount);
        });

    Target GenerateTypeScript => d => d
        .Description("Generate TypeScript client via Kiota → core/primitives/typescript")
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .OnlyWhenStatic(() => OpenApiOutput.FileExists())
        .Produces(GeneratedTypeScript / "**/*.ts")
        .Executes(() =>
        {
            Log.Information("Generating TypeScript client via Kiota...");

            GeneratedTypeScript.CreateOrCleanDirectory();

            var args = BuildKiotaArgs("typescript", GeneratedTypeScript);

            RunKiota(args);

            var fileCount = GeneratedTypeScript.GlobFiles("**/*.ts").Count;
            Log.Information("TypeScript client generated: {Output} ({Count} files)", GeneratedTypeScript, fileCount);
        });

    // ════════════════════════════════════════════════════════════════════════
    // Aggregate Generation (All Languages in Parallel)
    // ════════════════════════════════════════════════════════════════════════

    Target GenerateAll => d => d
        .Description("Generate all polyglot clients from TypeSpec (C#, Python, TypeScript)")
        .DependsOn<ITypeSpec>(x => x.GenerateDotNet)
        .DependsOn<ITypeSpec>(x => x.GeneratePython)
        .DependsOn<ITypeSpec>(x => x.GenerateTypeScript)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  All polyglot clients generated:");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  C#:         {Path}", GeneratedDotNet);
            Log.Information("  Python:     {Path}", GeneratedPython);
            Log.Information("  TypeScript: {Path}", GeneratedTypeScript);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Sync to Consumers (Dashboard)
    // ════════════════════════════════════════════════════════════════════════

    Target SyncDashboardTypes => d => d
        .Description("Copy generated TypeScript types to dashboard/src/types/generated")
        .DependsOn<ITypeSpec>(x => x.GenerateTypeScript)
        .OnlyWhenStatic(() => DashboardSrcDirectory.DirectoryExists())
        .Executes(() =>
        {
            Log.Information("Syncing TypeScript types to dashboard...");

            if (GeneratedTypeScript.DirectoryExists())
            {
                DashboardTypesDestination.CreateDirectory();
                GeneratedTypeScript.Copy(DashboardTypesDestination, ExistsPolicy.MergeAndOverwrite);
                Log.Information("  Synced: {Src} → {Dest}", GeneratedTypeScript, DashboardTypesDestination);
            }
            else
            {
                Log.Warning("Generated TypeScript not found at {Path}", GeneratedTypeScript);
            }
        });

    // ════════════════════════════════════════════════════════════════════════
    // Info & Diagnostics
    // ════════════════════════════════════════════════════════════════════════

    Target TypeSpecInfo => d => d
        .Description("Show TypeSpec/Kiota configuration and generation status")
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl TypeSpec → Kiota Configuration");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("");
            Log.Information("  Source (core/specs):");
            Log.Information("    TypeSpec Dir : {Path} ({Exists})",
                TypeSpecDirectory, Exists(TypeSpecDirectory));
            Log.Information("    Entry Point  : {Path} ({Exists})",
                TypeSpecEntry, Exists(TypeSpecEntry));
            Log.Information("    Config       : {Path} ({Exists})",
                TypeSpecConfig, Exists(TypeSpecConfig));
            Log.Information("");
            Log.Information("  Output (core/openapi):");
            Log.Information("    OpenAPI      : {Path} ({Exists})",
                OpenApiOutput, Exists(OpenApiOutput));
            Log.Information("    JSON Schema  : {Path} ({Exists})",
                JsonSchemaOutput, Exists(JsonSchemaOutput));
            Log.Information("");
            Log.Information("  Generated Primitives (core/primitives/):");
            Log.Information("    C#           : {Path} ({Count} files)",
                GeneratedDotNet, CountFiles(GeneratedDotNet, "**/*.cs"));
            Log.Information("    Python       : {Path} ({Count} files)",
                GeneratedPython, CountFiles(GeneratedPython, "**/*.py"));
            Log.Information("    TypeScript   : {Path} ({Count} files)",
                GeneratedTypeScript, CountFiles(GeneratedTypeScript, "**/*.ts"));
            Log.Information("");
            Log.Information("  Kiota Configuration:");
            Log.Information("    .NET Namespace : {Ns}", KiotaDotNetNamespace);
            Log.Information("    Client Name    : {Name}", KiotaClientName);
            if (!string.IsNullOrEmpty(KiotaIncludePatterns))
                Log.Information("    Include        : {Patterns}", KiotaIncludePatterns);
            if (!string.IsNullOrEmpty(KiotaExcludePatterns))
                Log.Information("    Exclude        : {Patterns}", KiotaExcludePatterns);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Clean
    // ════════════════════════════════════════════════════════════════════════

    Target TypeSpecClean => d => d
        .Description("Clean all generated artifacts (openapi, schemas, primitives)")
        .Executes(() =>
        {
            Log.Information("Cleaning generated artifacts...");

            AbsolutePath[] dirsToClean =
            [
                GeneratedDotNet,
                GeneratedPython,
                GeneratedTypeScript,
                JsonSchemaOutput,
                DashboardTypesDestination
            ];

            foreach (var dir in dirsToClean.Where(d => d.DirectoryExists()))
            {
                dir.DeleteDirectory();
                Log.Information("  Deleted: {Dir}", dir);
            }

            if (OpenApiOutput.FileExists())
            {
                OpenApiOutput.DeleteFile();
                Log.Information("  Deleted: {File}", OpenApiOutput);
            }

            Log.Information("Generated artifacts cleaned");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Private Helpers
    // ════════════════════════════════════════════════════════════════════════

    private string BuildKiotaArgs(string language, AbsolutePath output)
    {
        var args = $"generate --language {language} " +
                   $"--openapi \"{OpenApiOutput}\" " +
                   $"--output \"{output}\" " +
                   $"--class-name {KiotaClientName} " +
                   "--exclude-backward-compatible " +
                   "--clean-output";

        // Support fine-grained generation via include/exclude patterns
        if (!string.IsNullOrEmpty(KiotaIncludePatterns))
        {
            foreach (var pattern in KiotaIncludePatterns.Split(','))
            {
                args += $" --include-path \"{pattern.Trim()}\"";
            }
        }

        if (!string.IsNullOrEmpty(KiotaExcludePatterns))
        {
            foreach (var pattern in KiotaExcludePatterns.Split(','))
            {
                args += $" --exclude-path \"{pattern.Trim()}\"";
            }
        }

        return args;
    }

    private void RunKiota(string arguments)
    {
        var process = ProcessTasks.StartProcess(
            "kiota",
            arguments,
            RootDirectory,
            logOutput: true
        );
        process.AssertZeroExitCode();
    }

    private static string Exists(AbsolutePath path) =>
        path.FileExists() || path.DirectoryExists() ? "exists" : "MISSING";

    private static int CountFiles(AbsolutePath dir, string pattern) =>
        dir.DirectoryExists() ? dir.GlobFiles(pattern).Count : 0;
}
