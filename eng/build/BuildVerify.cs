


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ANcpLua.Roslyn.Utilities.Testing.MSBuild;
using DuckDB.NET.Data;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.Npm;
using Serilog;

namespace Qyl.Build;

[ParameterPrefix(nameof(IVerify))]
interface IVerify : IHazSourcePaths
{
    [Parameter("Skip verification after generation")]
    bool? SkipVerify => TryGetValue<bool?>(() => SkipVerify);


    Target VerifyGeneratedCode => d => d
        .Unlisted()
        .Description("Verify generated C# code compiles")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(async () =>
        {
            var paths = CodegenPaths.From(this);
            var generatedFiles = paths.CollectorStorage.GlobFiles("*.g.cs")
                .Where(f => !f.ToString().Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .ToList();

            if (generatedFiles.Count is 0)
            {
                Log.Warning("No generated files found to verify");
                return;
            }

            Log.Information("Compiling {Count} generated files in isolation...", generatedFiles.Count);

            await using var builder = new ProjectBuilder();
            builder
                .WithTargetFramework(Tfm.Net100)
                .WithOutputType(Val.Library)
                .WithProperty(Prop.Nullable, Val.Enable)
                .WithProperty(Prop.ImplicitUsings, Val.Enable);

            foreach (var file in generatedFiles)
            {
                var relativePath = RootDirectory.GetRelativePathTo(file).ToString();
                builder.AddSource(relativePath, await File.ReadAllTextAsync(file));
            }

            var result = await builder.BuildAsync();

            if (result.Failed)
            {
                foreach (var error in result.GetErrors())
                    Log.Error("  {Error}", error);
                throw new InvalidOperationException(
                    $"Generated code compilation failed with {result.GetErrors().Count()} error(s)");
            }

            Log.Information("Generated code compilation: PASSED ({Count} files)", generatedFiles.Count);
        });

    Target VerifyDuckDbSchema => d => d
        .Unlisted()
        .Description("Verify generated DuckDB schema is valid")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(async () =>
        {
            var paths = CodegenPaths.From(this);
            var schemaFile = paths.CollectorStorage / "DuckDbSchema.g.sql";

            if (!schemaFile.FileExists())
            {
                Log.Warning("DuckDbSchema.g.sql not found, skipping DuckDB verification");
                return;
            }

            var sql = await File.ReadAllTextAsync(schemaFile);

            await using var connection = new DuckDBConnection("DataSource=:memory:");
            connection.Open();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }

            var tables = new List<string>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'main'";
                await using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    tables.Add(reader.GetString(0));
            }

            Log.Information("DuckDB schema validation: PASSED ({Count} tables: {Tables})",
                tables.Count, string.Join(", ", tables));
        });

    Target VerifyFrontendTypes => d => d
        .Unlisted()
        .Description("Verify frontend TypeScript types compile")
        .DependsOn<IPipeline>(static x => x.FrontendInstall)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            Log.Information("Verifying frontend TypeScript types...");

            NpmTasks.NpmRun(s => s
                .SetProcessWorkingDirectory<NpmRunSettings>(DashboardDirectory)
                .SetCommand("typecheck"));

            Log.Information("Frontend TypeScript types: VALID");
        });

    Target VerifyGeneratedFilesClean => d => d
        .Unlisted()
        .Description("CI gate: verify committed generated files match HEAD")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            Log.Information("Checking for uncommitted generated file changes...");

            var diffOutput = GitTasks.Git(
                "diff --name-only",
                RootDirectory, logOutput: false, logInvocation: false);

            string[] activeGeneratedPrefixes =
            [
                "services/qyl.collector/Storage/"
            ];

            var dirtyFiles = diffOutput
                .Select(static o => o.Text.Trim())
                .Where(static f => f.Length > 0)
                .Where(f =>
                    activeGeneratedPrefixes.Any(p => f.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                    (f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
                     f.EndsWith(".g.sql", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (dirtyFiles.Count is 0)
            {
                Log.Information("All generated files match HEAD");
                return;
            }

            foreach (var file in dirtyFiles)
                Log.Error("  Generated file changed: {File}", file);

            throw new InvalidOperationException(
                $"{dirtyFiles.Count} committed generated file(s) have uncommitted changes. " +
                "Run the owning generator and commit the output.");
        });

    Target VerifyCollectorPublicApiIsExplicit => d => d
        .Unlisted()
        .Description("Verify collector does not publish source-generator discovered endpoint modules")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var files = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static f =>
                {
                    var path = f.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                });

            var offenders = files
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .Where(static file => IsForbiddenEndpointMapper(file.File, file.Text))
                .Select(static file => file.File)
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector public API is explicitly mapped");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Generated endpoint mapper call: {File}", offender);

            throw new InvalidOperationException(
                "Do not publish collector-local endpoint modules through QylMapEndpoints, MapQylGeneratedEndpoints, " +
                "or standalone Map*Endpoint extension methods. Expose contract-backed routes explicitly from CollectorEndpointExtensions.");

            static bool IsForbiddenEndpointMapper(string relativePath, string text)
            {
                var normalizedPath = relativePath.Replace('\\', '/');
                if (normalizedPath.EndsWith("services/qyl.collector/Hosting/CollectorEndpointExtensions.cs", StringComparison.Ordinal))
                    return false;

                return text.Contains("MapQylGeneratedEndpoints(", StringComparison.Ordinal)
                       || text.Contains("[QylMapEndpoints", StringComparison.Ordinal)
                       || text
                           .Split('\n')
                           .Select(static line => line.Trim())
                           .Any(static line =>
                               line.StartsWith("public static ", StringComparison.Ordinal)
                               && line.Contains(" Map", StringComparison.Ordinal)
                               && line.Contains("Endpoint", StringComparison.Ordinal)
                               && line.Contains('('));
            }
        });

    Target Verify => d => d
        .Description("Run all generated code verification checks")
        .DependsOn(VerifyGeneratedCode)
        .DependsOn(VerifyDuckDbSchema)
        .DependsOn(VerifyFrontendTypes)
        .DependsOn(VerifyGeneratedFilesClean)
        .DependsOn(VerifyCollectorPublicApiIsExplicit)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Verification Complete");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Generated C# code compiles");
            Log.Information("  DuckDB schema is valid");
            Log.Information("  Frontend TypeScript types compile");
            Log.Information("  Generated files match HEAD");
            Log.Information("  Collector public API is explicitly mapped");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });
}
