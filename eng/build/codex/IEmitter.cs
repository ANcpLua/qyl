using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Components.Theory;

/// <summary>
///     Code generation emitter for qyl models, schemas, and TypeScript types.
///     Generates from the canonical model definitions:
///     - C# records (qyl.protocol/Models/*.cs)
///     - C# primitives (qyl.protocol/Primitives/*.cs)
///     - DuckDB schema SQL (core/generated/duckdb/schema.sql)
///     - DuckDB C# mappings (core/generated/duckdb/DuckDbSchema.g.cs)
///     - TypeScript types (core/generated/typescript/*.ts)
///     Usage:
///     nuke Emit              # Generate all
///     nuke EmitCSharp        # C# only
///     nuke EmitDuckDb        # DuckDB only
///     nuke EmitTypeScript    # TypeScript only
///     nuke EmitInfo          # Show configuration
/// </summary>
[ParameterPrefix(nameof(IEmitter))]
interface IEmitter : IHasSolution, IGenerationGuard
{
    // ════════════════════════════════════════════════════════════════════════
    // Output Paths
    // ════════════════════════════════════════════════════════════════════════

    AbsolutePath GeneratedDirectory => CoreDirectory / "generated";

    AbsolutePath GeneratedDuckDbDirectory => GeneratedDirectory / "duckdb";

    AbsolutePath GeneratedTypeScriptDirectory => GeneratedDirectory / "typescript";

    AbsolutePath GeneratedCSharpDirectory => GeneratedDirectory / "csharp";

    AbsolutePath ProtocolModelsDirectory => ProtocolDirectory / "Models";

    AbsolutePath ProtocolPrimitivesDirectory => ProtocolDirectory / "Primitives";

    AbsolutePath ProtocolAttributesDirectory => ProtocolDirectory / "Attributes";

    AbsolutePath ProtocolContractsDirectory => ProtocolDirectory / "Contracts";

    AbsolutePath CollectorStorageDirectory => CollectorDirectory / "Storage";

    // ════════════════════════════════════════════════════════════════════════
    // Parameters
    // ════════════════════════════════════════════════════════════════════════

    [Parameter("Root namespace for generated C# code")]
    string RootNamespace => TryGetValue(() => RootNamespace) ?? "Qyl.Protocol";

    [Parameter("Skip sync to consumer projects")] bool SkipSync => TryGetValue<bool?>(() => SkipSync) ?? false;

    // ════════════════════════════════════════════════════════════════════════
    // Schema Definition (Source of Truth)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     The canonical schema definition for all qyl models.
    ///     This is the SINGLE SOURCE OF TRUTH.
    /// </summary>
    static QylSchema Schema => QylSchema.Instance;

    // ════════════════════════════════════════════════════════════════════════
    // Aggregate Target
    // ════════════════════════════════════════════════════════════════════════

    Target Emit => d => d
        .Description("Generate all code from schema (C#, DuckDB, TypeScript)")
        .DependsOn<IEmitter>(x => x.EmitCSharp)
        .DependsOn<IEmitter>(x => x.EmitDuckDb)
        .DependsOn<IEmitter>(x => x.EmitTypeScript)
        .Executes(() =>
        {
            ((IGenerationGuard)this).LogSummary();

            Log.Information("");
            Log.Information("  Generated artifacts:");
            Log.Information("    C# Models:    {Path}", ProtocolModelsDirectory);
            Log.Information("    C# Primitives:{Path}", ProtocolPrimitivesDirectory);
            Log.Information("    DuckDB SQL:   {Path}", GeneratedDuckDbDirectory);
            Log.Information("    TypeScript:   {Path}", GeneratedTypeScriptDirectory);
        });

    // ════════════════════════════════════════════════════════════════════════
    // C# Generation
    // ════════════════════════════════════════════════════════════════════════

    Target EmitCSharp => d => d
        .Description("Generate C# records and primitives")
        .Produces(ProtocolModelsDirectory / "*.g.cs")
        .Produces(ProtocolPrimitivesDirectory / "*.g.cs")
        .Produces(ProtocolAttributesDirectory / "*.g.cs")
        .Executes(() =>
        {
            Log.Information("Generating C# models...");

            ProtocolModelsDirectory.CreateDirectory();
            ProtocolPrimitivesDirectory.CreateDirectory();
            ProtocolAttributesDirectory.CreateDirectory();

            var guard = (IGenerationGuard)this;

            // Generate primitives
            foreach (var primitive in Schema.Primitives)
            {
                var path = ProtocolPrimitivesDirectory / $"{primitive.Name}.g.cs";
                var content = CSharpEmitter.EmitPrimitive(primitive, RootNamespace);

                var decision = guard.ShouldGenerateWithContent(path, content, $"Primitive {primitive.Name}");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, primitive.Name);
                }
            }

