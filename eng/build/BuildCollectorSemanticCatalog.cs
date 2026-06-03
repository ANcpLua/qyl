using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Qyl.Build;

interface ICollectorSemanticCatalog : IHazSourcePaths
{
    const string StablePackageId = "Qyl.OpenTelemetry.SemanticConventions";
    const string IncubatingPackageId = "Qyl.OpenTelemetry.SemanticConventions.Incubating";

    AbsolutePath CollectorSemanticCatalogFile =>
        CollectorDirectory / "Ingestion" / "Generated" / "CollectorSemanticAttributeCatalog.g.cs";

    Target GenerateCollectorSemanticAttributeCatalog => d => d
        .Description("Generate collector semantic attribute catalog from Qyl.OpenTelemetry.SemanticConventions")
        .Executes(() =>
        {
            CollectorSemanticCatalogFile.Parent.CreateDirectory();
            var generated = GenerateCollectorSemanticAttributeCatalogText();
            if (!CollectorSemanticCatalogFile.FileExists() ||
                !string.Equals(File.ReadAllText(CollectorSemanticCatalogFile), generated, StringComparison.Ordinal))
            {
                File.WriteAllText(CollectorSemanticCatalogFile, generated);
            }

            Log.Information(
                "Generated collector semantic attribute catalog: {File}",
                RootDirectory.GetRelativePathTo(CollectorSemanticCatalogFile));
        });

    Target VerifyCollectorSemanticAttributeCatalog => d => d
        .Unlisted()
        .Description("Verify collector semantic attribute catalog is generated from package references")
        .Executes(() =>
        {
            if (!CollectorSemanticCatalogFile.FileExists())
                throw new FileNotFoundException(
                    "Missing generated collector semantic attribute catalog. Run `./eng/build.sh GenerateCollectorSemanticAttributeCatalog`.",
                    CollectorSemanticCatalogFile.ToString());

            var expected = GenerateCollectorSemanticAttributeCatalogText();
            var actual = File.ReadAllText(CollectorSemanticCatalogFile);

            if (string.Equals(actual, expected, StringComparison.Ordinal))
            {
                Log.Information("Collector semantic attribute catalog matches Qyl.OpenTelemetry.SemanticConventions");
                return;
            }

            throw new InvalidOperationException(
                "Generated collector semantic attribute catalog is stale. " +
                "Run `./eng/build.sh GenerateCollectorSemanticAttributeCatalog` and commit the result.");
        });

    Target VerifyCollectorSemanticPolicyIsCatalogBacked => d => d
        .Unlisted()
        .Description("Verify collector runtime semantic policy uses the generated catalog")
        .Executes(() =>
        {
            var policyFile = CollectorDirectory / "Ingestion" / "AttributeKeySets.cs";
            if (!policyFile.FileExists())
                throw new FileNotFoundException("Missing collector semantic policy", policyFile.ToString());

            var text = File.ReadAllText(policyFile);
            string[] forbidden =
            [
                "Qyl.OpenTelemetry.SemanticConventions",
                "QylGenAiCostProcessor",
                "BuildAttributeSet",
                "AttributesFrom",
                "BindingFlags",
                "DynamicallyAccessedMembers",
                "typeof(",
                "GenAiAttributes.",
                "DbAttributes",
                "ErrorAttributes",
                "ExceptionAttributes",
                "HttpAttributes",
                "MessagingAttributes",
                "ProfileAttributes",
                "ServiceAttributes",
                "SessionAttributes"
            ];

            var offenders = forbidden
                .Where(token => text.Contains(token, StringComparison.Ordinal))
                .ToList();

            string[] required =
            [
                "CollectorSemanticAttributeCatalog.SpanAttributeAllowList",
                "CollectorSemanticAttributeCatalog.LogAttributeAllowList",
                "CollectorSemanticAttributeCatalog.ProfileAttributeAllowList",
                "CollectorSemanticAttributeCatalog.ResourceAttributeAllowList",
                "CollectorSemanticAttributeCatalog.ProjectIdResourceKeys",
                "CollectorSemanticAttributeCatalog.SessionCorrelation",
                "CollectorSemanticAttributeCatalog.QylResourceAttributeAllowList",
                "CollectorSemanticAttributeCatalog.DeniedExactKeys",
                "CollectorSemanticAttributeCatalog.DeniedKeyTokens",
                "CollectorSemanticAttributeCatalog.SpanStorageProjectionKeys",
                "CollectorSemanticAttributeCatalog.GenAiProviderName"
            ];

            var missing = required
                .Where(token => !text.Contains(token, StringComparison.Ordinal))
                .ToList();

            if (offenders.Count is 0 && missing.Count is 0)
            {
                Log.Information("Collector semantic policy is backed by the generated catalog");
                return;
            }

            foreach (var offender in offenders)
                Log.Error("  Runtime semantic policy handwiring token '{Token}' found in {File}",
                    offender,
                    RootDirectory.GetRelativePathTo(policyFile));

            foreach (var requirement in missing)
                Log.Error("  Runtime semantic policy missing catalog token '{Token}' in {File}",
                    requirement,
                    RootDirectory.GetRelativePathTo(policyFile));

            throw new InvalidOperationException(
                "Do not handwire OpenTelemetry semantic convention type lists in AttributeKeySets. " +
                "Generate CollectorSemanticAttributeCatalog.g.cs from Qyl.OpenTelemetry.SemanticConventions and consume it there.");
        });

