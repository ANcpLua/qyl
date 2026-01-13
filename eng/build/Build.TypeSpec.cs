using System.IO;
using Components;
using Context;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

/// <summary>
///     TypeSpec compilation for API contract generation.
///     Generates OpenAPI 3.1 spec from TypeSpec God Schema.
///     Dashboard types are generated separately via openapi-typescript (npm run generate:types).
/// </summary>
[ParameterPrefix(nameof(ITypeSpec))]
interface ITypeSpec : IHasSolution
{
    /// <summary>
    ///     TypeSpec source directory (God Schema - Single Source of Truth).
    /// </summary>
    AbsolutePath TypeSpecDirectory => RootDirectory / "schema";

    AbsolutePath TypeSpecEntry => TypeSpecDirectory / "main.tsp";

    AbsolutePath TypeSpecConfig => TypeSpecDirectory / "tspconfig.yaml";

    AbsolutePath TypeSpecGenerated => TypeSpecDirectory / "generated";

    AbsolutePath OpenApiOutput => TypeSpecGenerated / "openapi.yaml";

    AbsolutePath JsonSchemaOutput => TypeSpecGenerated;

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
        .DependsOn<ITypeSpec>(static x => x.TypeSpecInstall)
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
                Log.Information("");
                Log.Information("Next steps:");
                Log.Information("  C#/DuckDB: nuke Generate");
                Log.Information("  Dashboard: cd src/qyl.dashboard && npm run generate:types");
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
            var paths = BuildPaths.From(this);

            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  qyl TypeSpec God Schema Configuration");
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  Source (God Schema):");
            Log.Information("    TypeSpec Dir : {Path} ({Exists})",
                TypeSpecDirectory, TypeSpecDirectory.DirectoryExists() ? "exists" : "MISSING");
            Log.Information("    Entry Point  : {Path} ({Exists})",
                TypeSpecEntry, TypeSpecEntry.FileExists() ? "exists" : "MISSING");
            Log.Information("    Config       : {Path} ({Exists})",
                TypeSpecConfig, TypeSpecConfig.FileExists() ? "exists" : "MISSING");
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  Generated Output:");
            Log.Information("    Generated Dir: {Path} ({Exists})",
                TypeSpecGenerated, TypeSpecGenerated.DirectoryExists() ? "exists" : "not generated");
            Log.Information("    OpenAPI      : {Path} ({Exists})",
                OpenApiOutput, OpenApiOutput.FileExists() ? "exists" : "not generated");
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  Generation Flow:");
            Log.Information("    schema/*.tsp → schema/generated/openapi.yaml → C#/DuckDB/TypeScript");
            Log.Information("══════════════════════════════════════════════════════════════");
            Log.Information("  Usage:");
            Log.Information("    nuke TypeSpecCompile    # Generate OpenAPI from God Schema");
            Log.Information("    nuke Generate           # Generate C#/DuckDB from OpenAPI");
            Log.Information("    npm run generate:types  # Generate dashboard TS from OpenAPI");
            Log.Information("══════════════════════════════════════════════════════════════");
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
}