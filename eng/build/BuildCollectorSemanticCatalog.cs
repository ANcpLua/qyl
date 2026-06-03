using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
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
            File.WriteAllText(CollectorSemanticCatalogFile, GenerateCollectorSemanticAttributeCatalogText());
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
                "CollectorSemanticAttributeCatalog.DeniedExactKeys",
                "CollectorSemanticAttributeCatalog.SpanStorageProjectionKeys"
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
        var resolver = new SemConvAttributeResolver(ReadCentralPackageVersions());

        var spanAttributeAllowList = resolver.ValuesFromTypes(
            StableAttributes("Db"),
            StableAttributes("Error"),
            StableAttributes("Exception"),
            IncubatingAttributes("GenAi"),
            StableAttributes("Http"),
            IncubatingAttributes("Messaging"),
            StableAttributes("Otel"),
            IncubatingAttributes("Profile"),
            StableAttributes("Server"));

        var logAttributeAllowList = resolver.ValuesFromTypes(
            StableAttributes("Error"),
            StableAttributes("Exception"),
            IncubatingAttributes("GenAi"),
            StableAttributes("Http"),
            IncubatingAttributes("Messaging"),
            StableAttributes("Otel"),
            StableAttributes("Server"));

        var profileAttributeAllowList = resolver.ValuesFromTypes(
            StableAttributes("Error"),
            IncubatingAttributes("GenAi"),
            StableAttributes("Otel"),
            IncubatingAttributes("Profile"));

        var resourceAttributeAllowList = resolver.ValuesFromTypes(
            StableAttributes("Deployment"),
            IncubatingAttributes("Host"),
            IncubatingAttributes("Os"),
            StableAttributes("Service"));

        var qylResourceAttributeAllowList = new[]
        {
            "qyl.capability.id",
            "qyl.capability.kind"
        };

        var sessionCorrelation = resolver.ValuesFromMembers(
            Member(IncubatingAttributes("GenAi"), "ConversationId"),
            Member(IncubatingAttributes("Mcp"), "SessionId"),
            Member(IncubatingAttributes("Session"), "Id"));

        var deniedExactKeys = resolver.ValuesFromTypes(
                IncubatingAttributes("Enduser"),
                IncubatingAttributes("User"))
            .Concat(resolver.ValuesFromMembers(
                Member(StableAttributes("Code"), "FilePath"),
                Member(StableAttributes("Code"), "Stacktrace"),
                Member(StableAttributes("Db"), "QueryText"),
                Member(StableAttributes("Exception"), "Message"),
                Member(StableAttributes("Exception"), "Stacktrace"),
                Member(IncubatingAttributes("GenAi"), "AgentDescription"),
                Member(IncubatingAttributes("GenAi"), "AgentId"),
                Member(IncubatingAttributes("GenAi"), "AgentName"),
                Member(IncubatingAttributes("GenAi"), "ConversationId"),
                Member(IncubatingAttributes("GenAi"), "DataSourceId"),
                Member(IncubatingAttributes("GenAi"), "EvaluationExplanation"),
                Member(IncubatingAttributes("GenAi"), "EvaluationName"),
                Member(IncubatingAttributes("GenAi"), "InputMessages"),
                Member(IncubatingAttributes("GenAi"), "OutputMessages"),
                Member(IncubatingAttributes("GenAi"), "PromptName"),
                Member(IncubatingAttributes("GenAi"), "ResponseId"),
                Member(IncubatingAttributes("GenAi"), "RetrievalDocuments"),
                Member(IncubatingAttributes("GenAi"), "RetrievalQueryText"),
                Member(IncubatingAttributes("GenAi"), "SystemInstructions"),
                Member(IncubatingAttributes("GenAi"), "ToolCallArguments"),
                Member(StableAttributes("Http"), "RequestHeader"),
                Member(StableAttributes("Http"), "ResponseHeader"),
                Member(IncubatingAttributes("GenAi"), "ToolCallId"),
                Member(IncubatingAttributes("GenAi"), "ToolCallResult"),
                Member(IncubatingAttributes("GenAi"), "ToolDefinitions"),
                Member(IncubatingAttributes("GenAi"), "ToolDescription"),
                Member(IncubatingAttributes("GenAi"), "WorkflowName"),
                Member(IncubatingAttributes("Mcp"), "SessionId"),
                Member(StableAttributes("Service"), "InstanceId"),
                Member(IncubatingAttributes("Session"), "Id"),
                Member(IncubatingAttributes("Session"), "PreviousId"),
                Member(StableAttributes("Url"), "Full"),
                Member(StableAttributes("Url"), "Query")))
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

        var spanStorageProjectionKeys = resolver.ValuesFromMembers(
                Member(IncubatingAttributes("GenAi"), "ProviderName"),
                Member(IncubatingAttributes("GenAi"), "RequestModel"),
                Member(IncubatingAttributes("GenAi"), "ResponseModel"),
                Member(IncubatingAttributes("GenAi"), "UsageInputTokens"),
                Member(IncubatingAttributes("GenAi"), "UsageOutputTokens"),
                Member(IncubatingAttributes("GenAi"), "RequestTemperature"),
                Member(IncubatingAttributes("GenAi"), "ResponseFinishReasons"),
                Member(IncubatingAttributes("GenAi"), "ToolName"))
            .Append("gen_ai.usage.cost")
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        var genAiProjection = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GenAiProviderName"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "ProviderName")),
            ["GenAiRequestModel"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "RequestModel")),
            ["GenAiResponseModel"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "ResponseModel")),
            ["GenAiInputTokens"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "UsageInputTokens")),
            ["GenAiOutputTokens"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "UsageOutputTokens")),
            ["GenAiTemperature"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "RequestTemperature")),
            ["GenAiStopReason"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "ResponseFinishReasons")),
            ["GenAiToolName"] = resolver.ValueFromMember(Member(IncubatingAttributes("GenAi"), "ToolName")),
            ["GenAiCostUsd"] = "gen_ai.usage.cost",
            ["ServiceName"] = resolver.ValueFromMember(Member(StableAttributes("Service"), "Name"))
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

    private Dictionary<string, string> ReadCentralPackageVersions()
    {
        var document = XDocument.Load(RootDirectory / "Directory.Packages.props");
        return document
            .Descendants("PackageVersion")
            .Select(static element => new
            {
                Id = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value
            })
            .Where(static package => package.Id is not null && package.Version is not null)
            .ToDictionary(static package => package.Id!, static package => package.Version!, StringComparer.Ordinal);
    }

    private static AttributeTypeRef StableAttributes(string domain) =>
        new(
            StablePackageId,
            $"Qyl.OpenTelemetry.SemanticConventions.Attributes.{domain}.{domain}Attributes");

    private static AttributeTypeRef IncubatingAttributes(string domain) =>
        new(
            IncubatingPackageId,
            $"Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.{domain}.{domain}Attributes");

    private static AttributeMemberRef Member(AttributeTypeRef type, string memberName) =>
        new(type, memberName);

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