            // Generate models
            foreach (var model in Schema.Models)
            {
                var path = ProtocolModelsDirectory / $"{model.Name}.g.cs";
                var content = CSharpEmitter.EmitModel(model, RootNamespace);

                var decision = guard.ShouldGenerateWithContent(path, content, $"Model {model.Name}");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, model.Name);
                }
            }

            // Generate GenAiAttributes constants
            {
                var path = ProtocolAttributesDirectory / "GenAiAttributes.g.cs";
                var content = CSharpEmitter.EmitGenAiAttributes(Schema.GenAiAttributes, RootNamespace);

                var decision = guard.ShouldGenerateWithContent(path, content, "GenAiAttributes");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, "GenAiAttributes");
                }
            }

            Log.Information("C# generation complete");
        });

    // ════════════════════════════════════════════════════════════════════════
    // DuckDB Generation
    // ════════════════════════════════════════════════════════════════════════

    Target EmitDuckDb => d => d
        .Description("Generate DuckDB schema SQL and C# mappings")
        .Produces(GeneratedDuckDbDirectory / "schema.sql")
        .Produces(GeneratedDuckDbDirectory / "DuckDbSchema.g.cs")
        .Executes(() =>
        {
            Log.Information("Generating DuckDB schema...");

            GeneratedDuckDbDirectory.CreateDirectory();

            var guard = (IGenerationGuard)this;

            // Generate SQL schema
            {
                var path = GeneratedDuckDbDirectory / "schema.sql";
                var content = DuckDbEmitter.EmitSql(Schema);

                var decision = guard.ShouldGenerateWithContent(path, content, "DuckDB SQL Schema");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, "schema.sql");
                }
            }

            // Generate C# schema mappings
            {
                var path = GeneratedDuckDbDirectory / "DuckDbSchema.g.cs";
                var content = DuckDbEmitter.EmitCSharpMappings(Schema, RootNamespace);

                var decision = guard.ShouldGenerateWithContent(path, content, "DuckDB C# Mappings");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, "DuckDbSchema.g.cs");
                }
            }

            Log.Information("DuckDB generation complete");
        });

    // ════════════════════════════════════════════════════════════════════════
    // TypeScript Generation
    // ════════════════════════════════════════════════════════════════════════

    Target EmitTypeScript => d => d
        .Description("Generate TypeScript types for dashboard")
        .Produces(GeneratedTypeScriptDirectory / "*.ts")
        .Executes(() =>
        {
            Log.Information("Generating TypeScript types...");

            GeneratedTypeScriptDirectory.CreateDirectory();

            var guard = (IGenerationGuard)this;

            // Generate models.ts
            {
                var path = GeneratedTypeScriptDirectory / "models.ts";
                var content = TypeScriptEmitter.EmitModels(Schema);

                var decision = guard.ShouldGenerateWithContent(path, content, "TypeScript Models");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, "models.ts");
                }
            }

            // Generate api-types.ts
            {
                var path = GeneratedTypeScriptDirectory / "api-types.ts";
                var content = TypeScriptEmitter.EmitApiTypes(Schema);

                var decision = guard.ShouldGenerateWithContent(path, content, "TypeScript API Types");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, "api-types.ts");
                }
            }

            // Generate index.ts barrel export
            {
                var path = GeneratedTypeScriptDirectory / "index.ts";
                var content = TypeScriptEmitter.EmitIndex();

                var decision = guard.ShouldGenerateWithContent(path, content, "TypeScript Index");
                if (decision is GenerationDecision.Generate or GenerationDecision.Update or GenerationDecision.Overwrite
                    or GenerationDecision.OverwriteAll)
                {
                    File.WriteAllText(path, content);
                    guard.LogGenerated(path, "index.ts");
                }
            }

            Log.Information("TypeScript generation complete");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Sync to Consumers
    // ════════════════════════════════════════════════════════════════════════

    Target SyncGeneratedTypes => d => d
        .Description("Sync generated types to consumer projects")
        .DependsOn<IEmitter>(x => x.Emit)
        .OnlyWhenStatic(() => !SkipSync)
        .Executes(() =>
        {
            Log.Information("Syncing generated types to consumers...");

            // Sync DuckDB schema to collector
            var collectorStorageDest = CollectorStorageDirectory / "DuckDbSchema.g.cs";
            var duckDbSchemaSource = GeneratedDuckDbDirectory / "DuckDbSchema.g.cs";

            if (duckDbSchemaSource.FileExists())
            {
                CollectorStorageDirectory.CreateDirectory();
                duckDbSchemaSource.Copy(collectorStorageDest, ExistsPolicy.FileOverwrite);
                Log.Information("  Synced: {Src} → {Dest}", duckDbSchemaSource.Name, collectorStorageDest);
            }

            // Sync TypeScript to dashboard
            var dashboardTypesDest = DashboardSrcDirectory / "types" / "generated";

            if (GeneratedTypeScriptDirectory.DirectoryExists())
            {
                dashboardTypesDest.CreateDirectory();
                GeneratedTypeScriptDirectory.Copy(dashboardTypesDest, ExistsPolicy.MergeAndOverwrite);
                Log.Information("  Synced: {Src} → {Dest}", GeneratedTypeScriptDirectory, dashboardTypesDest);
            }

            Log.Information("Sync complete");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Info & Diagnostics
    // ════════════════════════════════════════════════════════════════════════

    Target EmitInfo => d => d
        .Description("Show emitter configuration and schema summary")
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  qyl Code Generation Emitter");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("");
            Log.Information("  Schema Summary:");
            Log.Information("    Primitives:    {Count}", Schema.Primitives.Count);
            Log.Information("    Models:        {Count}", Schema.Models.Count);
            Log.Information("    Tables:        {Count}", Schema.Tables.Count);
            Log.Information("    GenAI Attrs:   {Count}", Schema.GenAiAttributes.Count);
            Log.Information("");
            Log.Information("  Output Paths:");
            Log.Information("    C# Models:     {Path}", ProtocolModelsDirectory);
            Log.Information("    C# Primitives: {Path}", ProtocolPrimitivesDirectory);
            Log.Information("    DuckDB SQL:    {Path}", GeneratedDuckDbDirectory / "schema.sql");
            Log.Information("    DuckDB C#:     {Path}", GeneratedDuckDbDirectory / "DuckDbSchema.g.cs");
            Log.Information("    TypeScript:    {Path}", GeneratedTypeScriptDirectory);
            Log.Information("");
            Log.Information("  Sync Destinations:");
            Log.Information("    Collector:     {Path}", CollectorStorageDirectory);
            Log.Information("    Dashboard:     {Path}", DashboardSrcDirectory / "types" / "generated");
            Log.Information("");
            Log.Information("  Configuration:");
            Log.Information("    RootNamespace: {Ns}", RootNamespace);
            Log.Information("    SkipSync:      {Skip}", SkipSync);
            Log.Information("    Force:         {Force}", Force);
            Log.Information("    DryRun:        {DryRun}", DryRun);
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    // ════════════════════════════════════════════════════════════════════════
    // Clean
    // ════════════════════════════════════════════════════════════════════════

    Target EmitClean => d => d
        .Description("Clean all generated artifacts")
        .Executes(() =>
        {
            Log.Information("Cleaning generated artifacts...");

            AbsolutePath[] dirsToClean =
            [
                GeneratedDuckDbDirectory,
                GeneratedTypeScriptDirectory,
                GeneratedCSharpDirectory
            ];

            foreach (var dir in dirsToClean.Where(d => d.DirectoryExists()))
            {
                dir.DeleteDirectory();
                Log.Information("  Deleted: {Dir}", dir);
            }

            // Clean generated files in protocol project
            var generatedFiles = new[]
                {
                    ProtocolModelsDirectory,
                    ProtocolPrimitivesDirectory,
                    ProtocolAttributesDirectory
                }
                .Where(d => d.DirectoryExists())
                .SelectMany(d => d.GlobFiles("*.g.cs"));

            foreach (var file in generatedFiles)
            {
                file.DeleteFile();
                Log.Information("  Deleted: {File}", file);
            }

            Log.Information("Clean complete");
        });
}