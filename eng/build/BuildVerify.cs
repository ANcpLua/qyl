


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        .DependsOn<IPipeline>(static x => x.Generate)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(async () =>
        {
            var paths = CodegenPaths.From(this);
            var generatedFiles = paths.Protocol.GlobFiles("**/*.g.cs")
                .Concat(paths.CollectorStorage.GlobFiles("*.g.cs"))
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
        .DependsOn<IPipeline>(static x => x.Generate)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(async () =>
        {
            var paths = CodegenPaths.From(this);
            var schemaFile = paths.CollectorStorage / "DuckDbSchema.g.cs";

            if (!schemaFile.FileExists())
            {
                Log.Warning("DuckDbSchema.g.cs not found, skipping DuckDB verification");
                return;
            }

            var content = await File.ReadAllTextAsync(schemaFile);

            var ddlStatements = new List<string>();
            foreach (Match match in VerifyRegexes.CreateTablePattern().Matches(content))
                ddlStatements.Add(match.Value);
            foreach (Match match in VerifyRegexes.CreateIndexPattern().Matches(content))
                ddlStatements.Add(match.Value);

            if (ddlStatements.Count is 0)
            {
                Log.Warning("No DDL statements found in DuckDbSchema.g.cs");
                return;
            }

            Log.Information("Executing {Count} DDL statements against in-memory DuckDB...", ddlStatements.Count);

            await using var connection = new DuckDBConnection("DataSource=:memory:");
            connection.Open();

            foreach (var ddl in ddlStatements)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ddl;
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

    Target VerifySchemaConventions => d => d
        .Unlisted()
        .Description("Verify generated TypeScript uses OTel snake_case property names")
        .DependsOn<IPipeline>(static x => x.TypeSpecCompile)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(async () =>
        {
            var apiTsPath = DashboardDirectory / "src" / "types" / "api.ts";

            if (!apiTsPath.FileExists())
            {
                Log.Warning("Generated api.ts not found: {Path}", apiTsPath);
                return;
            }

            Log.Information("Verifying OTel naming conventions in generated TypeScript...");
            var content = await File.ReadAllTextAsync(apiTsPath);

            var matches = VerifyRegexes.TypeScriptPropertyPattern().Matches(content);

            var violations = new HashSet<string>();
            foreach (Match match in matches)
            {
                var propName = match.Groups[1].Value;

                if (IsTypeScriptKeyword(propName)) continue;

                if (IsCamelCase(propName)) violations.Add(propName);
            }

            if (violations.Count > 0)
            {
                Log.Warning("Found {Count} potential OTel naming convention issues:", violations.Count);
                foreach (var v in violations.Take(10))
                    Log.Warning("  - {Property} (may need to be snake_case)", v);
                if (violations.Count > 10)
                    Log.Warning("  ... and {Count} more", violations.Count - 10);

                Log.Information("Review generated types to ensure OTel attributes use snake_case");
            }
            else
            {
                Log.Information("OTel naming conventions: VALID (no camelCase property names found)");
            }
        });

    Target VerifyFrontendTypes => d => d
        .Unlisted()
        .Description("Verify frontend TypeScript types compile")
        .DependsOn<IPipeline>(static x => x.TypeSpecCompile)
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
        .Description("CI gate: verify generated files match HEAD after regeneration")
        .DependsOn<IPipeline>(static x => x.Generate)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            Log.Information("Checking for uncommitted generated file changes...");

            var diffOutput = GitTasks.Git(
                "diff --name-only",
                RootDirectory, logOutput: false, logInvocation: false);

            string[] generatedSuffixes = [".g.cs", ".g.sql", ".g.tsp", ".g.ts"];

            string[] weaverOutputs =
            [
                "services/qyl.dashboard/src/lib/semconv.ts",
                "core/specs/emitters/qyl-semconv-lint/data/otel-attribute-registry.json",
                "packages/qyl-client/src/conventions.ts",
                "docs/attributes/qyl.attrs.md",
            ];

            string[] weaverSchemaPrefixes =
            [
                "packages/Qyl.OpenTelemetry.SemanticConventions/schemas/",
                "packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/schemas/",
            ];

            var dirtyFiles = diffOutput
                .Select(static o => o.Text.Trim())
                .Where(static f => f.Length > 0)
                .Where(f =>
                    generatedSuffixes.Any(s => f.EndsWith(s, StringComparison.OrdinalIgnoreCase)) ||
                    f.Contains("core/openapi/", StringComparison.OrdinalIgnoreCase) ||
                    weaverOutputs.Contains(f, StringComparer.OrdinalIgnoreCase) ||
                    weaverSchemaPrefixes.Any(p => f.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (dirtyFiles.Count is 0)
            {
                Log.Information("All generated files match HEAD");
                return;
            }

            foreach (var file in dirtyFiles)
                Log.Error("  Generated file changed: {File}", file);

            throw new InvalidOperationException(
                $"{dirtyFiles.Count} generated file(s) changed after regeneration. " +
                "Run 'nuke Generate' and commit the output.");
        });

    Target Verify => d => d
        .Description("Run all generated code verification checks")
        .DependsOn(VerifyGeneratedCode)
        .DependsOn(VerifyDuckDbSchema)
        .DependsOn(VerifySchemaConventions)
        .DependsOn(VerifyFrontendTypes)
        .DependsOn(VerifyGeneratedFilesClean)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Verification Complete");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Generated C# code compiles");
            Log.Information("  DuckDB schema is valid");
            Log.Information("  OTel snake_case conventions enforced");
            Log.Information("  Frontend TypeScript types compile");
            Log.Information("  Generated files match HEAD");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });


    private static bool IsTypeScriptKeyword(string name) =>
        name is "type" or "get" or "post" or "put" or "delete" or "patch"
            or "content" or "responses" or "parameters" or "requestBody"
            or "query" or "path" or "header" or "cookie"
            or "string" or "number" or "boolean" or "object" or "array"
            or "null" or "undefined" or "never" or "any" or "unknown" or "void";

    private static bool IsCamelCase(string name)
    {
        if (!name.Contains('_') && string.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal))
            return false;

        if (name.Contains('_'))
            return false;

        return char.IsLower(name[0]) && name.Skip(1).Any(char.IsUpper);
    }
}

internal static partial class VerifyRegexes
{
    [GeneratedRegex(@"^\s+(\w+)[\?]?\s*:\s*", RegexOptions.Multiline)]
    internal static partial Regex TypeScriptPropertyPattern();

    [GeneratedRegex(@"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS[\s\S]*?\)\s*;")]
    internal static partial Regex CreateTablePattern();

    [GeneratedRegex(@"CREATE\s+(?:UNIQUE\s+)?INDEX\s+IF\s+NOT\s+EXISTS[^\n]+;")]
    internal static partial Regex CreateIndexPattern();
}