internal readonly record struct AttributeTypeRef(string PackageId, string TypeName);

internal readonly record struct AttributeMemberRef(AttributeTypeRef Type, string MemberName);

internal sealed class SemConvAttributeResolver(IReadOnlyDictionary<string, string> packageVersions)
{
    private readonly Dictionary<string, Assembly> _assemblies = new(StringComparer.Ordinal);

    public string[] ValuesFromTypes(params AttributeTypeRef[] typeRefs) =>
        typeRefs
            .SelectMany(ValuesFromType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    public string[] ValuesFromMembers(params AttributeMemberRef[] memberRefs) =>
        memberRefs
            .Select(ValueFromMember)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    public string ValueFromMember(AttributeMemberRef memberRef)
    {
        var type = ResolveType(memberRef.Type);
        var field = type.GetField(memberRef.MemberName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (field is not { IsLiteral: true, IsInitOnly: false } || field.FieldType != typeof(string))
        {
            throw new InvalidOperationException(
                $"Semantic convention member '{memberRef.Type.TypeName}.{memberRef.MemberName}' is not a public string constant.");
        }

        return (string)field.GetRawConstantValue()!;
    }

    private IEnumerable<string> ValuesFromType(AttributeTypeRef typeRef) =>
        ResolveType(typeRef)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(static field => field is { IsLiteral: true, IsInitOnly: false } &&
                                   field.FieldType == typeof(string))
            .Select(static field => (string)field.GetRawConstantValue()!);

    private Type ResolveType(AttributeTypeRef typeRef)
    {
        var assembly = ResolveAssembly(typeRef.PackageId);
        return assembly.GetType(typeRef.TypeName, throwOnError: true)!;
    }

    private Assembly ResolveAssembly(string packageId)
    {
        if (_assemblies.TryGetValue(packageId, out var assembly))
            return assembly;

        if (!packageVersions.TryGetValue(packageId, out var version))
            throw new InvalidOperationException($"Central package version '{packageId}' is missing.");

        var packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            packageRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages");
        }

        var packageDirectory = Path.Combine(packageRoot, packageId.ToLowerInvariant(), version);
        var libDirectory = Path.Combine(packageDirectory, "lib");
        var assemblyPath = new[]
            {
                Path.Combine(libDirectory, "net10.0", packageId + ".dll"),
                Path.Combine(libDirectory, "netstandard2.0", packageId + ".dll")
            }
            .FirstOrDefault(File.Exists);

        if (assemblyPath is null)
        {
            throw new FileNotFoundException(
                $"Package assembly for '{packageId}' {version} was not found under '{packageDirectory}'. " +
                "Run `dotnet restore services/qyl.collector/qyl.collector.csproj --configfile nuget.config`.");
        }

        assembly = Assembly.LoadFrom(assemblyPath);
        _assemblies.Add(packageId, assembly);
        return assembly;
    }
}
