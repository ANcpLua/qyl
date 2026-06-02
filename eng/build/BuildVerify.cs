


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

            string[] activeGeneratedFiles =
            [
                "services/qyl.collector/Storage/DuckDbSchema.g.cs",
                "services/qyl.collector/Storage/DuckDbSchema.g.sql"
            ];

            var dirtyFiles = diffOutput
                .Select(static o => o.Text.Trim())
                .Where(static f => f.Length > 0)
                .Where(f => activeGeneratedFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
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

    Target VerifyCollectorUsesSemanticConstants => d => d
        .Unlisted()
        .Description("Verify collector semantic attribute keys flow through generated constants")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            string[] forbiddenSemanticLiterals =
            [
                "\"client.",
                "\"code.",
                "\"db.",
                "\"deployment.",
                "\"enduser.",
                "\"error.type\"",
                "\"exception.",
                "\"gen_ai.",
                "\"host.",
                "\"http.",
                "\"mcp.",
                "\"messaging.",
                "\"meter.name\"",
                "\"os.",
                "\"profile.",
                "\"otel.scope.",
                "\"qyl.capability.",
                "\"server.",
                "\"service.",
                "\"session.id\"",
                "\"url.",
                "\"user.id\""
            ];

            var offenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static f =>
                {
                    var path = f.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .Where(file => forbiddenSemanticLiterals.Any(token =>
                    file.Text.Contains(token, StringComparison.Ordinal)))
                .Select(static file => file.File)
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector semantic attribute keys use generated constants");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Raw semantic attribute literal: {File}", offender);

            throw new InvalidOperationException(
                "Do not hardcode semantic attribute keys in the collector. Use Qyl.OpenTelemetry.SemanticConventions* " +
                "or Qyl.Telemetry generated constants.");
        });

    Target VerifyCollectorMetricTagsAreBounded => d => d
        .Unlisted()
        .Description("Verify collector metric tag names stay bounded")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            string[] forbiddenMetricTagTokens =
            [
                "SemanticAttributeKeys.EnduserId",
                "SemanticAttributeKeys.GenAiAgentId",
                "SemanticAttributeKeys.GenAiAgentName",
                "SemanticAttributeKeys.GenAiConversationId",
                "SemanticAttributeKeys.GenAiRequestModel",
                "SemanticAttributeKeys.GenAiResponseModel",
                "SemanticAttributeKeys.GenAiToolCallId",
                "SemanticAttributeKeys.GenAiToolName",
                "SemanticAttributeKeys.McpSessionId",
                "SemanticAttributeKeys.SessionId",
                "SemanticAttributeKeys.UrlFull",
                "SemanticAttributeKeys.UrlPath",
                "SemanticAttributeKeys.UserId"
            ];

            var offenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static file =>
                {
                    var path = file.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => FindRegisterTagNameBlocks(file.Text)
                    .SelectMany(block => forbiddenMetricTagTokens
                        .Where(token => block.Contains(token, StringComparison.Ordinal))
                        .Select(token => (file.File, Token: token))))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector metric tag names are bounded");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Unbounded metric tag '{Token}' found in {File}", offender.Token, offender.File);

            throw new InvalidOperationException(
                "Do not register unbounded IDs, model names, tool names, URLs, or user identifiers as metric tag names.");

            static IEnumerable<string> FindRegisterTagNameBlocks(string text)
            {
                const string marker = "RegisterTagNames(";
                var searchIndex = 0;
                while ((searchIndex = text.IndexOf(marker, searchIndex, StringComparison.Ordinal)) >= 0)
                {
                    var endIndex = text.IndexOf(");", searchIndex, StringComparison.Ordinal);
                    if (endIndex < 0)
                        yield break;

                    yield return text[searchIndex..(endIndex + 2)];
                    searchIndex = endIndex + 2;
                }
            }
        });

    Target VerifyNoHandwrittenOtlpWireParser => d => d
        .Unlisted()
        .Description("Verify OTLP wire contracts use generated protobuf types")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            string[] removedTokens =
            [
                "OtlpProtobufParser",
                "OtlpLogProtobufParser",
                "TraceServiceMethodProvider",
                "class TraceServiceBase",
                "ProtobufReader",
                "IProtobufParseable",
                "OtlpResourceSpansProto",
                "OtlpScopeSpansProto",
                "OtlpSpanProto",
                "OtlpResourceLogsProto",
                "OtlpScopeLogsProto",
                "OtlpLogRecordProto",
                "ExportLogsServiceRequestProto",
                "OtlpExportProfilesServiceRequest",
                "OtlpResourceProfiles",
                "OtlpScopeProfiles",
                "OtlpProfile",
                "OtlpValueType",
                "OtlpProfileSample",
                "OtlpProfileFunction",
                "OtlpProfileLocation",
                "OtlpProfileLine",
                "OtlpProfileMapping",
                "OtlpProfileLink",
                "OtlpProfileStack"
            ];

            static bool ContainsRemovedToken(string text, string token)
            {
                if (!token.All(static c => char.IsLetterOrDigit(c) || c is '_'))
                    return text.Contains(token, StringComparison.Ordinal);

                var index = 0;
                while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
                {
                    var before = index is 0 ? '\0' : text[index - 1];
                    var afterIndex = index + token.Length;
                    var after = afterIndex >= text.Length ? '\0' : text[afterIndex];

                    if (!IsIdentifierChar(before) && !IsIdentifierChar(after))
                        return true;

                    index += token.Length;
                }

                return false;
            }

            static bool IsIdentifierChar(char value) => char.IsLetterOrDigit(value) || value is '_';

            var offenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static f =>
                {
                    var path = f.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => removedTokens
                    .Where(token => ContainsRemovedToken(file.Text, token))
                    .Select(token => (file.File, Token: token)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector OTLP wire contracts use generated protobuf types");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Removed OTLP wire parser '{Token}' found in {File}", offender.Token, offender.File);

            throw new InvalidOperationException(
                "Do not reintroduce handwritten OTLP protobuf readers or service binders. " +
                "Use the checked-in OpenTelemetry .proto inputs and Grpc.Tools-generated OpenTelemetry.Proto.* types.");
        });

    Target VerifyNoRemovedBuildSurface => d => d
        .Unlisted()
        .Description("Verify removed local build surfaces stay removed")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var buildDirectory = RootDirectory / "eng" / "build";
            AbsolutePath[] files =
            [
                RootDirectory / "Directory.Packages.props",
                RootDirectory / ".gitignore",
                RootDirectory / ".github" / "workflows" / "ci.yml",
                RootDirectory / "eng" / "build.sh",
                buildDirectory / "build.csproj",
                buildDirectory / "Build.cs",
                RootDirectory / "packages" / "Qyl.Telemetry" / "Qyl.Telemetry.csproj",
                RootDirectory / "packages" / "Qyl.Telemetry" / "Conventions" / "QylAttributes.cs"
            ];

            string[] removedTokens =
            [
                "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration",
                "Scalar.Kiota",
                "core/specs",
                "core/openapi",
                "eng/semconv",
                "packages/Qyl.Contracts",
                "./eng/build.sh Generate",
                "./eng/build.sh OtelConventions",
                "nuke Generate",
                "nuke OtelConventions",
                "otel-conventions-api"
            ];

            string[] removedCollectorTokens =
            [
                "ITelemetrySseBroadcaster",
                "TelemetrySseBroadcaster",
                "TelemetryMessage",
                "TelemetrySignal",
                "PublishSpans",
                "JsonSerializable(typeof(AgentDecisionRecord))",
                "JsonSerializable(typeof(AgentRunRecord))",
                "JsonSerializable(typeof(ErrorCategoryStat))",
                "JsonSerializable(typeof(ErrorRow))",
                "JsonSerializable(typeof(ErrorStats))",
                "JsonSerializable(typeof(IReadOnlyList<ErrorCategoryStat>))",
                "JsonSerializable(typeof(IReadOnlyList<ErrorRow>))",
                "JsonSerializable(typeof(SpanBatch))",
                "JsonSerializable(typeof(SpanStorageRow))",
                "JsonSerializable(typeof(List<AgentDecisionRecord>))",
                "JsonSerializable(typeof(List<AgentRunRecord>))",
                "JsonSerializable(typeof(List<SpanStorageRow>))",
                "JsonSerializable(typeof(List<ToolCallRecord>))",
                "JsonSerializable(typeof(ToolCallRecord))"
            ];

            string[] removedCollectorRouteLiterals =
            [
                "\"/observe",
                "\"/metrics"
            ];

            AbsolutePath[] removedPaths =
            [
                RootDirectory / "services" / "qyl.collector" / "Contracts",
                RootDirectory / "services" / "qyl.collector" / "Observe",
                RootDirectory / "services" / "qyl.collector" / "Metrics",
                RootDirectory / "services" / "qyl.collector" / "Storage" / "promoted-columns.g.sql",
                RootDirectory / "packages" / "Qyl.Telemetry" / "Conventions" / "Qyl.g.cs"
            ];

            var offenders = files
                .Where(static file => file.FileExists())
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => removedTokens
                    .Where(token => file.Text.Contains(token, StringComparison.Ordinal))
                    .Select(token => (file.File, Token: token)))
                .ToList();

            var collectorOffenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static file =>
                {
                    var path = file.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => removedCollectorTokens
                    .Where(token => ContainsRemovedToken(file.Text, token))
                    .Select(token => (file.File, Token: token)))
                .ToList();

            var collectorRouteOffenders = (CollectorDirectory / "Hosting" / "CollectorEndpointExtensions.cs")
                .FileExists()
                ? removedCollectorRouteLiterals
                    .Where(token => File.ReadAllText(CollectorDirectory / "Hosting" / "CollectorEndpointExtensions.cs")
                        .Contains(token, StringComparison.Ordinal))
                    .Select(token => (
                        File: RootDirectory.GetRelativePathTo(
                            CollectorDirectory / "Hosting" / "CollectorEndpointExtensions.cs").ToString(),
                        Token: token))
                    .ToList()
                : [];

            var pathOffenders = removedPaths
                .Where(static path => path.FileExists() || Directory.Exists(path.ToString()))
                .Select(path => RootDirectory.GetRelativePathTo(path).ToString())
                .ToList();

            if (offenders.Count is 0 &&
                collectorOffenders.Count is 0 &&
                collectorRouteOffenders.Count is 0 &&
                pathOffenders.Count is 0)
            {
                Log.Information("Removed local build surfaces stayed removed");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Removed build surface '{Token}' found in {File}", offender.Token, offender.File);

            foreach (var offender in collectorOffenders)
                Log.Error("  Removed collector surface '{Token}' found in {File}", offender.Token, offender.File);

            foreach (var offender in collectorRouteOffenders)
                Log.Error("  Removed collector route '{Token}' found in {File}", offender.Token, offender.File);

            foreach (var path in pathOffenders)
                Log.Error("  Removed collector surface found: {Path}", path);

            throw new InvalidOperationException(
                "Do not reintroduce removed local build surfaces. qyl-api-schema is the API/schema source of truth.");

            static bool ContainsRemovedToken(string text, string token)
            {
                if (!token.All(static c => char.IsLetterOrDigit(c) || c is '_'))
                    return text.Contains(token, StringComparison.Ordinal);

                var index = 0;
                while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
                {
                    var before = index is 0 ? '\0' : text[index - 1];
                    var afterIndex = index + token.Length;
                    var after = afterIndex >= text.Length ? '\0' : text[afterIndex];

                    if (!IsIdentifierChar(before) && !IsIdentifierChar(after))
                        return true;

                    index += token.Length;
                }

                return false;
            }

            static bool IsIdentifierChar(char value) => char.IsLetterOrDigit(value) || value is '_';
        });

    Target Verify => d => d
        .Description("Run all generated code verification checks")
        .DependsOn(VerifyGeneratedCode)
        .DependsOn(VerifyDuckDbSchema)
        .DependsOn(VerifyFrontendTypes)
        .DependsOn(VerifyGeneratedFilesClean)
        .DependsOn(VerifyCollectorPublicApiIsExplicit)
        .DependsOn(VerifyCollectorUsesSemanticConstants)
        .DependsOn(VerifyCollectorMetricTagsAreBounded)
        .DependsOn(VerifyNoHandwrittenOtlpWireParser)
        .DependsOn(VerifyNoRemovedBuildSurface)
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
            Log.Information("  Collector semantic keys use generated constants");
            Log.Information("  Collector metric tags are bounded");
            Log.Information("  Collector OTLP wire contracts use generated protobuf types");
            Log.Information("  Removed local build surfaces stayed removed");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });
}
