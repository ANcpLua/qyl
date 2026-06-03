


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    Target VerifyCollectorHasNoLocalHttpDtos => d => d
        .Unlisted()
        .Description("Verify collector HTTP DTOs come from Qyl.Api.Contracts")
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
                        TypeName: TryGetDeclaredTypeName(line.TrimStart())))
                    .Where(static declaration => IsLocalHttpDtoName(declaration.TypeName)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector HTTP DTOs come from Qyl.Api.Contracts");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Collector-local HTTP DTO at {File}:{Line}: {Type}",
                    offender.File, offender.Line, offender.TypeName);

            throw new InvalidOperationException(
                "Do not create collector-local Request/Response/Dto/Contract types for HTTP routes. " +
                "Add or regenerate the model in qyl-api-schema, publish Qyl.Api.Contracts, then consume it here.");

            static bool IsLocalHttpDtoName(string? typeName) =>
                typeName is not null &&
                (typeName.EndsWith("Request", StringComparison.Ordinal) ||
                 typeName.EndsWith("Response", StringComparison.Ordinal) ||
                 typeName.EndsWith("Dto", StringComparison.Ordinal) ||
                 typeName.EndsWith("Contract", StringComparison.Ordinal));
        });

    Target VerifyCollectorHasNoLocalApiModels => d => d
        .Unlisted()
        .Description("Verify collector does not define API-facing local models")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var offenders = CollectorSourceFiles()
                .SelectMany(file => DeclaredTypes(file)
                    .Where(static declaration => IsLocalApiModel(declaration))
                    .Select(declaration => (
                        File: declaration.File,
                        declaration.Line,
                        declaration.Name,
                        declaration.Namespace)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector declares no local API-facing models");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Local API model at {File}:{Line}: {Namespace}.{Name}",
                    offender.File, offender.Line, offender.Namespace, offender.Name);

            throw new InvalidOperationException(
                "Do not create collector-local API models. Public API models must be generated in qyl-api-schema " +
                "and consumed through Qyl.Api.Contracts; collector-local records/classes may only represent storage, " +
                "ingestion, or infrastructure internals.");

            static bool IsLocalApiModel(DeclaredType declaration)
            {
                if (declaration.IsStatic)
                    return false;

                if (declaration.Namespace.StartsWith("Qyl.Collector.Observe", StringComparison.Ordinal) ||
                    declaration.Namespace.StartsWith("Qyl.Collector.Metrics", StringComparison.Ordinal) ||
                    declaration.Path.Contains("/Observe/", StringComparison.Ordinal) ||
                    declaration.Path.Contains("/Metrics/", StringComparison.Ordinal))
                {
                    return true;
                }

                if (IsInfrastructureType(declaration.Name))
                    return false;

                if (IsStorageOrIngestionInternal(declaration.Path))
                    return false;

                return IsApiShapedName(declaration.Name);
            }

            static bool IsStorageOrIngestionInternal(string path) =>
                path.Contains("/Storage/", StringComparison.Ordinal) ||
                path.Contains("/Ingestion/", StringComparison.Ordinal) ||
                path.Contains("/Cost/", StringComparison.Ordinal);

            static bool IsInfrastructureType(string name) =>
                name.EndsWith("Middleware", StringComparison.Ordinal) ||
                name.EndsWith("HealthCheck", StringComparison.Ordinal) ||
                name.EndsWith("Options", StringComparison.Ordinal) ||
                name.EndsWith("Service", StringComparison.Ordinal) ||
                name.EndsWith("ServiceImpl", StringComparison.Ordinal) ||
                name.EndsWith("Context", StringComparison.Ordinal) ||
                name.EndsWith("Redactor", StringComparison.Ordinal);

            static bool IsApiShapedName(string name) =>
                name.EndsWith("Request", StringComparison.Ordinal) ||
                name.EndsWith("Response", StringComparison.Ordinal) ||
                name.EndsWith("Dto", StringComparison.Ordinal) ||
                name.EndsWith("Contract", StringComparison.Ordinal) ||
                name.EndsWith("Entity", StringComparison.Ordinal) ||
                name.EndsWith("Stats", StringComparison.Ordinal) ||
                name.EndsWith("Summary", StringComparison.Ordinal) ||
                name.EndsWith("Page", StringComparison.Ordinal) ||
                name.EndsWith("Query", StringComparison.Ordinal) ||
                name.EndsWith("Filter", StringComparison.Ordinal) ||
                name.EndsWith("Result", StringComparison.Ordinal) ||
                name.EndsWith("Item", StringComparison.Ordinal) ||
                name.EndsWith("Message", StringComparison.Ordinal) ||
                name.EndsWith("Event", StringComparison.Ordinal) ||
                name.EndsWith("Subscription", StringComparison.Ordinal) ||
                name.EndsWith("Catalog", StringComparison.Ordinal);
        });

    Target VerifyCollectorHttpJsonContextUsesOnlyContracts => d => d
        .Unlisted()
        .Description("Verify collector HTTP JSON context does not expose local models")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var contextFile = CollectorDirectory / "QylSerializerContext.cs";
            if (!contextFile.FileExists())
            {
                Log.Information("Collector HTTP JSON context is absent");
                return;
            }

            var localTypeNames = CollectorSourceFiles()
                .SelectMany(file => DeclaredTypes(file).Select(static declaration => declaration.Name))
                .ToHashSet(StringComparer.Ordinal);

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(contextFile), path: contextFile.ToString());
            var root = tree.GetCompilationUnitRoot();
            var offenders = root.DescendantNodes()
                .OfType<AttributeSyntax>()
                .Where(static attribute => attribute.Name.ToString().Contains("JsonSerializable", StringComparison.Ordinal))
                .Select(attribute => (
                    Attribute: attribute,
                    TypeName: ExtractJsonSerializableTypeName(attribute)))
                .Where(static item => item.TypeName is not null)
                .Where(item => IsLocalSerializerType(item.TypeName!, localTypeNames))
                .Select(item => (
                    File: RootDirectory.GetRelativePathTo(contextFile).ToString(),
                    Line: item.Attribute.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    TypeName: item.TypeName!))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector HTTP JSON context serializes contract models only");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Local type in HTTP JSON context at {File}:{Line}: {Type}",
                    offender.File, offender.Line, offender.TypeName);

            throw new InvalidOperationException(
                "Do not register collector-local models in QylSerializerContext. HTTP JSON output must use " +
                "Qyl.Api.Contracts models; internal ingestion/storage serializers need their own private context.");

            static string? ExtractJsonSerializableTypeName(AttributeSyntax attribute)
            {
                var firstArgument = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                if (firstArgument is not TypeOfExpressionSyntax typeOf)
                    return null;

                return UnwrapCollectionType(typeOf.Type.ToString());
            }

            static string UnwrapCollectionType(string typeName)
            {
                while (typeName.EndsWith("[]", StringComparison.Ordinal))
                    typeName = typeName[..^2];

                if (typeName.StartsWith("List<", StringComparison.Ordinal) ||
                    typeName.StartsWith("IReadOnlyList<", StringComparison.Ordinal))
                {
                    var start = typeName.IndexOf('<', StringComparison.Ordinal) + 1;
                    var end = typeName.LastIndexOf('>');
                    if (end > start)
                        return typeName[start..end].Trim();
                }

                return typeName.Trim();
            }

            static bool IsLocalSerializerType(string typeName, ISet<string> localTypeNames)
            {
                if (typeName.StartsWith("Qyl.Collector.", StringComparison.Ordinal))
                    return true;

                var simpleName = typeName.Split('.').Last();
                return localTypeNames.Contains(simpleName);
            }
        });

    Target VerifyCollectorEndpointResponsesUseContracts => d => d
        .Unlisted()
        .Description("Verify collector endpoints do not return storage DTOs directly")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            string[] directStorageResponseTokens =
            [
                "Results.Ok(spans",
                "TypedResults.Ok(spans",
                "Results.Ok(logs",
                "TypedResults.Ok(logs",
                "Results.Ok(profiles",
                "TypedResults.Ok(profiles",
                "Results.Ok(stats",
                "TypedResults.Ok(stats",
                "Results.Ok(session",
                "TypedResults.Ok(session",
                "Results.Ok(detail",
                "TypedResults.Ok(detail",
                "Results.Ok(rows",
                "TypedResults.Ok(rows",
                "JsonSerializable(typeof(SpanStorageRow))",
                "JsonSerializable(typeof(LogStorageRow))",
                "JsonSerializable(typeof(ProfileStorageRow))",
                "JsonSerializable(typeof(SessionQueryRow))",
                "JsonSerializable(typeof(StorageStats))",
                "JsonSerializable(typeof(SessionGenAiStats))"
            ];

            AbsolutePath[] endpointFiles =
            [
                CollectorDirectory / "Hosting" / "CollectorEndpointExtensions.cs",
                CollectorDirectory / "SpanEndpoints.cs",
                CollectorDirectory / "QylSerializerContext.cs"
            ];

            var offenders = endpointFiles
                .Where(static file => file.FileExists())
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => directStorageResponseTokens
                    .Where(token => file.Text.Contains(token, StringComparison.Ordinal))
                    .Select(token => (file.File, Token: token)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector endpoint responses are contract-backed");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Direct storage response token '{Token}' found in {File}", offender.Token, offender.File);

            throw new InvalidOperationException(
                "Do not return storage rows, storage stats, or local DTOs directly from HTTP endpoints. " +
                "Map storage internals to Qyl.Api.Contracts in Mapping/Mappers.cs before returning.");
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

    Target VerifyCollectorStorageReadsUseGeneratedColumnLists => d => d
        .Unlisted()
        .Description("Verify storage row reads use generated DuckDB column lists")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var storeFile = CollectorDirectory / "Storage" / "DuckDbStore.cs";
            if (!storeFile.FileExists())
                throw new InvalidOperationException("Missing DuckDbStore.cs storage implementation.");

            var text = File.ReadAllText(storeFile);

            string[] requiredGeneratedColumnLists =
            [
                "SpanStorageRow.SelectColumnList",
                "LogStorageRow.SelectColumnList",
                "ProfileStorageRow.SelectColumnList",
                "ProfileFunctionRow.SelectColumnList",
                "ProfileLocationRow.SelectColumnList",
                "ProfileMappingRow.SelectColumnList",
                "ProfileSampleRow.SelectColumnList",
                "ProfileStackRow.SelectColumnList"
            ];

            string[] forbiddenManualSelectTokens =
            [
                "SelectSpanColumns",
                "SELECT log_id, trace_id",
                "SELECT profile_id, trace_id",
                "SELECT profile_id, ordinal"
            ];

            var missing = requiredGeneratedColumnLists
                .Where(token => !text.Contains(token, StringComparison.Ordinal))
                .ToList();
            var forbidden = forbiddenManualSelectTokens
                .Where(token => text.Contains(token, StringComparison.Ordinal))
                .ToList();

            if (missing.Count is 0 && forbidden.Count is 0)
            {
                Log.Information("Collector storage row reads use generated DuckDB column lists");
                return;
            }

            foreach (var token in missing)
                Log.Error("  Missing generated column list usage in DuckDbStore.cs: {Token}", token);

            foreach (var token in forbidden)
                Log.Error("  Manual storage row SELECT token in DuckDbStore.cs: {Token}", token);

            throw new InvalidOperationException(
                "Do not handwrite SELECT column lists for generated storage rows. Use each row type's " +
                "generated SelectColumnList so MapFromReader ordinals and SQL columns share one source of truth.");
        });

    Target VerifyCollectorStorageTablesUseGeneratedDdl => d => d
        .Unlisted()
        .Description("Verify storage row tables use generated DuckDB DDL")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var storeFile = CollectorDirectory / "Storage" / "DuckDbStore.cs";
            if (!storeFile.FileExists())
                throw new InvalidOperationException("Missing DuckDbStore.cs storage implementation.");

            var storeText = File.ReadAllText(storeFile);

            string[] requiredGeneratedDdl =
            [
                "SpanStorageRow.CreateTableDdl",
                "LogStorageRow.CreateTableDdl",
                "ProfileStorageRow.CreateTableDdl",
                "ProfileFunctionRow.CreateTableDdl",
                "ProfileLocationRow.CreateTableDdl",
                "ProfileMappingRow.CreateTableDdl",
                "ProfileSampleRow.CreateTableDdl",
                "ProfileStackRow.CreateTableDdl"
            ];

            var schemaText = string.Concat(
                ReadIfExists(CollectorDirectory / "Storage" / "DuckDbSchema.Core.cs"),
                ReadIfExists(CollectorDirectory / "Storage" / "DuckDbSchema.Logs.cs"),
                ReadIfExists(CollectorDirectory / "Storage" / "DuckDbSchema.Profiles.cs"));

            string[] forbiddenManualDdl =
            [
                "SpansDdl",
                "LogsDdl",
                "ProfilesDdl",
                "ProfileFunctionsDdl",
                "ProfileLocationsDdl",
                "ProfileMappingsDdl",
                "ProfileSamplesDdl",
                "ProfileStacksDdl",
                "CREATE TABLE IF NOT EXISTS spans",
                "CREATE TABLE IF NOT EXISTS logs",
                "CREATE TABLE IF NOT EXISTS profiles",
                "CREATE TABLE IF NOT EXISTS profile_functions",
                "CREATE TABLE IF NOT EXISTS profile_locations",
                "CREATE TABLE IF NOT EXISTS profile_mappings",
                "CREATE TABLE IF NOT EXISTS profile_samples",
                "CREATE TABLE IF NOT EXISTS profile_stacks"
            ];

            var missing = requiredGeneratedDdl
                .Where(token => !storeText.Contains(token, StringComparison.Ordinal))
                .ToList();
            var forbidden = forbiddenManualDdl
                .Where(token => schemaText.Contains(token, StringComparison.Ordinal))
                .ToList();

            if (missing.Count is 0 && forbidden.Count is 0)
            {
                Log.Information("Collector storage row tables use generated DuckDB DDL");
                return;
            }

            foreach (var token in missing)
                Log.Error("  Missing generated CREATE TABLE DDL usage in DuckDbStore.cs: {Token}", token);

            foreach (var token in forbidden)
                Log.Error("  Manual storage row table DDL token found in DuckDbSchema files: {Token}", token);

            throw new InvalidOperationException(
                "Do not handwrite CREATE TABLE DDL for generated storage rows. Put schema metadata on " +
                "DuckDbTable/DuckDbColumn attributes and initialize row tables through generated CreateTableDdl.");

            static string ReadIfExists(AbsolutePath path) =>
                path.FileExists() ? File.ReadAllText(path) : "";
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

    Target VerifyOtlpConverterHotPath => d => d
        .Unlisted()
        .Description("Verify OTLP converter hot path avoids removed allocation patterns")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var converterFile = CollectorDirectory / "Ingestion" / "OtlpConverter.cs";
            if (!converterFile.FileExists())
                throw new FileNotFoundException("Missing OTLP converter", converterFile.ToString());

            string[] forbidden =
            [
                ".Select(ConvertProtoAnyValueToString)",
                ".Where(static kv => ConvertProtoAnyValueToString",
                ".ToDictionary(",
                "ToByteArray()",
                ".FirstOrDefault(static a => a.Key.IsAny("
            ];

            var lines = File.ReadAllLines(converterFile);
            var offenders = lines
                .SelectMany((line, index) => forbidden
                    .Where(token => line.Contains(token, StringComparison.Ordinal))
                    .Select(token => (Line: index + 1, Token: token, Text: line.Trim())))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("OTLP converter hot path avoids removed allocation patterns");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Removed hot-path token '{Token}' at {File}:{Line}: {Text}",
                    offender.Token,
                    RootDirectory.GetRelativePathTo(converterFile),
                    offender.Line,
                    offender.Text);

            throw new InvalidOperationException(
                "Do not reintroduce LINQ duplicate conversion, ByteString.ToByteArray, or extra session-correlation scans " +
                "in OtlpConverter hot paths.");
        });

    Target VerifyCollectorSpanIdentityIsComposite => d => d
        .Unlisted()
        .Description("Verify span storage identity is trace-scoped")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var spanStorageRowFile = CollectorDirectory / "Storage" / "DuckDbReaderExtensions.cs";
            var storeFile = CollectorDirectory / "Storage" / "DuckDbStore.cs";

            string[] forbidden =
            [
                "PRIMARY KEY (span_id)",
                "PRIMARY KEY (\"span_id\")",
                "ON CONFLICT (span_id)"
            ];

            var offenders = CollectorDirectory.GlobFiles("**/*.cs")
                .Concat(CollectorDirectory.GlobFiles("**/*.sql"))
                .Where(static file =>
                {
                    var path = file.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => forbidden
                    .Where(token => file.Text.Contains(token, StringComparison.Ordinal))
                    .Select(token => (file.File, Token: token)))
                .ToList();

            var missingRequired = new List<string>();
            var spanStorageRowText = spanStorageRowFile.FileExists() ? File.ReadAllText(spanStorageRowFile) : "";
            if (!spanStorageRowText.Contains("[DuckDbColumn(PrimaryKeyOrdinal = 0)]\n    public required string TraceId", StringComparison.Ordinal) ||
                !spanStorageRowText.Contains("[DuckDbColumn(PrimaryKeyOrdinal = 1)]\n    public required string SpanId", StringComparison.Ordinal))
            {
                missingRequired.Add(RootDirectory.GetRelativePathTo(spanStorageRowFile).ToString()
                                    + " must declare generated PRIMARY KEY order TraceId=0, SpanId=1");
            }

            if (!spanStorageRowText.Contains("ON CONFLICT (trace_id, span_id)", StringComparison.Ordinal))
            {
                missingRequired.Add(RootDirectory.GetRelativePathTo(spanStorageRowFile).ToString()
                                    + " must upsert ON CONFLICT (trace_id, span_id)");
            }

            if (!storeFile.FileExists() ||
                !File.ReadAllText(storeFile).Contains("SpanStorageRow.CreateTableDdl", StringComparison.Ordinal))
            {
                missingRequired.Add(RootDirectory.GetRelativePathTo(storeFile).ToString()
                                    + " must initialize spans through SpanStorageRow.CreateTableDdl");
            }

            if (offenders.Count is 0 && missingRequired.Count is 0)
            {
                Log.Information("Collector span storage identity is trace-scoped");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Span identity regression token '{Token}' found in {File}", offender.Token, offender.File);

            foreach (var missing in missingRequired)
                Log.Error("  Missing span identity invariant: {Invariant}", missing);

            throw new InvalidOperationException(
                "Span ids are unique only within traces. Store and upsert spans by (trace_id, span_id), never span_id alone.");
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
                RootDirectory / "services" / "qyl.collector" / "qyl.collector.csproj",
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
                "Microsoft.AspNetCore.Authentication.JwtBearer",
                "otel-conventions-api"
            ];

            string[] removedCollectorTokens =
            [
                "ITelemetrySseBroadcaster",
                "TelemetrySseBroadcaster",
                "TelemetryMessage",
                "TelemetrySignal",
                "PublishSpans",
                "SpanRingBuffer",
                "LiveLogDeduplicator",
                "DeduplicatedLiveLog",
                "SseItem<object?>",
                "new { status = \"ok\" }",
                "new { logs =",
                "QYL_RINGBUFFER_CAPACITY",
                "PushRange(",
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
                "BaggageJson",
                "baggage_json",
                "GenAiToolCallId",
                "gen_ai_tool_call_id",
                "GetValueOrDefault(GenAiAttributes.ToolCallId)",
                "QylCapabilityPrefix",
                "AttributeKeyPrefix",
                "StartsWithOrdinal(QylCapabilityPrefix)",
                "Paths.Any(path.StartsWithIgnoreCase)",
                "status_message",
                "span.Status?.Message",
                "ProfileDataJson",
                "profile_data_json",
                "ProfileFrameType",
                "profile_frame_type",
                "DropTelemetryTablesWhenSpanIdentityIsLegacy",
                "ShouldDropTelemetryTablesForSpanIdentity",
                "s_legacyTelemetry",
                "PRAGMA table_info",
                "DROP TABLE IF EXISTS",
                "InferProvider",
                "ExtractProviders",
                "StartsWithIgnoreCase(\"gpt\")",
                "StartsWithIgnoreCase(\"claude\")",
                "StartsWithIgnoreCase(\"gemini\")",
                "StartsWithIgnoreCase(\"llama\")",
                "StartsWithIgnoreCase(\"mistral\")",
                "StartsWithIgnoreCase(\"command\")",
                "ExtractBaggageJson",
                "JsonFormatter.Default.Format(profile)",
                ".Select(ConvertProtoAnyValueToString)",
                ".ToDictionary(",
                "ToByteArray()",
                "Serialize(attributes, ShouldPersistSpanAttribute)",
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
                "AddAuthentication(",
                "UseAuthentication(",
                "UseAuthorization(",
                "JwtBearerDefaults",
                "KeycloakOptions",
                "QylAgentInventoryEndpoint.AdminPolicy",
                "QYL_KEYCLOAK",
                "UrlAttributes.Path",
                "SourceLocation",
                "SourceLocationCache",
                "LogSourceEnricher",
                "PdbSourceResolver",
                "PRIMARY KEY (span_id)",
                "ON CONFLICT (span_id)",
                "source_file",
                "source_line",
                "source_column",
                "source_method",
                "dropped_attributes_count",
                "instrumentation_scope",
                "endpoint.DisplayName",
                "Name.Split(' ')",
                "Dictionary<string, DedupBucket>",
                "$\"{service}\\u001f{severity}\\u001f{body}\"",
                "_buckets.MinBy",
                "qb.Add(\"time_unix_nano > $N\"",
                "after = ordered[^1].TimeUnixNano",
                "OrderBy(static l => l.TimeUnixNano).ToArray()",
                "SpanRowMapper",
                "ClearTableAsync(\"profiles\"",
                "thread.Join(TimeSpan.FromSeconds(2))",
                "DELETE FROM profile_functions;",
                "/api/v1/ingest",
                "StartsWithIgnoreCase(\"mcp/\")",
                "qyl native protocol",
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
                "JsonSerializable(typeof(ToolCallRecord))",
                "public int SessionsDeleted",
                "public int ConsoleCleared",
                "SessionsDeleted = result.SessionsDeleted",
                "ConsoleCleared = result.ConsoleCleared",
                "TotalDeleted =>",
                "GetGenAiStatsAsync",
                "GetGenAiSpansAsync",
                "SessionGenAiStats",
                "ContractStatsMapper",
                "TelemetryTableClearCounts",
                "ClearAllTelemetryAsync",
                "ClearAllSpansAsync",
                "ClearAllLogsAsync",
                "ClearAllProfilesAsync",
                "MetricCount",
                "DroppedLogCount",
                "DroppedMetricCount",
                "SpanColumnCount",
                "SpanColumnList",
                "SpanOnConflictClause",
                "BuildMultiRowSpanInsertSql",
                "AddSpanParameters",
                "ManualLogsDdl",
                "LogColumnCount",
                "LogColumnList",
                "s_logInsertSqlCache",
                "BuildMultiRowLogInsertSql",
                "AddLogParameters",
                "MapLog(",
                "BuildMultiRowInsertSql(string table",
                "ProfileStorageRow.ColumnList",
                "InsertRowsBatchedAsync(con, tx, \"profile_",
                "static (cmd, f)",
                "static (cmd, l)",
                "static (cmd, m)",
                "static (cmd, s)",
                "static (cmd, st)",
                "MapProfile(",
                "TRY_CAST(status_code",
                "kind VARCHAR NOT NULL",
                "status_code VARCHAR NOT NULL"
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
                "\"/metrics",
                "\"/logs/live",
                "\"/sessions/{sessionId}/spans",
                "\"/traces/{traceId}/profiles",
                "\"/genai",
                "\"/telemetry",
                "\"/meta"
            ];

            AbsolutePath[] removedPaths =
            [
                RootDirectory / "services" / "qyl.collector" / "Contracts",
                RootDirectory / "services" / "qyl.collector" / "Query",
                RootDirectory / "services" / "qyl.collector" / "Observe",
                RootDirectory / "services" / "qyl.collector" / "Metrics",
                RootDirectory / "services" / "qyl.collector" / "Alerts",
                RootDirectory / "services" / "qyl.collector" / "Auth",
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
                RootDirectory / "services" / "qyl.collector" / "Storage" / "SpanRowMapper.cs",
                RootDirectory / "services" / "qyl.collector" / "Ingestion" / "LogSourceEnricher.cs",
                RootDirectory / "services" / "qyl.collector" / "Ingestion" / "PdbSourceResolver.cs",
                RootDirectory / "services" / "qyl.collector" / "Ingestion" / "SourceLocation.cs",
                RootDirectory / "services" / "qyl.collector" / "Ingestion" / "SourceLocationCache.cs",
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
        .DependsOn(VerifyCollectorHasNoLocalHttpDtos)
        .DependsOn(VerifyCollectorHasNoLocalApiModels)
        .DependsOn(VerifyCollectorHttpJsonContextUsesOnlyContracts)
        .DependsOn(VerifyCollectorEndpointResponsesUseContracts)
        .DependsOn(VerifyCollectorUsesSemanticConstants)
        .DependsOn(VerifyCollectorMetricTagsAreBounded)
        .DependsOn(VerifyCollectorDuckDbAccessIsStorageOnly)
        .DependsOn(VerifyCollectorStorageReadsUseGeneratedColumnLists)
        .DependsOn(VerifyCollectorStorageTablesUseGeneratedDdl)
        .DependsOn(VerifyNoHandwrittenOtlpWireParser)
        .DependsOn(VerifyOtlpConverterHotPath)
        .DependsOn(VerifyCollectorSpanIdentityIsComposite)
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
            Log.Information("  Collector HTTP DTOs come from Qyl.Api.Contracts");
            Log.Information("  Collector declares no local API-facing models");
            Log.Information("  Collector HTTP JSON context serializes contract models only");
            Log.Information("  Collector endpoint responses are contract-backed");
            Log.Information("  Collector semantic keys use generated constants");
            Log.Information("  Collector metric tags are bounded");
            Log.Information("  Collector DuckDB access stays behind storage intent methods");
            Log.Information("  Collector storage row reads use generated DuckDB column lists");
            Log.Information("  Collector storage row tables use generated DuckDB DDL");
            Log.Information("  Collector OTLP wire contracts use generated protobuf types");
            Log.Information("  OTLP converter hot path avoids removed allocation patterns");
            Log.Information("  Collector span storage identity is trace-scoped");
            Log.Information("  Removed local build surfaces stayed removed");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    private readonly record struct DeclaredType(
        string File,
        string Path,
        int Line,
        string Namespace,
        string Name,
        bool IsStatic);

    private IEnumerable<AbsolutePath> CollectorSourceFiles() =>
        CollectorDirectory.GlobFiles("**/*.cs")
            .Where(static file =>
            {
                var path = file.ToString();
                return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                       && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
            });

    private IEnumerable<DeclaredType> DeclaredTypes(AbsolutePath file)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file.ToString());
        var root = tree.GetCompilationUnitRoot();
        var relativePath = RootDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');

        foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var line = declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new DeclaredType(
                RootDirectory.GetRelativePathTo(file).ToString(),
                relativePath,
                line,
                ResolveNamespace(declaration),
                declaration.Identifier.ValueText,
                declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)));
        }

        static string ResolveNamespace(SyntaxNode node)
        {
            for (var current = node.Parent; current is not null; current = current.Parent)
            {
                if (current is BaseNamespaceDeclarationSyntax namespaceDeclaration)
                    return namespaceDeclaration.Name.ToString();
            }

            return "";
        }
    }

    private static string? TryGetDeclaredTypeName(string line)
    {
        string[] prefixes =
        [
            "public sealed partial record ",
            "public sealed record ",
            "public readonly record ",
            "public partial record ",
            "public record ",
            "public sealed partial class ",
            "public sealed class ",
            "public static partial class ",
            "public static class ",
            "public partial class ",
            "public class ",
            "public readonly struct ",
            "public struct ",
            "public enum ",
            "internal sealed partial record ",
            "internal sealed record ",
            "internal readonly record ",
            "internal partial record ",
            "internal record ",
            "internal sealed partial class ",
            "internal sealed class ",
            "internal static partial class ",
            "internal static class ",
            "internal partial class ",
            "internal class ",
            "internal readonly struct ",
            "internal struct ",
            "internal enum "
        ];

        foreach (var prefix in prefixes)
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var nameStart = prefix.Length;
            var nameEnd = line.IndexOfAny([' ', '(', ':', '<', ';'], nameStart);
            return nameEnd < 0 ? line[nameStart..] : line[nameStart..nameEnd];
        }

        return null;
    }
}
