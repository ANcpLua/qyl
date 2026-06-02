


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Npm;
using Serilog;

namespace Qyl.Build;

[ParameterPrefix(nameof(IVerify))]
interface IVerify : IHazSourcePaths
{
    [Parameter("Skip verification")]
    bool? SkipVerify => TryGetValue<bool?>(() => SkipVerify);

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

    Target VerifyCollectorHasNoUnexpectedPublicTypes => d => d
        .Unlisted()
        .Description("Verify collector does not expose public types outside the ASP.NET Program hook")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var offenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static file =>
                {
                    var path = file.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .SelectMany(file => File.ReadAllLines(file)
                    .Select((line, index) => (
                        File: RootDirectory.GetRelativePathTo(file).ToString(),
                        Line: index + 1,
                        Text: line.TrimStart()))
                    .Where(static line => IsUnexpectedPublicType(line.Text)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector exposes no public type surface outside Program");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Public collector type at {File}:{Line}: {Text}",
                    offender.File, offender.Line, offender.Text);

            throw new InvalidOperationException(
                "The collector is an application, not a contract assembly. Keep collector types internal; " +
                "public API contracts must come from Qyl.Api.Contracts.");

            static bool IsUnexpectedPublicType(string line)
            {
                if (line is "public partial class Program;")
                    return false;

                return line.StartsWith("public class ", StringComparison.Ordinal)
                       || line.StartsWith("public static class ", StringComparison.Ordinal)
                       || line.StartsWith("public static partial class ", StringComparison.Ordinal)
                       || line.StartsWith("public sealed class ", StringComparison.Ordinal)
                       || line.StartsWith("public partial class ", StringComparison.Ordinal)
                       || line.StartsWith("public sealed partial class ", StringComparison.Ordinal)
                       || line.StartsWith("public record ", StringComparison.Ordinal)
                       || line.StartsWith("public sealed record ", StringComparison.Ordinal)
                       || line.StartsWith("public readonly record ", StringComparison.Ordinal)
                       || line.StartsWith("public partial record ", StringComparison.Ordinal)
                       || line.StartsWith("public struct ", StringComparison.Ordinal)
                       || line.StartsWith("public readonly struct ", StringComparison.Ordinal)
                       || line.StartsWith("public enum ", StringComparison.Ordinal);
            }
        });

