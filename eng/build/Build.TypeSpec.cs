using System.IO;
using System.Linq;
using Components;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

[ParameterPrefix(nameof(ITypeSpec))]
interface ITypeSpec : IHasSolution
{
    AbsolutePath TypeSpecDirectory => RootDirectory / "core" / "specs";

    AbsolutePath TypeSpecEntry => TypeSpecDirectory / "main.tsp";

    AbsolutePath TypeSpecConfig => TypeSpecDirectory / "tspconfig.yaml";

    AbsolutePath OpenApiOutput => RootDirectory / "core" / "openapi" / "openapi.yaml";

    AbsolutePath JsonSchemaOutput => RootDirectory / "core" / "schemas";

    AbsolutePath GeneratedCSharp => RootDirectory / "core" / "generated" / "dotnet";

    AbsolutePath GeneratedPython => RootDirectory / "core" / "generated" / "python";

    AbsolutePath GeneratedTypeScript => RootDirectory / "core" / "generated" / "typescript";

    AbsolutePath DashboardTypesDestination => DashboardDirectory / "src" / "types" / "generated";

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

            OpenApiOutput.Parent.CreateDirectory();
            JsonSchemaOutput.CreateDirectory();

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(TypeSpecDirectory)
                .SetCommand("compile"));

            if (OpenApiOutput.FileExists())
            {
                var size = new FileInfo(OpenApiOutput).Length;
                Log.Information("OpenAPI generated: {Output} ({Size:N0} bytes)", OpenApiOutput, size);
            }
            else
                Log.Warning("OpenAPI output not found at {Output}", OpenApiOutput);
        });

    Target GenerateCSharp => d => d
        .Description("Generate C# client (Kiota)")
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .OnlyWhenStatic(() => OpenApiOutput.FileExists())
        .Produces(GeneratedCSharp / "**/*.cs")
        .Executes(() =>
        {
            Log.Information("Generating C# client via Kiota...");

            GeneratedCSharp.CreateOrCleanDirectory();

            var process = ProcessTasks.StartProcess(
                "kiota",
                "generate " +
                "--language csharp " +
                $"--openapi \"{OpenApiOutput}\" " +
                $"--output \"{GeneratedCSharp}\" " +
                "--namespace-name Qyl.Core " +
                "--class-name QylClient " +
                "--exclude-backward-compatible " +
                "--clean-output",
                RootDirectory,
                logOutput: true
            );
            process.AssertZeroExitCode();

            var fileCount = GeneratedCSharp.GlobFiles("**/*.cs").Count;
            Log.Information("C# client generated: {Output} ({Count} files)", GeneratedCSharp, fileCount);
        });

    Target GeneratePython => d => d
        .Description("Generate Python client (Kiota)")
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .OnlyWhenStatic(() => OpenApiOutput.FileExists())
        .Produces(GeneratedPython / "**/*.py")
        .Executes(() =>
        {
            Log.Information("Generating Python client via Kiota...");

            GeneratedPython.CreateOrCleanDirectory();

            var process = ProcessTasks.StartProcess(
                "kiota",
                "generate " +
                "--language python " +
                $"--openapi \"{OpenApiOutput}\" " +
                $"--output \"{GeneratedPython}\" " +
                "--class-name QylClient " +
                "--exclude-backward-compatible " +
                "--clean-output",
                RootDirectory,
                logOutput: true
            );
            process.AssertZeroExitCode();

            var fileCount = GeneratedPython.GlobFiles("**/*.py").Count;
            Log.Information("Python client generated: {Output} ({Count} files)", GeneratedPython, fileCount);
        });

    Target GenerateTypeScript => d => d
        .Description("Generate TypeScript client (Kiota)")
        .DependsOn<ITypeSpec>(x => x.TypeSpecCompile)
        .OnlyWhenStatic(() => OpenApiOutput.FileExists())
        .Produces(GeneratedTypeScript / "**/*.ts")
        .Executes(() =>
        {
            Log.Information("Generating TypeScript client via Kiota...");

            GeneratedTypeScript.CreateOrCleanDirectory();

            var process = ProcessTasks.StartProcess(
                "kiota",
                "generate " +
                "--language typescript " +
                $"--openapi \"{OpenApiOutput}\" " +
                $"--output \"{GeneratedTypeScript}\" " +
                "--class-name QylClient " +
                "--exclude-backward-compatible " +
                "--clean-output",
                RootDirectory,
                logOutput: true
            );
            process.AssertZeroExitCode();

            var fileCount = GeneratedTypeScript.GlobFiles("**/*.ts").Count;
            Log.Information("TypeScript client generated: {Output} ({Count} files)", GeneratedTypeScript, fileCount);
        });

    Target GenerateAll => d => d
        .Description("Generate all clients from TypeSpec")
        .DependsOn<ITypeSpec>(x => x.GenerateCSharp)
        .DependsOn<ITypeSpec>(x => x.GeneratePython)
        .DependsOn<ITypeSpec>(x => x.GenerateTypeScript)
        .Executes(() =>
        {
            Log.Information("All clients generated:");
            Log.Information("  C#:         {Path}", GeneratedCSharp);
            Log.Information("  Python:     {Path}", GeneratedPython);
            Log.Information("  TypeScript: {Path}", GeneratedTypeScript);
        });

    Target SyncGeneratedTypes => d => d
        .Description("Copy generated TypeScript types to dashboard")
        .DependsOn<ITypeSpec>(x => x.GenerateAll)
        .Executes(() =>
        {
            Log.Information("Syncing generated types...");

            if (GeneratedTypeScript.DirectoryExists())
            {
                DashboardTypesDestination.CreateDirectory();
                GeneratedTypeScript.Copy(DashboardTypesDestination, ExistsPolicy.MergeAndOverwrite);
                Log.Information("  TypeScript → {Dest}", DashboardTypesDestination);
            }

            Log.Information("  C# client:     {Path}", GeneratedCSharp);
            Log.Information("  Python client: {Path}", GeneratedPython);
        });

    Target TypeSpecInfo => d => d
        .Description("Show TypeSpec configuration and status")
        .Executes(() =>
        {
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  qyl TypeSpec Configuration");
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  Source:");
            Log.Information("    TypeSpec Dir : {Path} ({Exists})",
                TypeSpecDirectory, TypeSpecDirectory.DirectoryExists() ? "exists" : "MISSING");
            Log.Information("    Entry Point  : {Path} ({Exists})",
                TypeSpecEntry, TypeSpecEntry.FileExists() ? "exists" : "MISSING");
            Log.Information("    Config       : {Path} ({Exists})",
                TypeSpecConfig, TypeSpecConfig.FileExists() ? "exists" : "MISSING");
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  Output:");
            Log.Information("    OpenAPI      : {Path} ({Exists})",
                OpenApiOutput, OpenApiOutput.FileExists() ? "exists" : "not generated");
            Log.Information("    JSON Schema  : {Path} ({Exists})",
                JsonSchemaOutput, JsonSchemaOutput.DirectoryExists() ? "exists" : "not generated");
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  Generated Clients:");
            Log.Information("    C#           : {Path} ({Count} files)",
                GeneratedCSharp,
                GeneratedCSharp.DirectoryExists() ? GeneratedCSharp.GlobFiles("**/*.cs").Count : 0);
            Log.Information("    Python       : {Path} ({Count} files)",
                GeneratedPython,
                GeneratedPython.DirectoryExists() ? GeneratedPython.GlobFiles("**/*.py").Count : 0);
            Log.Information("    TypeScript   : {Path} ({Count} files)",
                GeneratedTypeScript,
                GeneratedTypeScript.DirectoryExists() ? GeneratedTypeScript.GlobFiles("**/*.ts").Count : 0);
            Log.Information("══════════════════════════════════════════════════════════════");
        });

    Target TypeSpecClean => d => d
        .Description("Clean all generated code and artifacts")
        .Executes(() =>
        {
            Log.Information("Cleaning generated artifacts...");

            AbsolutePath[] dirs =
            [
                GeneratedCSharp,
                GeneratedPython,
                GeneratedTypeScript,
                JsonSchemaOutput,
                DashboardTypesDestination
            ];

            foreach (var dir in dirs.Where(d => d.DirectoryExists()))
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
}