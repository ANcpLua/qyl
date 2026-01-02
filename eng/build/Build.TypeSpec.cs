using System.IO;
using Components;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

/// <summary>
///     TypeSpec compilation for API contract generation.
///     Generates OpenAPI 3.1 spec from TypeSpec source.
///     Dashboard types are generated separately via openapi-typescript (npm run generate:types).
/// </summary>
[ParameterPrefix(nameof(ITypeSpec))]
interface ITypeSpec : IHasSolution
{
    AbsolutePath TypeSpecDirectory => RootDirectory / "core" / "specs";

    AbsolutePath TypeSpecEntry => TypeSpecDirectory / "main.tsp";

    AbsolutePath TypeSpecConfig => TypeSpecDirectory / "tspconfig.yaml";

    AbsolutePath OpenApiOutput => RootDirectory / "core" / "openapi" / "openapi.yaml";

    AbsolutePath JsonSchemaOutput => RootDirectory / "core" / "schemas";

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
                Log.Information("");
                Log.Information("Next steps:");
                Log.Information("  Dashboard types: cd src/qyl.dashboard && npm run generate:types");
            }
            else
                Log.Warning("OpenAPI output not found at {Output}", OpenApiOutput);
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
            Log.Information("  Usage:");
            Log.Information("    nuke TypeSpecCompile           # Generate OpenAPI from TypeSpec");
            Log.Information("    npm run generate:types         # Generate dashboard types from OpenAPI");
            Log.Information("══════════════════════════════════════════════════════════════");
        });

    Target TypeSpecClean => d => d
        .Description("Clean TypeSpec generated artifacts")
        .Executes(() =>
        {
            Log.Information("Cleaning TypeSpec artifacts...");

            if (JsonSchemaOutput.DirectoryExists())
            {
                JsonSchemaOutput.DeleteDirectory();
                Log.Information("  Deleted: {Dir}", JsonSchemaOutput);
            }

            if (OpenApiOutput.FileExists())
            {
                OpenApiOutput.DeleteFile();
                Log.Information("  Deleted: {File}", OpenApiOutput);
            }

            Log.Information("TypeSpec artifacts cleaned");
        });
}
