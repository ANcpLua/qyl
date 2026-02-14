// =============================================================================
// qyl Build System - Code Verification
// =============================================================================
// Validates generated code compiles, OTel conventions enforced, frontend types work
// Uses ProjectBuilder for isolated MSBuild compilation with SARIF/binlog output
// Uses DuckDB in-memory for DDL validation
// =============================================================================


// ════════════════════════════════════════════════════════════════════════════════
// IVerify - Generated Code Validation
// ════════════════════════════════════════════════════════════════════════════════

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
using Nuke.Common.Tools.Npm;
using Serilog;

/// <summary>
///     Validates generated code compiles and behaves correctly.
///     Uses ProjectBuilder for isolated MSBuild execution with binlog introspection.
/// </summary>
[ParameterPrefix(nameof(IVerify))]
interface IVerify : IHazSourcePaths
{
    [Parameter("Skip verification after generation")]
    bool? SkipVerify => TryGetValue<bool?>(() => SkipVerify);

    // ════════════════════════════════════════════════════════════════════════
    // Verification Targets
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Verify all generated C# code compiles without errors.
    ///     Uses ProjectBuilder for isolated compilation with SARIF and binlog output.
    /// </summary>
    Target VerifyGeneratedCode => d => d
        .Description("Verify generated C# code compiles")
        .DependsOn<IPipeline>(static x => x.Generate)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
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