    string GenerateCollectorSemanticAttributeCatalogText()
    {
        var resolver = new SemConvAttributeResolver(ReadResolvedPackageAssemblies());
        var allAttributeValues = resolver.AllAttributeValues();
        var stableAttributeValues = resolver.AttributeValues(ICollectorSemanticCatalog.StablePackageId);
        var incubatingAttributeValues = resolver.AttributeValues(ICollectorSemanticCatalog.IncubatingPackageId);

        var spanAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, "db.", "error.", "exception.", "http.", "otel.", "server."),
            ValuesWithPrefixes(incubatingAttributeValues, "gen_ai.", "messaging.", "profile."));

        var logAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, "error.", "exception.", "http.", "otel.", "server."),
            ValuesWithPrefixes(incubatingAttributeValues, "gen_ai.", "messaging."));

        var profileAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, "error.", "otel."),
            ValuesWithPrefixes(incubatingAttributeValues, "gen_ai.", "profile."));

        var resourceAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, "deployment.", "service."),
            ValuesWithPrefixes(incubatingAttributeValues, "host.", "os."));

        var qylResourceAttributeAllowList = new[]
        {
            "qyl.capability.id",
            "qyl.capability.kind"
        };

        var projectIdResourceKeys = new[]
        {
            "qyl.project.id",
            "qyl.workspace.id"
        };

        var sessionCorrelation = resolver.RequiredAttributeValues(
            "gen_ai.conversation.id",
            "mcp.session.id",
            "session.id");

        var deniedExactKeys = ValuesWithPrefixes(allAttributeValues, "enduser.", "user.")
            .Concat(resolver.RequiredAttributeValues(
                "code.file.path",
                "code.stacktrace",
                "db.query.text",
                "exception.message",
                "exception.stacktrace",
                "gen_ai.agent.description",
                "gen_ai.agent.id",
                "gen_ai.agent.name",
                "gen_ai.conversation.id",
                "gen_ai.data_source.id",
                "gen_ai.evaluation.explanation",
                "gen_ai.evaluation.name",
                "gen_ai.input.messages",
                "gen_ai.output.messages",
                "gen_ai.prompt.name",
                "gen_ai.response.id",
                "gen_ai.retrieval.documents",
                "gen_ai.retrieval.query.text",
                "gen_ai.system_instructions",
                "gen_ai.tool.call.arguments",
                "http.request.header",
                "http.response.header",
                "gen_ai.tool.call.id",
                "gen_ai.tool.call.result",
                "gen_ai.tool.definitions",
                "gen_ai.tool.description",
                "gen_ai.workflow.name",
                "host.id",
                "host.ip",
                "host.mac",
                "host.name",
                "mcp.session.id",
                "service.instance.id",
                "session.id",
                "session.previous_id",
                "url.full",
                "url.query"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        var deniedKeyTokens = new[]
        {
            "access_token",
            "api-key",
            "api_key",
            "apikey",
            "authorization",
            "body",
            "completion",
            "cookie",
            "credential",
            "definition",
            "document",
            "fingerprint",
            "id_token",
            "instruction",
            "jwt",
            "message",
            "password",
            "private_key",
            "prompt",
            "query",
            "refresh_token",
            "result",
            "secret",
            "set-cookie",
            "token"
        };

        var spanStorageProjectionKeys = resolver.RequiredAttributeValues(
                "gen_ai.provider.name",
                "gen_ai.request.model",
                "gen_ai.response.model",
                "gen_ai.usage.input_tokens",
                "gen_ai.usage.output_tokens",
                "gen_ai.request.temperature",
                "gen_ai.response.finish_reasons",
                "gen_ai.tool.name")
            .Append("gen_ai.usage.cost")
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        var genAiProjection = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GenAiProviderName"] = resolver.RequiredAttributeValue("gen_ai.provider.name"),
            ["GenAiRequestModel"] = resolver.RequiredAttributeValue("gen_ai.request.model"),
            ["GenAiResponseModel"] = resolver.RequiredAttributeValue("gen_ai.response.model"),
            ["GenAiInputTokens"] = resolver.RequiredAttributeValue("gen_ai.usage.input_tokens"),
            ["GenAiOutputTokens"] = resolver.RequiredAttributeValue("gen_ai.usage.output_tokens"),
            ["GenAiTemperature"] = resolver.RequiredAttributeValue("gen_ai.request.temperature"),
            ["GenAiStopReason"] = resolver.RequiredAttributeValue("gen_ai.response.finish_reasons"),
            ["GenAiToolName"] = resolver.RequiredAttributeValue("gen_ai.tool.name"),
            ["GenAiCostUsd"] = "gen_ai.usage.cost",
            ["HttpRequestMethod"] = resolver.RequiredAttributeValue("http.request.method"),
            ["HttpRoute"] = resolver.RequiredAttributeValue("http.route"),
            ["SchemaUrlCurrent"] = resolver.SchemaUrlCurrent(),
            ["ServiceName"] = resolver.RequiredAttributeValue("service.name")
        };

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine("// Generated by eng/build GenerateCollectorSemanticAttributeCatalog from Qyl.OpenTelemetry.SemanticConventions package references.");
        builder.AppendLine("// </auto-generated>");
        builder.AppendLine();
        builder.AppendLine("using System.Collections.Frozen;");
        builder.AppendLine();
        builder.AppendLine("namespace Qyl.Collector.Ingestion;");
        builder.AppendLine();
        builder.AppendLine("internal static class CollectorSemanticAttributeCatalog");
        builder.AppendLine("{");
        builder.AppendLine("    internal const string BaggagePrefix = \"baggage.\";");
        builder.AppendLine();

        WriteFrozenSet(builder, "SessionCorrelation", sessionCorrelation, "StringComparer.Ordinal");
        WriteFrozenSet(builder, "ProjectIdResourceKeys", projectIdResourceKeys, "StringComparer.Ordinal");
        WriteFrozenSet(builder, "QylResourceAttributeAllowList", qylResourceAttributeAllowList, "StringComparer.Ordinal");
        WriteFrozenSet(builder, "SpanAttributeAllowList", spanAttributeAllowList, "StringComparer.Ordinal");
        WriteFrozenSet(builder, "LogAttributeAllowList", logAttributeAllowList, "StringComparer.Ordinal");
        WriteFrozenSet(builder, "ProfileAttributeAllowList", profileAttributeAllowList, "StringComparer.Ordinal");
        WriteFrozenSet(builder, "ResourceAttributeAllowList", resourceAttributeAllowList, "StringComparer.Ordinal");
        WriteFrozenSet(builder, "DeniedExactKeys", deniedExactKeys, "StringComparer.OrdinalIgnoreCase");
        WriteStringArray(builder, "DeniedKeyTokens", deniedKeyTokens);
        WriteFrozenSet(builder, "SpanStorageProjectionKeys", spanStorageProjectionKeys, "StringComparer.Ordinal");

        foreach (var projection in genAiProjection.OrderBy(static item => item.Key, StringComparer.Ordinal))
            builder.Append("    internal const string ")
                .Append(projection.Key)
                .Append(" = ")
                .Append(StringLiteral(projection.Value))
                .AppendLine(";");

        builder.AppendLine("}");
        return builder.ToString();
    }

    private IReadOnlyDictionary<string, string> ReadResolvedPackageAssemblies() =>
        ProjectAssetsPackageResolver.ResolvePackageAssemblies(
            LocateBuildProjectAssetsFile(),
            ICollectorSemanticCatalog.StablePackageId,
            ICollectorSemanticCatalog.IncubatingPackageId);

    private AbsolutePath LocateBuildProjectAssetsFile()
    {
        var expected = RootDirectory / "eng" / "build" / "artifacts" / "obj" / "build" / "project.assets.json";
        if (expected.FileExists())
            return expected;

        var candidates = (RootDirectory / "eng" / "build")
            .GlobFiles("**/project.assets.json")
            .OrderBy(static path => path.ToString(), StringComparer.Ordinal)
            .ToList();

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new FileNotFoundException(
                "MSBuild restore graph for eng/build/build.csproj was not found. Run `dotnet restore eng/build/build.csproj`."),
            _ => throw new InvalidOperationException(
                "Multiple eng/build project.assets.json files were found; refusing to guess the semantic convention package graph: " +
                string.Join(", ", candidates.Select(path => RootDirectory.GetRelativePathTo(path))))
        };
    }

    private static string[] ValuesWithPrefixes(IEnumerable<string> values, params string[] prefixes) =>
        values
            .Where(value => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

    private static string[] NormalizedValues(params IEnumerable<string>[] values) =>
        values
            .SelectMany(static value => value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static void WriteFrozenSet(
        StringBuilder builder,
        string name,
        IEnumerable<string> values,
        string comparer)
    {
        builder.Append("    internal static readonly FrozenSet<string> ")
            .Append(name)
            .AppendLine(" = FrozenSet.Create(");
        builder.Append("        ").Append(comparer);

        foreach (var value in values.Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal))
            builder.AppendLine(",").Append("        ").Append(StringLiteral(value));

        builder.AppendLine(");");
        builder.AppendLine();
    }

    private static void WriteStringArray(StringBuilder builder, string name, IEnumerable<string> values)
    {
        builder.Append("    internal static readonly string[] ").Append(name).AppendLine(" =");
        builder.AppendLine("    [");
        foreach (var value in values.Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal))
            builder.Append("        ").Append(StringLiteral(value)).AppendLine(",");
        builder.AppendLine("    ];");
        builder.AppendLine();
    }

    private static string StringLiteral(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

internal sealed class SemConvAttributeResolver(IReadOnlyDictionary<string, string> packageAssemblyPaths)
{
    private readonly Dictionary<string, Assembly> _assemblies = new(StringComparer.Ordinal);
    private string[]? _allAttributeValues;

    public string[] AllAttributeValues() =>
        _allAttributeValues ??=
        [
            .. AttributeValuesFromPackage(ICollectorSemanticCatalog.StablePackageId)
                .Concat(AttributeValuesFromPackage(ICollectorSemanticCatalog.IncubatingPackageId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
        ];

    public string[] AttributeValues(string packageId) =>
        AttributeValuesFromPackage(packageId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    public string[] RequiredAttributeValues(params string[] values) =>
        values.Select(RequiredAttributeValue).ToArray();

    public string RequiredAttributeValue(string value)
    {
        if (!AllAttributeValues().Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"Semantic convention attribute key '{value}' is not present in the configured package references.");

        return value;
    }

    public string SchemaUrlCurrent()
    {
        var assembly = ResolveAssembly(ICollectorSemanticCatalog.StablePackageId);
        var type = assembly.GetType("Qyl.OpenTelemetry.SemanticConventions.SchemaUrl", throwOnError: true)!;
        var field = type.GetField("Current", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (field is not { IsLiteral: true, IsInitOnly: false } || field.FieldType != typeof(string))
        {
            throw new InvalidOperationException(
                "Semantic convention SchemaUrl.Current is not a public string constant.");
        }

        return (string)field.GetRawConstantValue()!;
    }

    private IEnumerable<string> AttributeValuesFromPackage(string packageId) =>
        ResolveAssembly(packageId)
            .GetTypes()
            .Where(static type =>
                type.FullName is not null &&
                type.FullName.Contains(".Attributes.", StringComparison.Ordinal) &&
                type.Name.EndsWith("Attributes", StringComparison.Ordinal))
            .SelectMany(AttributeValuesFromType);

    private static IEnumerable<string> AttributeValuesFromType(Type type) =>
        type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(static field => field is { IsLiteral: true, IsInitOnly: false } &&
                                   field.FieldType == typeof(string))
            .Select(static field => (string)field.GetRawConstantValue()!);

    private Assembly ResolveAssembly(string packageId)
    {
        if (_assemblies.TryGetValue(packageId, out var assembly))
            return assembly;

        try
        {
            assembly = Assembly.Load(new AssemblyName(packageId));
            _assemblies.Add(packageId, assembly);
            return assembly;
        }
        catch (FileNotFoundException)
        {
        }

        if (!packageAssemblyPaths.TryGetValue(packageId, out var assemblyPath))
        {
            throw new FileNotFoundException(
                $"Resolved package assembly for '{packageId}' was not found in eng/build/build.csproj restore assets. " +
                "Run `dotnet restore eng/build/build.csproj`.");
        }

        assembly = Assembly.LoadFrom(assemblyPath);
        _assemblies.Add(packageId, assembly);
        return assembly;
    }
}

internal static class ProjectAssetsPackageResolver
{
    public static IReadOnlyDictionary<string, string> ResolvePackageAssemblies(
        string assetsFile,
        params string[] packageIds)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsFile));
        var root = document.RootElement;
        var packageFolders = root.GetProperty("packageFolders")
            .EnumerateObject()
            .Select(static folder => folder.Name)
            .ToArray();

        if (packageFolders.Length is 0)
            throw new InvalidOperationException($"Restore assets file '{assetsFile}' contains no packageFolders.");

        var requested = packageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var target in root.GetProperty("targets").EnumerateObject())
        {
            foreach (var library in target.Value.EnumerateObject())
            {
                var slashIndex = library.Name.IndexOf('/', StringComparison.Ordinal);
                if (slashIndex <= 0 || slashIndex == library.Name.Length - 1)
                    continue;

                var packageId = library.Name[..slashIndex];
                if (!requested.Contains(packageId) || resolved.ContainsKey(packageId))
                    continue;

                var version = library.Name[(slashIndex + 1)..];
                var assemblyPath = ResolveCompileAssembly(
                    packageFolders,
                    packageId,
                    version,
                    library.Value);

                resolved.Add(packageId, assemblyPath);
            }
        }

        var missing = packageIds
            .Where(packageId => !resolved.ContainsKey(packageId))
            .ToArray();

        if (missing.Length is not 0)
            throw new InvalidOperationException(
                $"Restore assets file '{assetsFile}' does not resolve required package(s): {string.Join(", ", missing)}.");

        return resolved;
    }

    private static string ResolveCompileAssembly(
        IReadOnlyList<string> packageFolders,
        string packageId,
        string version,
        JsonElement library)
    {
        if (!library.TryGetProperty("compile", out var compileAssets))
            throw new InvalidOperationException($"Package '{packageId}/{version}' has no compile assets in restore graph.");

        var dllFileName = packageId + ".dll";
        var relativeAssetPath = compileAssets
            .EnumerateObject()
            .Select(static asset => asset.Name)
            .Where(static asset => !asset.EndsWith("/_._", StringComparison.Ordinal))
            .FirstOrDefault(asset => string.Equals(Path.GetFileName(asset), dllFileName, StringComparison.Ordinal));

        if (relativeAssetPath is null)
            throw new FileNotFoundException($"Package '{packageId}/{version}' has no compile asset named '{dllFileName}'.");

        var packageDirectoryName = packageId.ToLowerInvariant();
        var assemblyPath = packageFolders
            .Select(packageFolder => Path.Combine(packageFolder, packageDirectoryName, version, relativeAssetPath))
            .FirstOrDefault(File.Exists);

        if (assemblyPath is null)
            throw new FileNotFoundException(
                $"Resolved compile asset '{relativeAssetPath}' for package '{packageId}/{version}' was not found under packageFolders from restore assets.");

        return assemblyPath;
    }
}
