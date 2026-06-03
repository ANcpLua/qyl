


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
            var offenders = CollectorSourceFiles()
                .SelectMany(ForbiddenEndpointMappers)
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector public API is explicitly mapped");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Generated endpoint mapper at {File}:{Line}: {Text}",
                    offender.File, offender.Line, offender.Text);

            throw new InvalidOperationException(
                "Do not publish collector-local endpoint modules through QylMapEndpoints, MapQylGeneratedEndpoints, " +
                "or standalone Map*Endpoint extension methods. Expose contract-backed routes explicitly from CollectorEndpointExtensions.");
        });

    Target VerifyCollectorHasNoUnexpectedPublicTypes => d => d
        .Unlisted()
        .Description("Verify collector does not expose public types outside the ASP.NET Program hook")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var offenders = CollectorSourceFiles()
                .SelectMany(DeclaredTypes)
                .Where(static declaration => declaration.IsPublic && !IsAllowedPublicCollectorType(declaration))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector exposes no public type surface outside Program");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Public collector type at {File}:{Line}: {Namespace}.{Name}",
                    offender.File, offender.Line, offender.Namespace, offender.Name);

            throw new InvalidOperationException(
                "The collector is an application, not a contract assembly. Keep collector types internal; " +
                "public API contracts must come from Qyl.Api.Contracts.");
        });

    Target VerifyCollectorHasNoPublicLocalModels => d => d
        .Unlisted()
        .Description("Verify collector-local DTO models do not become public API")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var offenders = CollectorSourceFiles()
                .SelectMany(DeclaredTypes)
                .Where(static declaration =>
                    declaration.IsPublic &&
                    !IsAllowedPublicCollectorType(declaration) &&
                    IsPublicLocalModelDeclaration(declaration))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector-local DTO models are internal");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Public collector-local model at {File}:{Line}: {Namespace}.{Name}",
                    offender.File, offender.Line, offender.Namespace, offender.Name);

            throw new InvalidOperationException(
                "Do not create public collector-local DTO/model surfaces. Public API models must come from Qyl.Api.Contracts.");

            static bool IsPublicLocalModelDeclaration(DeclaredType declaration)
            {
                if (declaration.Kind is "record" or "record struct")
                    return true;

                if ((declaration.Kind is "struct" && declaration.Name is "SpanColumn") ||
                    (declaration.Kind is "enum" && declaration.Name is "CompareOp"))
                {
                    return true;
                }

                return declaration.Kind is "class" &&
                       declaration.IsSealed &&
                       (declaration.Name.EndsWith("Options", StringComparison.Ordinal) ||
                        declaration.Name.EndsWith("Entry", StringComparison.Ordinal) ||
                        declaration.Name.EndsWith("Row", StringComparison.Ordinal) ||
                        declaration.Name.EndsWith("Stats", StringComparison.Ordinal) ||
                        declaration.Name.EndsWith("Result", StringComparison.Ordinal));
            }
        });

    Target VerifyCollectorHasNoLocalHttpDtos => d => d
        .Unlisted()
        .Description("Verify collector HTTP DTOs come from Qyl.Api.Contracts")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var offenders = CollectorSourceFiles()
                .SelectMany(DeclaredTypes)
                .Where(static declaration => IsLocalHttpDtoName(declaration.Name))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector HTTP DTOs come from Qyl.Api.Contracts");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Collector-local HTTP DTO at {File}:{Line}: {Namespace}.{Name}",
                    offender.File, offender.Line, offender.Namespace, offender.Name);

            throw new InvalidOperationException(
                "Do not create collector-local Request/Response/Dto/Contract types for HTTP routes. " +
                "Add or regenerate the model in qyl-api-schema, publish Qyl.Api.Contracts, then consume it here.");

            static bool IsLocalHttpDtoName(string typeName) =>
                typeName.EndsWith("Request", StringComparison.Ordinal) ||
                typeName.EndsWith("Response", StringComparison.Ordinal) ||
                typeName.EndsWith("Dto", StringComparison.Ordinal) ||
                typeName.EndsWith("Contract", StringComparison.Ordinal);
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
                "JsonSerializable(typeof(SessionGenAiStats))",
                "SseItem<LogRecord>",
                "ServerSentEventsResult<"
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
            var offenders = CollectorSourceFiles()
                .Where(static file => !file.ToString().EndsWith(".g.cs", StringComparison.Ordinal))
                .SelectMany(SemanticUsageOffenders)
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector semantic attribute keys use generated constants");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  {Kind} at {File}:{Line}: {Text}",
                    offender.Kind, offender.File, offender.Line, offender.Text);

            throw new InvalidOperationException(
                "Do not hardcode semantic attribute keys or consume SemConv attribute types directly in the collector. " +
                "Generate CollectorSemanticAttributeCatalog.g.cs from Qyl.OpenTelemetry.SemanticConventions and consume it there.");
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

    Target VerifyCollectorSessionFacetsAreBounded => d => d
        .Unlisted()
        .Description("Verify collector session summaries do not aggregate unbounded distinct facets")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            string[] forbiddenSessionFacetTokens =
            [
                "LIST(DISTINCT",
                "ARRAY_AGG(DISTINCT",
                "STRING_AGG(DISTINCT",
                "GROUP_CONCAT(DISTINCT"
            ];

            var sessionsFile = CollectorDirectory / "Storage" / "DuckDbStore.Sessions.cs";
            var text = sessionsFile.FileExists() ? File.ReadAllText(sessionsFile) : "";
            var offenders = forbiddenSessionFacetTokens
                .Where(token => text.Contains(token, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Collector session facets are bounded");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Unbounded session facet aggregate '{Token}' found in {File}",
                    offender,
                    RootDirectory.GetRelativePathTo(sessionsFile));

            throw new InvalidOperationException(
                "Do not aggregate unbounded distinct provider, model, or service arrays into session summaries. " +
                "Use a named bounded aggregate limit before materializing Qyl.Api.Contracts session models.");
        });

    Target VerifyInstrumentationHasNoStorageTenantKnowledge => d => d
        .Unlisted()
        .Description("Verify instrumentation packages stay storage- and tenant-blind")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            string[] forbiddenInstrumentationTokens =
            [
                "ProjectId",
                "project_id",
                "qyl.project.id",
                "qyl.workspace.id",
                "tenant.id",
                "TenantId",
                "SpanStorageRow",
                "LogStorageRow",
                "ProfileStorageRow"
            ];

            var instrumentationRoots = new[]
            {
                RootDirectory / "internal" / "qyl.instrumentation",
                RootDirectory / "internal" / "qyl.instrumentation.generators"
            };

            var offenders = instrumentationRoots
                .Where(static directory => Directory.Exists(directory))
                .SelectMany(static directory => directory.GlobFiles("**/*.cs"))
                .Where(static file =>
                {
                    var path = file.ToString();
                    return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                })
                .Select(file => (
                    File: RootDirectory.GetRelativePathTo(file).ToString(),
                    Text: File.ReadAllText(file)))
                .SelectMany(file => forbiddenInstrumentationTokens
                    .Where(token => file.Text.Contains(token, StringComparison.Ordinal))
                    .Select(token => (file.File, Token: token)))
                .ToList();

            if (offenders.Count is 0)
            {
                Log.Information("Instrumentation packages are storage- and tenant-blind");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Storage or tenant token '{Token}' found in instrumentation file {File}",
                    offender.Token, offender.File);

            throw new InvalidOperationException(
                "Do not put storage schemas, tenant ids, project ids, or collector storage rows into instrumentation packages. " +
                "Stamp project_id only at the collector receiver/ingestion-to-storage boundary.");
        });

    Target VerifyCollectorStorageReadsAreProjectScoped => d => d
        .Unlisted()
        .Description("Verify collector storage reads stay scoped by project_id")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var projectScopeFile = CollectorDirectory / "Storage" / "ProjectScope.cs";
            var storeFile = CollectorDirectory / "Storage" / "DuckDbStore.cs";
            var sessionsFile = CollectorDirectory / "Storage" / "DuckDbStore.Sessions.cs";

            var projectScopeText = projectScopeFile.FileExists() ? File.ReadAllText(projectScopeFile) : "";
            var storeText = storeFile.FileExists() ? File.ReadAllText(storeFile) : "";
            var sessionsText = sessionsFile.FileExists() ? File.ReadAllText(sessionsFile) : "";

            string[] requiredProjectScopeTokens =
            [
                "public const string DefaultProjectId = \"default\";",
                "public static string Normalize(string? projectId)"
            ];

            string[] requiredStoreTokens =
            [
                "FROM spans WHERE project_id = $1 AND session_id = $2",
                "FROM spans WHERE project_id = $1 AND trace_id = $2",
                "qb.Add(\"project_id = $N\", ProjectScope.Normalize(projectId));",
                "(SELECT COUNT(*) FROM spans WHERE project_id = $1)",
                "(SELECT COUNT(*) FROM logs WHERE project_id = $1)",
                "SELECT COUNT(*) FROM spans WHERE project_id = $1",
                "SELECT COUNT(*) FROM logs WHERE project_id = $1",
                "FROM profiles WHERE project_id = $1 AND profile_id = $2",
                "ReadChildRows(con, header.ProjectId, profileId"
            ];

            string[] requiredSessionTokens =
            [
                "WHERE project_id = $1",
                "WHERE project_id = $1 AND (session_id = $2 OR (session_id IS NULL AND trace_id = $2))"
            ];

            var missing = new List<string>();

            foreach (var token in requiredProjectScopeTokens)
                if (!projectScopeText.Contains(token, StringComparison.Ordinal))
                    missing.Add($"{RootDirectory.GetRelativePathTo(projectScopeFile)} missing token: {token}");

            foreach (var token in requiredStoreTokens)
                if (!storeText.Contains(token, StringComparison.Ordinal))
                    missing.Add($"{RootDirectory.GetRelativePathTo(storeFile)} missing token: {token}");

            foreach (var token in requiredSessionTokens)
                if (!sessionsText.Contains(token, StringComparison.Ordinal))
                    missing.Add($"{RootDirectory.GetRelativePathTo(sessionsFile)} missing token: {token}");

            if (missing.Count is 0)
            {
                Log.Information("Collector storage reads are project-scoped");
                return;
            }

            foreach (var item in missing)
                Log.Error("  {Missing}", item);

            throw new InvalidOperationException(
                "Storage reads over spans, logs, profiles, and session summaries must include project_id scope. " +
                "project_id is stamped at ingestion and must remain a storage boundary.");
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
                "ProfileStackRow.SelectColumnList",
                "ModelPricingRow.SelectColumnList",
                "ModelPricingRow.MapFromReader"
            ];

            string[] forbiddenManualSelectTokens =
            [
                "SelectSpanColumns",
                "SELECT log_id, trace_id",
                "SELECT profile_id, trace_id",
                "SELECT profile_id, ordinal",
                "SELECT provider, model, input_cost"
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
                "ProfileStackRow.CreateTableDdl",
                "ModelPricingRow.CreateTableDdl"
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
                "CREATE TABLE IF NOT EXISTS profile_stacks",
                "CREATE TABLE IF NOT EXISTS model_pricing"
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

    Target VerifyCollectorStorageWritesUseGeneratedBatchHelper => d => d
        .Unlisted()
        .Description("Verify storage row writes use generated DuckDB insert helpers")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var storeFile = CollectorDirectory / "Storage" / "DuckDbStore.cs";
            if (!storeFile.FileExists())
                throw new InvalidOperationException("Missing DuckDbStore.cs storage implementation.");

            var text = File.ReadAllText(storeFile);

            string[] requiredGeneratedInsertHelpers =
            [
                "InsertRowsBatchedAsync(con, tx, logs, LogStorageRow.AddParameters",
                "InsertRowsBatchedAsync(con, tx, batch.Spans, SpanStorageRow.AddParameters",
                "InsertRowsBatchedAsync(con, tx, rows, ModelPricingRow.AddParameters",
                "ProfileStorageRow.AddParameters",
                "ProfileFunctionRow.AddParameters",
                "ProfileLocationRow.AddParameters",
                "ProfileMappingRow.AddParameters",
                "ProfileSampleRow.AddParameters",
                "ProfileStackRow.AddParameters"
            ];

            string[] forbiddenManualInsertLoopTokens =
            [
                "var totalLogs = logs.Count",
                "var totalSpans = spans.Count",
                "BuildMultiRowInsertSql(chunkSize)",
                "LogStorageRow.AddParameters(cmd, logs[offset + i])",
                "SpanStorageRow.AddParameters(cmd, spans[offset + i])",
                "MapSpan(reader)"
            ];

            var missing = requiredGeneratedInsertHelpers
                .Where(token => !text.Contains(token, StringComparison.Ordinal))
                .ToList();
            var forbidden = forbiddenManualInsertLoopTokens
                .Where(token => text.Contains(token, StringComparison.Ordinal))
                .ToList();

            if (missing.Count is 0 && forbidden.Count is 0)
            {
                Log.Information("Collector storage row writes use generated DuckDB insert helpers");
                return;
            }

            foreach (var token in missing)
                Log.Error("  Missing generated insert helper usage in DuckDbStore.cs: {Token}", token);

            foreach (var token in forbidden)
                Log.Error("  Manual storage row insert loop token found in DuckDbStore.cs: {Token}", token);

            throw new InvalidOperationException(
                "Do not handwrite per-row storage insert loops for generated row types. Use InsertRowsBatchedAsync " +
                "with each row type's generated AddParameters and BuildMultiRowInsertSql helpers.");
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

    Target VerifyOtlpConverterUsesCentralizedSemanticProjection => d => d
        .Unlisted()
        .Description("Verify OTLP span semantic storage projection stays centralized")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var converterFile = CollectorDirectory / "Ingestion" / "OtlpConverter.cs";
            if (!converterFile.FileExists())
                throw new FileNotFoundException("Missing OTLP converter", converterFile.ToString());

            var converterText = File.ReadAllText(converterFile);

            string[] forbidden =
            [
                "Qyl.OpenTelemetry.SemanticConventions",
                "using GenAiAttributes",
                "QylGenAiCostProcessor",
                "GenAiAttributes.",
                "IsGenAiStorageAttribute",
                "ExtractGenAiAttributes",
                "private static bool ShouldConvertSpanAttribute",
                "readonly record struct GenAiData"
            ];

            var offenders = forbidden
                .Where(token => converterText.Contains(token, StringComparison.Ordinal))
                .ToList();

            string[] required =
            [
                "CollectorSemanticAttributeCatalog.ServiceName",
                "AttributeKeySets.ShouldConvertSpanAttribute",
                "AttributeKeySets.ExtractSpanStorageProjection"
            ];

            var missing = required
                .Where(token => !converterText.Contains(token, StringComparison.Ordinal))
                .ToList();

            if (offenders.Count is 0 && missing.Count is 0)
            {
                Log.Information("OTLP span semantic storage projection is centralized");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Converter-local semantic projection token '{Token}' found in {File}",
                    offender,
                    RootDirectory.GetRelativePathTo(converterFile));

            foreach (var requirement in missing)
                Log.Error("  Missing centralized projection token '{Token}' in {File}",
                    requirement,
                    RootDirectory.GetRelativePathTo(converterFile));

            throw new InvalidOperationException(
                "Do not handwire GenAI or session span-storage projection in OtlpConverter. " +
                "Centralize semantic key policy in AttributeKeySets and consume it from the converter.");
        });

    Target VerifyCollectorSpanIdentityIsComposite => d => d
        .Unlisted()
        .Description("Verify span storage identity is project- and trace-scoped")
        .OnlyWhenDynamic(() => SkipVerify != true)
        .Executes(() =>
        {
            var spanStorageRowFile = CollectorDirectory / "Storage" / "DuckDbReaderExtensions.cs";
            var storeFile = CollectorDirectory / "Storage" / "DuckDbStore.cs";

            string[] forbidden =
            [
                "PRIMARY KEY (span_id)",
                "PRIMARY KEY (\"span_id\")",
                "ON CONFLICT (span_id)",
                "ON CONFLICT (trace_id, span_id)"
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
            if (!spanStorageRowText.Contains("[DuckDbColumn(PrimaryKeyOrdinal = 0, SqlType = \"VARCHAR(128)\")]\n    public required string ProjectId", StringComparison.Ordinal) ||
                !spanStorageRowText.Contains("[DuckDbColumn(PrimaryKeyOrdinal = 1)]\n    public required string TraceId", StringComparison.Ordinal) ||
                !spanStorageRowText.Contains("[DuckDbColumn(PrimaryKeyOrdinal = 2)]\n    public required string SpanId", StringComparison.Ordinal))
            {
                missingRequired.Add(RootDirectory.GetRelativePathTo(spanStorageRowFile).ToString()
                                    + " must declare generated PRIMARY KEY order ProjectId=0, TraceId=1, SpanId=2");
            }

            if (!spanStorageRowText.Contains("ON CONFLICT (project_id, trace_id, span_id)", StringComparison.Ordinal))
            {
                missingRequired.Add(RootDirectory.GetRelativePathTo(spanStorageRowFile).ToString()
                                    + " must upsert ON CONFLICT (project_id, trace_id, span_id)");
            }

            if (!storeFile.FileExists() ||
                !File.ReadAllText(storeFile).Contains("SpanStorageRow.CreateTableDdl", StringComparison.Ordinal))
            {
                missingRequired.Add(RootDirectory.GetRelativePathTo(storeFile).ToString()
                                    + " must initialize spans through SpanStorageRow.CreateTableDdl");
            }

            if (offenders.Count is 0 && missingRequired.Count is 0)
            {
                Log.Information("Collector span storage identity is project- and trace-scoped");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Span identity regression token '{Token}' found in {File}", offender.Token, offender.File);

            foreach (var missing in missingRequired)
                Log.Error("  Missing span identity invariant: {Invariant}", missing);

            throw new InvalidOperationException(
                "Span ids are unique only within traces and tenants. Store and upsert spans by " +
                "(project_id, trace_id, span_id), never span_id alone.");
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
                "DashboardBuildDescriptor",
                "DashboardBuildDescriptorReader",
                "LegacyEntryAssetMarker",
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
                "ModelPricingDdl",
                "ModelPricingTiersDdl",
                "CostByModelHourlyViewDdl",
                "model_pricing_tiers",
                "cost_by_model_hourly",
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
                "status_code VARCHAR NOT NULL",
                "\"\"\"{\"error\":\"Unauthorized\"",
                "Valid x-otlp-api-key header required",
                "SseItem<",
                "ServerSentEventsResult<"
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

            string[] removedCollectorRoutePrefixes =
            [
                "/observe",
                "/metrics",
                "/logs/live",
                "/sessions/{sessionId}/spans",
                "/traces/{traceId}/profiles",
                "/genai",
                "/telemetry",
                "/meta"
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
                RootDirectory / "services" / "qyl.dashboard" / "src" / "lib" / "semconv.ts",
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

            var collectorRouteOffenders = CollectorSourceFiles()
                .SelectMany(file => RemovedRouteLiterals(file, removedCollectorRoutePrefixes))
                .ToList();

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
                Log.Error("  Removed collector route '{Route}' found in {File}:{Line}",
                    offender.Route, offender.File, offender.Line);

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
        .DependsOn<ICollectorSemanticCatalog>(static x => x.VerifyCollectorSemanticAttributeCatalog)
        .DependsOn<ICollectorSemanticCatalog>(static x => x.VerifyCollectorSemanticPolicyIsCatalogBacked)
        .DependsOn(VerifyCollectorMetricTagsAreBounded)
        .DependsOn(VerifyCollectorSessionFacetsAreBounded)
        .DependsOn(VerifyInstrumentationHasNoStorageTenantKnowledge)
        .DependsOn(VerifyCollectorStorageReadsAreProjectScoped)
        .DependsOn(VerifyCollectorDuckDbAccessIsStorageOnly)
        .DependsOn(VerifyCollectorStorageReadsUseGeneratedColumnLists)
        .DependsOn(VerifyCollectorStorageTablesUseGeneratedDdl)
        .DependsOn(VerifyCollectorStorageWritesUseGeneratedBatchHelper)
        .DependsOn(VerifyNoHandwrittenOtlpWireParser)
        .DependsOn(VerifyOtlpConverterHotPath)
        .DependsOn(VerifyOtlpConverterUsesCentralizedSemanticProjection)
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
            Log.Information("  Collector semantic attribute catalog matches package references");
            Log.Information("  Collector semantic policy is backed by the generated catalog");
            Log.Information("  Collector metric tags are bounded");
            Log.Information("  Collector session facets are bounded");
            Log.Information("  Instrumentation packages are storage- and tenant-blind");
            Log.Information("  Collector storage reads are project-scoped");
            Log.Information("  Collector DuckDB access stays behind storage intent methods");
            Log.Information("  Collector storage row reads use generated DuckDB column lists");
            Log.Information("  Collector storage row tables use generated DuckDB DDL");
            Log.Information("  Collector storage row writes use generated DuckDB insert helpers");
            Log.Information("  Collector OTLP wire contracts use generated protobuf types");
            Log.Information("  OTLP converter hot path avoids removed allocation patterns");
            Log.Information("  OTLP span semantic storage projection is centralized");
            Log.Information("  Collector span storage identity is project- and trace-scoped");
            Log.Information("  Removed local build surfaces stayed removed");
            Log.Information("═══════════════════════════════════════════════════════════════");
        });

    private readonly record struct DeclaredType(
        string File,
        string Path,
        int Line,
        string Namespace,
        string Name,
        string Kind,
        bool IsPublic,
        bool IsStatic,
        bool IsSealed,
        bool IsPartial);

    private readonly record struct ForbiddenEndpointMapper(
        string File,
        int Line,
        string Text);

    private readonly record struct SemanticUsageOffender(
        string File,
        int Line,
        string Kind,
        string Text);

    private readonly record struct RemovedRouteLiteral(
        string File,
        int Line,
        string Route);

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
        var root = ParseCompilationUnit(file);
        var relativePath = RootDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');

        foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var line = declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var modifiers = declaration.Modifiers;
            yield return new DeclaredType(
                RootDirectory.GetRelativePathTo(file).ToString(),
                relativePath,
                line,
                ResolveNamespace(declaration),
                declaration.Identifier.ValueText,
                GetTypeKind(declaration),
                modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword)),
                modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)),
                modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.SealedKeyword)),
                modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
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

        static string GetTypeKind(BaseTypeDeclarationSyntax declaration) =>
            declaration switch
            {
                ClassDeclarationSyntax => "class",
                RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => "record struct",
                RecordDeclarationSyntax => "record",
                StructDeclarationSyntax => "struct",
                EnumDeclarationSyntax => "enum",
                _ => declaration.Kind().ToString()
            };
    }

    private IEnumerable<ForbiddenEndpointMapper> ForbiddenEndpointMappers(AbsolutePath file)
    {
        var relativePath = RootDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');
        if (relativePath.EndsWith("services/qyl.collector/Hosting/CollectorEndpointExtensions.cs", StringComparison.Ordinal))
            yield break;

        var root = ParseCompilationUnit(file);

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (InvocationName(invocation.Expression) is "MapQylGeneratedEndpoints")
                yield return new ForbiddenEndpointMapper(relativePath, NodeLine(invocation), NodePreview(invocation));
        }

        foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            if (attribute.Name.ToString().Contains("QylMapEndpoints", StringComparison.Ordinal))
                yield return new ForbiddenEndpointMapper(relativePath, NodeLine(attribute), NodePreview(attribute));
        }

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword)) ||
                !method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
            {
                continue;
            }

            var name = method.Identifier.ValueText;
            if (name.StartsWith("Map", StringComparison.Ordinal) &&
                name.Contains("Endpoint", StringComparison.Ordinal))
            {
                yield return new ForbiddenEndpointMapper(relativePath, NodeLine(method), NodePreview(method));
            }
        }
    }

    private IEnumerable<SemanticUsageOffender> SemanticUsageOffenders(AbsolutePath file)
    {
        var root = ParseCompilationUnit(file);
        var relativePath = RootDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');

        foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var name = usingDirective.Name?.ToString();
            if (IsForbiddenSemConvAttributeNamespace(name))
                yield return CreateOffender(usingDirective, "Direct semantic attribute import");
        }

        foreach (var name in root.DescendantNodes().OfType<NameSyntax>())
        {
            var text = name.ToString();
            if (IsForbiddenSemConvAttributeNamespace(text))
                yield return CreateOffender(name, "Direct semantic attribute reference");
        }

        foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                literal.Token.ValueText is { } value &&
                IsSemanticAttributeLiteral(value))
            {
                yield return CreateOffender(literal, "Raw semantic attribute literal");
            }
        }

        foreach (var interpolatedText in root.DescendantNodes().OfType<InterpolatedStringTextSyntax>())
        {
            var value = interpolatedText.TextToken.ValueText;
            if (IsSemanticAttributeLiteral(value))
                yield return CreateOffender(interpolatedText, "Raw semantic attribute literal");
        }

        SemanticUsageOffender CreateOffender(SyntaxNode node, string kind) =>
            new(relativePath, NodeLine(node), kind, NodePreview(node));
    }

    private IEnumerable<RemovedRouteLiteral> RemovedRouteLiterals(
        AbsolutePath file,
        IReadOnlyCollection<string> removedRoutePrefixes)
    {
        var root = ParseCompilationUnit(file);
        var relativePath = RootDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (InvocationName(invocation.Expression) is not { } invocationName ||
                !invocationName.StartsWith("Map", StringComparison.Ordinal))
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax literal ||
                !literal.IsKind(SyntaxKind.StringLiteralExpression) ||
                literal.Token.ValueText is not { Length: > 0 } route)
            {
                continue;
            }

            if (removedRoutePrefixes.Any(prefix => route.StartsWith(prefix, StringComparison.Ordinal)))
                yield return new RemovedRouteLiteral(relativePath, NodeLine(literal), route);
        }
    }

    private static CompilationUnitSyntax ParseCompilationUnit(AbsolutePath file)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file.ToString());
        return tree.GetCompilationUnitRoot();
    }

    private static bool IsAllowedPublicCollectorType(DeclaredType declaration) =>
        declaration.Name is "Program" &&
        declaration.Kind is "class" &&
        declaration.IsPartial &&
        declaration.Path.EndsWith("services/qyl.collector/Program.cs", StringComparison.Ordinal);

    private static string? InvocationName(ExpressionSyntax expression) =>
        expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };

    private static int NodeLine(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string NodePreview(SyntaxNode node)
    {
        var line = node.ToString()
            .Split('\n', 2)[0]
            .Replace('\r', ' ')
            .Trim();

        return line.Length <= 160 ? line : line[..160];
    }

    private static bool IsForbiddenSemConvAttributeNamespace(string? name) =>
        name is not null &&
        (name.StartsWith("Qyl.OpenTelemetry.SemanticConventions.Attributes", StringComparison.Ordinal) ||
         name.StartsWith("Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes", StringComparison.Ordinal));

    private static bool IsSemanticAttributeLiteral(string value)
    {
        string[] exact =
        [
            "error.type",
            "meter.name",
            "session.id",
            "user.id"
        ];

        if (exact.Contains(value, StringComparer.Ordinal))
            return true;

        string[] prefixes =
        [
            "client.",
            "code.",
            "db.",
            "deployment.",
            "enduser.",
            "exception.",
            "gen_ai.",
            "host.",
            "http.",
            "mcp.",
            "messaging.",
            "os.",
            "profile.",
            "otel.scope.",
            "qyl.capability.",
            "server.",
            "service.",
            "url."
        ];

        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
    }
}