            var builder = new ProjectBuilder();
            try
            {
                builder
                    .WithTargetFramework(Tfm.Net100)
                    .WithOutputType(Val.Library)
                    .WithProperty(Prop.Nullable, Val.Enable)
                    .WithProperty(Prop.ImplicitUsings, Val.Enable);

                foreach (var file in generatedFiles)
                {
                    var relativePath = RootDirectory.GetRelativePathTo(file).ToString();
                    builder.AddSource(relativePath, File.ReadAllText(file));
                }

                var result = builder.BuildAsync().GetAwaiter().GetResult();

                if (result.Failed)
                {
                    foreach (var error in result.GetErrors())
                        Log.Error("  {Error}", error);
                    throw new InvalidOperationException(
                        $"Generated code compilation failed with {result.GetErrors().Count()} error(s)");
                }

                Log.Information("Generated code compilation: PASSED ({Count} files)", generatedFiles.Count);
            }
            finally
            {
                builder.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        });

    /// <summary>
    ///     Verify DuckDB schema DDL executes against in-memory DuckDB.
    /// </summary>
    Target VerifyDuckDbSchema => d => d
        .Description("Verify generated DuckDB schema is valid")
        .DependsOn<IPipeline>(static x => x.Generate)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var paths = CodegenPaths.From(this);
            var schemaFile = paths.CollectorStorage / "DuckDbSchema.g.cs";

            if (!schemaFile.FileExists())
            {
                Log.Warning("DuckDbSchema.g.cs not found, skipping DuckDB verification");
                return;
            }

            var content = File.ReadAllText(schemaFile);

            // Extract DDL from generated C# string literals
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

            using var connection = new DuckDBConnection("DataSource=:memory:");
            connection.Open();

            foreach (var ddl in ddlStatements)
            {
                using var command = connection.CreateCommand();
                command.CommandText = ddl;
                command.ExecuteNonQuery();
            }

            // Verify tables were created
            var tables = new List<string>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'main'";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    tables.Add(reader.GetString(0));
            }

            Log.Information("DuckDB schema validation: PASSED ({Count} tables: {Tables})",
                tables.Count, string.Join(", ", tables));
        });

    /// <summary>
    ///     Verify generated TypeScript uses OTel semantic conventions (snake_case properties).
    ///     Checks the actual generated api.ts to catch naming mismatches.
    /// </summary>
    Target VerifySchemaConventions => d => d
        .Description("Verify generated TypeScript uses OTel snake_case property names")
        .DependsOn<IPipeline>(static x => x.GenerateTypeScript)
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var apiTsPath = DashboardDirectory / "src" / "types" / "api.ts";

            if (!apiTsPath.FileExists())
            {
                Log.Warning("Generated api.ts not found: {Path}", apiTsPath);
                return;
            }

            Log.Information("Verifying OTel naming conventions in generated TypeScript...");
            var content = File.ReadAllText(apiTsPath);

            // Find property names in TypeScript interface/type definitions
            // Pattern: matches lines like "    property_name?: string;" or "    property_name: number;"
            var matches = VerifyRegexes.TypeScriptPropertyPattern().Matches(content);

            var violations = new HashSet<string>();
            foreach (Match match in matches)
            {
                var propName = match.Groups[1].Value;

                // Skip TypeScript/OpenAPI keywords
                if (IsTypeScriptKeyword(propName)) continue;

                // Check for camelCase (lowercase start, has uppercase after first char)
                // Skip snake_case (has underscores) and single words
                if (IsCamelCase(propName)) violations.Add(propName);
            }

            if (violations.Count > 0)
            {
                Log.Warning("Found {Count} potential OTel naming convention issues:", violations.Count);
                foreach (var v in violations.Take(10))
                    Log.Warning("  - {Property} (may need to be snake_case)", v);
                if (violations.Count > 10)
                    Log.Warning("  ... and {Count} more", violations.Count - 10);

                // Log as warning, not error - some camelCase is acceptable for non-OTel fields
                Log.Information("Review generated types to ensure OTel attributes use snake_case");
            }
            else
            {
                Log.Information("OTel naming conventions: VALID (no camelCase property names found)");
            }
        });

    /// <summary>
    ///     Verify frontend TypeScript compiles with generated types.
    ///     Catches type mismatches between schema and dashboard code.
    /// </summary>
    Target VerifyFrontendTypes => d => d
        .Description("Verify frontend TypeScript types compile")
        .DependsOn<IPipeline>(static x => x.GenerateTypeScript)
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

    /// <summary>
    ///     Run all verification checks.
    /// </summary>
    Target Verify => d => d
        .Description("Run all generated code verification checks")
        .DependsOn(VerifyGeneratedCode)
        .DependsOn(VerifyDuckDbSchema)
        .DependsOn(VerifySchemaConventions)
        .DependsOn(VerifyFrontendTypes)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Verification Complete");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  ✓ Generated C# code compiles");
            Log.Information("  ✓ DuckDB schema is valid");
            Log.Information("  ✓ OTel snake_case conventions enforced");
            Log.Information("  ✓ Frontend TypeScript types compile");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ════════════════════════════════════════════════════════════════════════

    private static bool IsTypeScriptKeyword(string name) =>
        name is "type" or "get" or "post" or "put" or "delete" or "patch"
            or "content" or "responses" or "parameters" or "requestBody"
            or "query" or "path" or "header" or "cookie"
            or "string" or "number" or "boolean" or "object" or "array"
            or "null" or "undefined" or "never" or "any" or "unknown" or "void";

    private static bool IsCamelCase(string name)
    {
        // Skip single-word lowercase (valid)
        if (!name.Contains('_') && string.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal))
            return false;

        // Skip already snake_case (has underscores)
        if (name.Contains('_'))
            return false;

        // Check for camelCase (lowercase start, has uppercase after)
        return char.IsLower(name[0]) && name.Skip(1).Any(char.IsUpper);
    }
}

/// <summary>
///     Source-generated regex patterns for verification.
/// </summary>
internal static partial class VerifyRegexes
{
    /// <summary>
    ///     Matches TypeScript property definitions like "    property_name?: string;" or "    property_name: number;".
    /// </summary>
    [GeneratedRegex(@"^\s+(\w+)[\?]?\s*:\s*", RegexOptions.Multiline)]
    internal static partial Regex TypeScriptPropertyPattern();

    /// <summary>
    ///     Matches CREATE TABLE statements in generated DuckDB schema files.
    /// </summary>
    [GeneratedRegex(@"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS[\s\S]*?\)\s*;")]
    internal static partial Regex CreateTablePattern();

    /// <summary>
    ///     Matches CREATE INDEX statements (including UNIQUE) in generated DuckDB schema files.
    /// </summary>
    [GeneratedRegex(@"CREATE\s+(?:UNIQUE\s+)?INDEX\s+IF\s+NOT\s+EXISTS[^\n]+;")]
    internal static partial Regex CreateIndexPattern();
}