    Target VerifyCollectorHasNoPublicLocalModels => d => d
        .Unlisted()
        .Description("Verify collector-local DTO models do not become public API")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var offenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static file =>
                {
                    var path = file.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .SelectMany(file => File.ReadAllLines(file)
                    .Select((line, index) => (
                        File: RootDirectory.GetRelativePathTo(file).ToString(),
                        Line: index + 1,
                        Text: line.TrimStart()))
                    .Where(static line => IsPublicLocalModelDeclaration(line.Text)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector-local DTO models are internal");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Public collector-local model at {File}:{Line}: {Text}",
                    offender.File, offender.Line, offender.Text);

            throw new InvalidOperationException(
                "Do not create public collector-local DTO/model surfaces. Public API models must come from Qyl.Api.Contracts.");

            static bool IsPublicLocalModelDeclaration(string line)
            {
                if (line.StartsWith("public sealed record ", StringComparison.Ordinal) ||
                    line.StartsWith("public sealed partial record ", StringComparison.Ordinal) ||
                    line.StartsWith("public readonly record ", StringComparison.Ordinal) ||
                    line.StartsWith("public readonly struct SpanColumn", StringComparison.Ordinal) ||
                    line.StartsWith("public enum CompareOp", StringComparison.Ordinal))
                {
                    return true;
                }

                if (!line.StartsWith("public sealed class ", StringComparison.Ordinal))
                    return false;

                var nameStart = "public sealed class ".Length;
                var nameEnd = line.IndexOfAny([' ', '(', ':', '<'], nameStart);
                var className = nameEnd < 0 ? line[nameStart..] : line[nameStart..nameEnd];

                return className.EndsWith("Options", StringComparison.Ordinal) ||
                       className.EndsWith("Entry", StringComparison.Ordinal) ||
                       className.EndsWith("Row", StringComparison.Ordinal) ||
                       className.EndsWith("Stats", StringComparison.Ordinal) ||
                       className.EndsWith("Result", StringComparison.Ordinal);
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
                "EnduserAttributes.Id",
                "GenAiAttributes.AgentId",
                "GenAiAttributes.AgentName",
                "GenAiAttributes.ConversationId",
                "GenAiAttributes.RequestModel",
                "GenAiAttributes.ResponseModel",
                "GenAiAttributes.ToolCallId",
                "GenAiAttributes.ToolName",
                "McpAttributes.SessionId",
                "SessionAttributes.Id",
                "UrlAttributes.Full",
                "UrlAttributes.Path",
                "UserAttributes.Id"
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

    Target VerifyCollectorDuckDbAccessIsStorageOnly => d => d
        .Unlisted()
        .Description("Verify collector DuckDB access stays behind storage intent methods")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            string[] forbiddenDuckDbTokens =
            [
                "DuckDB.NET.Data",
                "DuckDBConnection",
                "DuckDBCommand",
                "DuckDBParameter",
                "DuckDBException",
                "DbCommand",
                "DbDataReader",
                "CreateCommand(",
                "ExecuteReadAsync",
                "ExecuteWriteAsync"
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
                .Where(static file => !IsStorageFile(file.File))
                .SelectMany(file => forbiddenDuckDbTokens
                    .Where(token => file.Text.Contains(token, StringComparison.Ordinal))
                    .Select(token => (file.File, Token: token)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector DuckDB access stays behind storage intent methods");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  DuckDB storage detail '{Token}' found outside Storage in {File}", offender.Token, offender.File);

            throw new InvalidOperationException(
                "Do not pass DuckDB connections, commands, readers, or raw ExecuteRead/ExecuteWrite hooks outside Storage. " +
                "Expose intent methods on DuckDbStore instead.");

            static bool IsStorageFile(string relativePath)
            {
                var normalizedPath = relativePath.Replace('\\', '/');
                return normalizedPath.Contains("services/qyl.collector/Storage/", StringComparison.Ordinal);
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
                RootDirectory / "nuget.config",
                RootDirectory / ".gitignore",
                RootDirectory / ".github" / "workflows" / "ci.yml",
                RootDirectory / "eng" / "build.sh",
                buildDirectory / "build.csproj",
                buildDirectory / "Build.cs",
                RootDirectory / "packages" / "Qyl.OpenTelemetry.Extensions" / "README.md",
                RootDirectory / "packages" / "Qyl.Telemetry" / "Qyl.Telemetry.csproj",
                RootDirectory / "packages" / "Qyl.Telemetry" / "Conventions" / "QylAttributes.cs"
            ];

            string[] removedTokens =
            [
                "Nuke.OpenTelemetry.Conventions",
                "Qyl.Client",
                "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration",
                "Scalar.Kiota",
                "core/specs",
                "core/openapi",
                "eng/semconv",
                "nuget.pkg.github.com",
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
                "ObservationSubscription",
                "ObserveCatalog",
                "ObserveEndpoints",
                "SchemaVersionNegotiator",
                "SubscriptionManager",
                "DomainContracts",
                "DerivedMetricCatalog",
                "DerivedMetricQueries",
                "MetricsEndpoints",
                "SemanticAttributeKeys",
                "EmbeddingClusterWorker",
                "IEmbeddingGenerator",
                "GeneratedEmbeddings",
                "GetUnclusteredChatSpansAsync",
                "UpsertSpanClustersAsync",
                "UnclusteredSpan",
                "SpanClusterRow",
                "ExtractBaggageJson",
                "JsonFormatter.Default.Format(profile)",
                "span_clusters",
                "ServiceMaterializerService",
                "ServiceClassifier",
                "ServiceInstancesDdl",
                "ServicesViewDdl",
                "service_instances",
                "ExtractServiceInstances",
                "UpsertServiceInstanceAsync",
                "InsightsMaterializerService",
                "TopologyMaterializer",
                "ProfileMaterializer",
                "AlertsMaterializer",
                "MaterializedInsightsDdl",
                "materialized_insights",
                "GetInsightHashAsync",
                "UpsertInsightAsync",
                "InsightRow",
                "AddQylCollectorFeatures",
                "GitHubService",
                "HandshakeService",
                "ProjectService",
                "WorkspaceService",
                "GenerationJobService",
                "GenerationProfileService",
                "WorkflowRunService",
                "SchemaPlanner",
                "SchemaExecutor",
                "IPatternEngine",
                "PatternEngine",
                "DiagnosticPatternCatalog",
                "SemanticDiffService",
                "DistributionComparer",
                "StatisticalMath",
                "StoreArtifactAsync",
                "InsertAgentRunAsync",
                "InsertAlertRuleAsync",
                "GetRegressionEventsAsync",
                "WorkflowRunEntity",
                "GenerationJobRecord",
                "GitHubRepo",
                "ArtifactRow",
                "AlertRuleEntity",
                "RegressionEventRow",
                "AgentRunRecord",
                "ToolCallRecord",
                "AgentDecisionRecord",
                "IssueService",
                "ErrorIssueRow",
                "ErrorIssueEventRow",
                "ErrorBreadcrumbRow",
                "CodexTelemetryMapper",
                "WithCodexTransformations",
                "codex.",
                "GetErrorsAsync",
                "GetErrorStatsAsync",
                "UpdateErrorStatusAsync",
                "GetErrorByIdAsync",
                "ErrorExtractor",
                "ErrorFingerprinter",
                "ErrorCategorizer",
                "ErrorEvent",
                "ErrorUpsertSql",
                "ErrorsDdl",
                "ExtractAndUpsertErrorsAsync",
                "AddErrorUpsertParameters",
                "DeleteSpansBeforeAsync",
                "DeleteOldestSpansAsync",
                "DeleteOldestLogsAsync",
                "ArchiveToParquetAsync",
                "ArchiveInternalAsync",
                "ValidateArchiveDirectory",
                "ValidateDuckDbSqlPath",
                "errors(fingerprint)",
                "CREATE TABLE IF NOT EXISTS errors",
                "AddHttpLogging(",
                "UseHttpLogging(",
                "UrlAttributes.Path",
                "body LIKE",
                "CopyToAsync(payload",
                "Encoding.UTF8.GetBytes(options.",
                "ex.Message",
                "record ErrorRow",
                "record ErrorStats",
                "record ErrorCategoryStat",
                "MigrationRunner",
                "ClearAllSessionsAsync",
                "session_entities",
                "_schema_versions",
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

            string[] removedCollectorQueryTokens =
            [
                "SessionQueryService",
                "SpanQueryBuilder",
                "SpanColumn",
                "CompareOp",
                "LogCursor",
                "DuckDbJson",
                "namespace Qyl.Collector.Query"
            ];

            string[] removedCollectorRouteLiterals =
            [
                "\"/observe",
                "\"/metrics"
            ];

            AbsolutePath[] removedPaths =
            [
                RootDirectory / "services" / "qyl.collector" / "Contracts",
                RootDirectory / "services" / "qyl.collector" / "Query",
                RootDirectory / "services" / "qyl.collector" / "Observe",
                RootDirectory / "services" / "qyl.collector" / "Metrics",
                RootDirectory / "services" / "qyl.collector" / "Alerts",
                RootDirectory / "services" / "qyl.collector" / "Analytics",
                RootDirectory / "services" / "qyl.collector" / "Artifacts",
                RootDirectory / "services" / "qyl.collector" / "Conversations",
                RootDirectory / "services" / "qyl.collector" / "Dashboards",
                RootDirectory / "services" / "qyl.collector" / "Errors",
                RootDirectory / "services" / "qyl.collector" / "Identity",
                RootDirectory / "services" / "qyl.collector" / "Insights",
                RootDirectory / "services" / "qyl.collector" / "Intelligence",
                RootDirectory / "services" / "qyl.collector" / "Provisioning",
                RootDirectory / "services" / "qyl.collector" / "SchemaControl",
                RootDirectory / "services" / "qyl.collector" / "Search",
                RootDirectory / "services" / "qyl.collector" / "Services",
                RootDirectory / "services" / "qyl.collector" / "Tracking",
                RootDirectory / "services" / "qyl.collector" / "Workflows",
                RootDirectory / "services" / "qyl.collector" / "Storage" / "Migrations",
                RootDirectory / "services" / "qyl.collector" / "Storage" / "DuckDbSchema.g.cs",
                RootDirectory / "services" / "qyl.collector" / "Storage" / "DuckDbSchema.g.sql",
                RootDirectory / "services" / "qyl.collector" / "Storage" / "promoted-columns.g.sql",
                RootDirectory / "packages" / "Qyl.Client",
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

            var collectorQueryOffenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Where(static file =>
                {
                    var path = file.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => removedCollectorQueryTokens
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
                collectorQueryOffenders.Count is 0 &&
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

            foreach (var offender in collectorQueryOffenders)
                Log.Error("  Removed collector query surface '{Token}' found in {File}", offender.Token, offender.File);

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
        .Description("Run collector and frontend verification checks")
        .DependsOn(VerifyFrontendTypes)
        .DependsOn(VerifyCollectorPublicApiIsExplicit)
        .DependsOn(VerifyCollectorHasNoUnexpectedPublicTypes)
        .DependsOn(VerifyCollectorHasNoPublicLocalModels)
        .DependsOn(VerifyCollectorUsesSemanticConstants)
        .DependsOn(VerifyCollectorMetricTagsAreBounded)
        .DependsOn(VerifyCollectorDuckDbAccessIsStorageOnly)
        .DependsOn(VerifyNoHandwrittenOtlpWireParser)
        .DependsOn(VerifyNoRemovedBuildSurface)
        .Executes(() =>
        {
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Verification Complete");
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("  Committed TypeSpec DuckDB schema artifact stays absent");
            Log.Information("  Frontend TypeScript types compile");
            Log.Information("  Collector public API is explicitly mapped");
            Log.Information("  Collector exposes no public type surface outside Program");
            Log.Information("  Collector-local DTO models are internal");
            Log.Information("  Collector semantic keys use generated constants");
            Log.Information("  Collector metric tags are bounded");
            Log.Information("  Collector DuckDB access stays behind storage intent methods");
            Log.Information("  Collector OTLP wire contracts use generated protobuf types");
            Log.Information("  Removed local build surfaces stayed removed");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });
}
