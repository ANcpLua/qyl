using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Telemetry.SemanticConventions;
using Serilog;

namespace Qyl.Build;

interface ICollectorSemanticCatalog : IHasSourcePaths
{
    const string StablePackageId = "Qyl.Telemetry.SemanticConventions";
    const string IncubatingPackageId = "Qyl.Telemetry.SemanticConventions.Incubating";

    AbsolutePath CollectorSemanticCatalogFile =>
        CollectorIngestionDirectory / "Generated" / "CollectorSemanticAttributeCatalog.g.cs";

    AbsolutePath CollectorSemanticPolicyFile =>
        RootDirectory / "eng" / "config" / "collector-semantic-policy.json";

    AbsolutePath AnalysisSemanticMetadataFile => RootDirectory / "services" / "qyl.collector" / "Analysis" / "Generated" / "semconv-metadata.json";

    Target GenerateCollectorSemanticAttributeCatalog => d => d
        .Description("Generate collector semantic attribute catalog from Qyl.Telemetry.SemanticConventions")
        .Executes(() =>
        {
            CollectorSemanticCatalogFile.Parent.CreateDirectory();
            var generated = GenerateCollectorSemanticAttributeCatalogText();
            if (!CollectorSemanticCatalogFile.FileExists() ||
                !string.Equals(File.ReadAllText(CollectorSemanticCatalogFile), generated, StringComparison.Ordinal))
            {
                File.WriteAllText(CollectorSemanticCatalogFile, generated);
            }

            File.WriteAllText(AnalysisSemanticMetadataFile, new SemConvAttributeResolver(ReadResolvedPackageAssemblies()).AnalysisMetadataJson());

            Log.Information(
                "Generated collector semantic attribute catalog: {File}",
                RootDirectory.GetRelativePathTo(CollectorSemanticCatalogFile));
        });

    Target VerifyCollectorSemanticAttributeCatalog => d => d
        .ProceedAfterFailure()
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

            var expectedMetadata = new SemConvAttributeResolver(ReadResolvedPackageAssemblies()).AnalysisMetadataJson();
            if (!AnalysisSemanticMetadataFile.FileExists() || File.ReadAllText(AnalysisSemanticMetadataFile) != expectedMetadata)
                throw new InvalidOperationException("Analysis semantic metadata is stale. Run GenerateCollectorSemanticAttributeCatalog.");

            if (string.Equals(actual, expected, StringComparison.Ordinal))
            {
                Log.Information("Collector semantic attribute catalog matches Qyl.Telemetry.SemanticConventions");
                return;
            }

            throw new InvalidOperationException(
                "Generated collector semantic attribute catalog is stale. " +
                "Run `./eng/build.sh GenerateCollectorSemanticAttributeCatalog` and commit the result.");
        });

    Target VerifyCollectorSemanticPolicyIsCatalogBacked => d => d
        .ProceedAfterFailure()
        .Unlisted()
        .Description("Verify collector runtime semantic policy uses the generated catalog")
        .Executes(() =>
        {
            var policyFile = CollectorIngestionDirectory / "AttributeKeySets.cs";
            if (!policyFile.FileExists())
                throw new FileNotFoundException("Missing collector semantic policy", policyFile.ToString());

            if (!CollectorSemanticCatalogFile.FileExists())
                throw new FileNotFoundException(
                    "Missing generated collector semantic attribute catalog. Run `./eng/build.sh GenerateCollectorSemanticAttributeCatalog`.",
                    CollectorSemanticCatalogFile.ToString());

            var policyRoot = ParseCSharp(policyFile);
            var catalogMembers = ReadCollectorSemanticCatalogMemberNames();
            var catalogReferences = CollectorSemanticCatalogMemberReferences(policyRoot);
            var invalidCatalogReferences = catalogReferences
                .Where(reference => !catalogMembers.Contains(reference.Member))
                .ToList();
            var structuralOffenders = SemanticPolicyStructuralOffenders(policyRoot, policyFile)
                .ToList();

            if (catalogReferences.Count > 0 && invalidCatalogReferences.Count is 0 && structuralOffenders.Count is 0)
            {
                Log.Information("Collector semantic policy is backed by the generated catalog");
                return;
            }

            if (catalogReferences.Count is 0)
                Log.Error("  Runtime semantic policy references no generated CollectorSemanticAttributeCatalog members in {File}",
                    RootDirectory.GetRelativePathTo(policyFile));

            foreach (var reference in invalidCatalogReferences)
                Log.Error("  Unknown generated catalog member at {File}:{Line}: {Member}",
                    RootDirectory.GetRelativePathTo(policyFile),
                    reference.Line,
                    reference.Member);

            foreach (var offender in structuralOffenders)
                Log.Error("  Runtime semantic policy handwiring at {File}:{Line}: {Kind}: {Text}",
                    offender.File,
                    offender.Line,
                    offender.Kind,
                    offender.Text);

            throw new InvalidOperationException(
                "Do not handwire OpenTelemetry semantic convention type lists in AttributeKeySets. " +
                "Generate CollectorSemanticAttributeCatalog.g.cs from Qyl.Telemetry.SemanticConventions and consume it there.");
        });

    private readonly record struct CatalogMemberReference(string Member, int Line);

    private readonly record struct SemanticPolicyOffender(string File, int Line, string Kind, string Text);

    IReadOnlySet<string> ResolveCollectorSemanticAttributeValues() =>
        new HashSet<string>(
            new SemConvAttributeResolver(ReadResolvedPackageAssemblies()).AllAttributeValues(),
            StringComparer.Ordinal);

    /// <summary>
    /// The values of one registry value class, e.g. <c>Messaging.MessagingAttributes+SystemValues</c>,
    /// read from the pinned packages rather than restated in a gate.
    /// </summary>
    IReadOnlySet<string> ResolveWellKnownValues(string typePath) =>
        new HashSet<string>(
            new SemConvAttributeResolver(ReadResolvedPackageAssemblies())
                .WellKnownValues(typePath).Values.Select(static value => value.Value),
            StringComparer.Ordinal);

    string GenerateCollectorSemanticAttributeCatalogText()
    {
        var resolver = new SemConvAttributeResolver(ReadResolvedPackageAssemblies());
        var policy = ReadCollectorSemanticPolicyConfig();
        var stableAttributeValues = resolver.AttributeValues(StablePackageId);
        var incubatingAttributeValues = resolver.AttributeValues(IncubatingPackageId);

        // Provenance for the `// incubating` markers below: an attribute is
        // Development-status iff it ships in the Incubating package and NOT in the
        // Stable package. A key that exists in Stable (e.g. after graduation) is
        // treated as Stable even if the Incubating package still re-exports it.
        var incubatingKeys = new HashSet<string>(
            incubatingAttributeValues.Except(stableAttributeValues, StringComparer.Ordinal),
            StringComparer.Ordinal);
        incubatingKeys.UnionWith(policy.DevelopmentAttributeAllowList.Span);
        incubatingKeys.UnionWith(policy.DevelopmentAttributeAllowList.Log);
        incubatingKeys.UnionWith(policy.DevelopmentAttributeAllowList.Metric);
        incubatingKeys.UnionWith(policy.DevelopmentAttributeAllowList.Resource);

        var spanAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, policy.SpanAttributeAllowList.StablePrefixes, "spanAttributeAllowList.stablePrefixes"),
            ValuesWithPrefixes(incubatingAttributeValues, policy.SpanAttributeAllowList.IncubatingPrefixes, "spanAttributeAllowList.incubatingPrefixes"),
            policy.DevelopmentAttributeAllowList.Span);

        var logAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, policy.LogAttributeAllowList.StablePrefixes, "logAttributeAllowList.stablePrefixes"),
            ValuesWithPrefixes(incubatingAttributeValues, policy.LogAttributeAllowList.IncubatingPrefixes, "logAttributeAllowList.incubatingPrefixes"),
            policy.DevelopmentAttributeAllowList.Log);

        var metricAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, policy.MetricAttributeAllowList.StablePrefixes, "metricAttributeAllowList.stablePrefixes"),
            ValuesWithPrefixes(incubatingAttributeValues, policy.MetricAttributeAllowList.IncubatingPrefixes, "metricAttributeAllowList.incubatingPrefixes"),
            policy.DevelopmentAttributeAllowList.Metric);

        var resourceAttributeAllowList = NormalizedValues(
            ValuesWithPrefixes(stableAttributeValues, policy.ResourceAttributeAllowList.StablePrefixes, "resourceAttributeAllowList.stablePrefixes"),
            ValuesWithPrefixes(incubatingAttributeValues, policy.ResourceAttributeAllowList.IncubatingPrefixes, "resourceAttributeAllowList.incubatingPrefixes"),
            policy.DevelopmentAttributeAllowList.Resource);

        var sessionCorrelation = resolver.RequiredAttributeValues(policy.SessionCorrelation);

        // The qyl-owned resource keys are held to the same rule as every other list here: a key the
        // collector persists must exist in the pinned registry packages, or the vocabulary and the
        // storage policy have drifted apart. Without this, a hand-added key in the policy JSON would
        // reach storage with nothing upstream defining it.
        var qylResourceAttributeAllowList = resolver
            .RequiredAttributeValues(policy.QylResourceAttributeAllowList)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        // The project-id keys decide which project a row is filed under, so a key that has drifted out of the
        // registry does not fail loudly, it misfiles data. Order is the policy's, because the lookup is
        // first-match; only membership is validated here.
        var projectIdResourceKeys = resolver.RequiredAttributeValues(policy.ProjectIdResourceKeys);

        // The deny list is the registry's: RegistryPolicy is generated from the qyl.persisted and
        // qyl.token_exempt annotations and the denied-token list, and the typed views and QYL0113 read the
        // same table. A key this file names for persistence that the registry does not persist would be
        // captured by the catalog and dropped by the denial, so it fails the generation instead.
        var deniedByRegistry = PersistedPolicyKeys(policy)
            .Where(static entry => !RegistryPolicy.IsPersisted(entry.Key))
            .Select(static entry => $"{entry.Key} ({entry.Section})")
            .ToArray();
        if (deniedByRegistry.Length is not 0)
        {
            throw new InvalidOperationException(
                "eng/config/collector-semantic-policy.json names keys for persistence that the registry does not persist " +
                $"(Qyl.Telemetry.SemanticConventions.RegistryPolicy): {string.Join(", ", deniedByRegistry)}.");
        }

        var appAttributePrefix = resolver.RequiredAttributeValue(policy.AppAttributes.TemplateKey) + ".";

        var spanHotAttributeKeys = resolver.RequiredAttributeValues(policy.SpanHotAttributeKeys)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        var projectionConstants = policy.ProjectionConstants.ToDictionary(
            static item => item.Key,
            item => (string?)resolver.RequiredAttributeValue(item.Value),
            StringComparer.Ordinal);
        projectionConstants.Add("SchemaUrlCurrent", resolver.SchemaUrlCurrent());

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine($"// Generated by eng/build GenerateCollectorSemanticAttributeCatalog from the {StablePackageId} and {IncubatingPackageId} package references (the deny list is {StablePackageId}'s RegistryPolicy) and eng/config/collector-semantic-policy.json.");
        builder.AppendLine("//");
        builder.AppendLine($"// Entries marked `// incubating` come from the {IncubatingPackageId}");
        builder.AppendLine("// package (Development-status attributes) and are subject to breaking changes — see");
        builder.AppendLine("// https://opentelemetry.io/docs/specs/otel/versioning-and-stability/#development.");
        builder.AppendLine($"// Unmarked entries come from the {StablePackageId} package or are qyl-owned keys.");
        builder.AppendLine("// Consuming code SHOULD treat incubating keys as unstable; the build fails loudly (see");
        builder.AppendLine("// VerifyCollectorSemanticAttributeCatalog) if any referenced key is renamed or removed upstream.");
        builder.AppendLine("// </auto-generated>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.Collections.Frozen;");
        builder.AppendLine();
        builder.AppendLine("namespace Qyl.Collector.Ingestion;");
        builder.AppendLine();
        builder.AppendLine("internal static class CollectorSemanticAttributeCatalog");
        builder.AppendLine("{");
        builder.AppendLine("    internal const string BaggagePrefix = \"baggage.\";");
        builder.AppendLine();

        WriteFrozenSet(builder, "SessionCorrelation", sessionCorrelation, "StringComparer.Ordinal", incubatingKeys);
        WriteFrozenSet(builder, "ProjectIdResourceKeys", projectIdResourceKeys, "StringComparer.Ordinal", incubatingKeys);

        // Precedence arrays preserve the order declared in collector-semantic-policy.json. The
        // FrozenSets above answer membership only; first-match lookups (session id, project-id
        // partition key) must read these, because FrozenSet enumeration order is unspecified.
        WriteOrderedStringArray(builder, "SessionCorrelationPrecedence", sessionCorrelation, incubatingKeys);
        WriteOrderedStringArray(builder, "ProjectIdResourceKeyPrecedence", projectIdResourceKeys, incubatingKeys);
        WriteFrozenSet(builder, "QylResourceAttributeAllowList", qylResourceAttributeAllowList, "StringComparer.Ordinal", incubatingKeys);
        WriteFrozenSet(builder, "SpanAttributeAllowList", spanAttributeAllowList, "StringComparer.Ordinal", incubatingKeys);
        WriteFrozenSet(builder, "LogAttributeAllowList", logAttributeAllowList, "StringComparer.Ordinal", incubatingKeys);
        WriteFrozenSet(builder, "MetricAttributeAllowList", metricAttributeAllowList, "StringComparer.Ordinal", incubatingKeys);
        WriteFrozenSet(builder, "ResourceAttributeAllowList", resourceAttributeAllowList, "StringComparer.Ordinal", incubatingKeys);
        // RegistryPolicy, verbatim. A denied key compares case-insensitively here, stricter than the registry's
        // ordinal comparison: a differently cased spelling of a denied key stays denied. The exempt and vendor keys
        // compare ordinally, as in the registry: they waive the token rule, so ignoring case would waive it for
        // spellings the registry never named.
        WriteFrozenSet(builder, "NotPersistedKeys", RegistryPolicy.NotPersistedKeys, "StringComparer.OrdinalIgnoreCase", incubatingKeys);
        WriteStringArray(builder, "NotPersistedPrefixes", RegistryPolicy.NotPersistedPrefixes);
        WriteStringArray(builder, "DeniedKeyTokens", RegistryPolicy.DeniedKeyTokens);
        WriteFrozenSet(builder, "TokenExemptKeys", RegistryPolicy.TokenExemptKeys, "StringComparer.Ordinal", incubatingKeys);
        WriteFrozenSet(builder, "VendorKeys", RegistryPolicy.VendorKeys, "StringComparer.Ordinal", incubatingKeys);
        WriteFrozenSet(builder, "SpanHotAttributeKeys", spanHotAttributeKeys, "StringComparer.Ordinal", incubatingKeys);

        // qyl.app.<key>, written by SetAppTag: captured by prefix within the bounds the registry note states.
        builder.Append("    internal const string AppAttributePrefix = ").Append(StringLiteral(appAttributePrefix)).Append(';');
        if (incubatingKeys.Contains(policy.AppAttributes.TemplateKey))
            builder.Append(" // incubating");
        builder.AppendLine();
        builder.Append("    internal const int AppAttributeMaxKeys = ").Append(policy.AppAttributes.MaxKeysPerSpan.ToString(System.Globalization.CultureInfo.InvariantCulture)).AppendLine(";");
        builder.Append("    internal const int AppAttributeMaxValueLength = ").Append(policy.AppAttributes.MaxValueLength.ToString(System.Globalization.CultureInfo.InvariantCulture)).AppendLine(";");
        builder.AppendLine();

        foreach (var projection in projectionConstants.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            if (projection.Value is null)
            {
                builder.Append("    internal static readonly string? ")
                    .Append(projection.Key)
                    .AppendLine(" = null;");
                continue;
            }

            builder.Append("    internal const string ")
                .Append(projection.Key)
                .Append(" = ")
                .Append(StringLiteral(projection.Value))
                .Append(';');

            if (incubatingKeys.Contains(projection.Value))
                builder.Append(" // incubating");

            builder.AppendLine();
        }

        foreach (var valueClass in policy.WellKnownValueClasses.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            var (values, incubating) = resolver.WellKnownValues(valueClass.Value);
            builder.AppendLine();
            builder.Append("    internal static class ").AppendLine(valueClass.Key);
            builder.AppendLine("    {");
            foreach (var (fieldName, value) in values)
            {
                builder.Append("        internal const string ")
                    .Append(fieldName)
                    .Append(" = ")
                    .Append(StringLiteral(value))
                    .Append(';');

                if (incubating)
                    builder.Append(" // incubating");

                builder.AppendLine();
            }

            builder.AppendLine("    }");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private IReadOnlyDictionary<string, string> ReadResolvedPackageAssemblies() =>
        ProjectAssetsPackageResolver.ResolvePackageAssemblies(
            LocateBuildProjectAssetsFile(),
            StablePackageId,
            IncubatingPackageId);

    private CollectorSemanticPolicyConfig ReadCollectorSemanticPolicyConfig()
    {
        if (!CollectorSemanticPolicyFile.FileExists())
        {
            throw new FileNotFoundException(
                "Missing collector semantic policy config.",
                CollectorSemanticPolicyFile.ToString());
        }

        var config = JsonSerializer.Deserialize(
            File.ReadAllText(CollectorSemanticPolicyFile),
            CollectorSemanticPolicyJsonContext.Default.CollectorSemanticPolicyConfig);

        if (config is null)
            throw new InvalidOperationException($"Collector semantic policy config '{CollectorSemanticPolicyFile}' is empty.");

        config.Validate(RootDirectory.GetRelativePathTo(CollectorSemanticPolicyFile).ToString());
        return config;
    }

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

    /// <summary>
    /// The keys this file names for the collector to persist as attributes. Not among them: the session
    /// correlation keys, which ingest lifts into a column, and the projection constants, which name what a reader reads.
    /// </summary>
    private static IEnumerable<(string Key, string Section)> PersistedPolicyKeys(CollectorSemanticPolicyConfig policy) =>
        policy.DevelopmentAttributeAllowList.Span.Select(static key => (key, "developmentAttributeAllowList.span"))
            .Concat(policy.DevelopmentAttributeAllowList.Log.Select(static key => (key, "developmentAttributeAllowList.log")))
            .Concat(policy.DevelopmentAttributeAllowList.Metric.Select(static key => (key, "developmentAttributeAllowList.metric")))
            .Concat(policy.DevelopmentAttributeAllowList.Resource.Select(static key => (key, "developmentAttributeAllowList.resource")))
            .Concat(policy.QylResourceAttributeAllowList.Select(static key => (key, "qylResourceAttributeAllowList")))
            .Concat(policy.ProjectIdResourceKeys.Select(static key => (key, "projectIdResourceKeys")))
            .Concat(policy.SpanHotAttributeKeys.Select(static key => (key, "spanHotAttributeKeys")));

    private static string[] ValuesWithPrefixes(
        IEnumerable<string> values,
        IReadOnlyCollection<string> prefixes,
        string section)
    {
        if (prefixes.Count is 0)
            throw new InvalidOperationException($"Collector semantic policy section '{section}' must not be empty.");

        var source = values.ToArray();
        var resolved = new List<string>();

        foreach (var prefix in prefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new InvalidOperationException($"Collector semantic policy section '{section}' contains an empty prefix.");

            var matches = source
                .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();

            if (matches.Length is 0)
            {
                throw new InvalidOperationException(
                    $"Collector semantic policy section '{section}' prefix '{prefix}' matched no attributes in the configured semantic convention packages.");
            }

            resolved.AddRange(matches);
        }

        return resolved.ToArray();
    }

    private static string[] NormalizedValues(params IEnumerable<string>[] values) =>
        values
            .SelectMany(static value => value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private IReadOnlySet<string> ReadCollectorSemanticCatalogMemberNames()
    {
        var root = ParseCSharp(CollectorSemanticCatalogFile);
        var catalogClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .SingleOrDefault(static declaration => declaration.Identifier.ValueText is "CollectorSemanticAttributeCatalog");

        if (catalogClass is null)
        {
            throw new InvalidOperationException(
                "Generated collector semantic attribute catalog does not declare CollectorSemanticAttributeCatalog.");
        }

        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in catalogClass.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in field.Declaration.Variables)
                members.Add(variable.Identifier.ValueText);
        }

        foreach (var property in catalogClass.Members.OfType<PropertyDeclarationSyntax>())
            members.Add(property.Identifier.ValueText);

        return members;
    }

    private static IReadOnlyList<CatalogMemberReference> CollectorSemanticCatalogMemberReferences(CompilationUnitSyntax root) =>
        root.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(static memberAccess => memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "CollectorSemanticAttributeCatalog" })
            .Select(static memberAccess => new CatalogMemberReference(
                memberAccess.Name.Identifier.ValueText,
                NodeLine(memberAccess)))
            .Distinct()
            .OrderBy(static reference => reference.Member, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Line)
            .ToArray();

    private IEnumerable<SemanticPolicyOffender> SemanticPolicyStructuralOffenders(
        CompilationUnitSyntax root,
        AbsolutePath policyFile)
    {
        var relativePath = RootDirectory.GetRelativePathTo(policyFile).ToString();

        foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (IsForbiddenSemanticConventionReference(usingDirective.Name?.ToString()))
                yield return CreateOffender(usingDirective, "Direct semantic convention import");
        }

        foreach (var name in root.DescendantNodes().OfType<NameSyntax>())
        {
            var text = name.ToString();
            if (IsForbiddenSemanticConventionReference(text))
                yield return CreateOffender(name, "Direct semantic convention reference");

            if (IsReflectionReference(text))
                yield return CreateOffender(name, "Reflection-based semantic policy");
        }

        foreach (var typeOf in root.DescendantNodes().OfType<TypeOfExpressionSyntax>())
            yield return CreateOffender(typeOf, "Type-based semantic policy");

        foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                literal.Token.ValueText is { Length: > 0 } value &&
                LooksLikeAttributePolicyLiteral(value))
            {
                yield return CreateOffender(literal, "Raw attribute policy literal");
            }
        }

        SemanticPolicyOffender CreateOffender(SyntaxNode node, string kind) =>
            new(relativePath, NodeLine(node), kind, NodePreview(node));
    }

    private static CompilationUnitSyntax ParseCSharp(AbsolutePath file)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file.ToString());
        return tree.GetCompilationUnitRoot();
    }

    /// <summary>The one semantic-convention namespace the runtime policy may name.</summary>
    private const string GeneratedMappingNamespace = "Qyl.Telemetry.SemanticConventions.Incubating.Mapping";

    /// <summary>
    /// The rule is "no handwired semantic-convention lists in the policy", not "no package
    /// reference": <c>Incubating.Mapping.AttributeMapping</c> is generated from the same registry
    /// as this catalog, so consuming its vendor and rename tables is the behaviour this gate
    /// exists to require. Everything else, the <c>Attributes</c> namespaces above all, stays out.
    /// A qualified name contributes its own prefixes as nodes, so a prefix of the allowed
    /// namespace is allowed too; the full name of anything else is still reported.
    /// </summary>
    private static bool IsForbiddenSemanticConventionReference(string? text)
    {
        if (text is null || !text.Contains("Qyl.Telemetry.SemanticConventions", StringComparison.Ordinal))
            return false;

        var normalized = text.StartsWith("global::", StringComparison.Ordinal)
            ? text["global::".Length..]
            : text;

        return !normalized.StartsWith(GeneratedMappingNamespace, StringComparison.Ordinal)
               && !GeneratedMappingNamespace.StartsWith(normalized, StringComparison.Ordinal);
    }

    private static bool IsReflectionReference(string text)
    {
        var simpleName = text.Split('.').Last();
        return simpleName is
            "Assembly" or
            "BindingFlags" or
            "DynamicallyAccessedMembersAttribute" or
            "GetField" or
            "GetFields" or
            "GetRawConstantValue" or
            "GetType" or
            "GetTypes";
    }

    private static bool LooksLikeAttributePolicyLiteral(string value) =>
        value.Contains('.', StringComparison.Ordinal) ||
        value.StartsWith("qyl_", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("gen_ai", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("otel_", StringComparison.OrdinalIgnoreCase);

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

    private static void WriteFrozenSet(
        StringBuilder builder,
        string name,
        IEnumerable<string> values,
        string comparer,
        IReadOnlySet<string> incubating)
    {
        builder.Append("    internal static readonly FrozenSet<string> ")
            .Append(name)
            .AppendLine(" = FrozenSet.Create(");
        builder.Append("        ").Append(comparer).AppendLine(",");

        var ordered = values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            builder.Append("        ").Append(StringLiteral(ordered[index]));
            if (index < ordered.Length - 1)
                builder.Append(',');
            if (incubating.Contains(ordered[index]))
                builder.Append(" // incubating");
            builder.AppendLine();
        }

        builder.AppendLine("    );");
        builder.AppendLine();
    }

    private static void WriteOrderedStringArray(
        StringBuilder builder,
        string name,
        IEnumerable<string> values,
        IReadOnlySet<string> incubating)
    {
        builder.Append("    internal static readonly string[] ").Append(name).AppendLine(" =");
        builder.AppendLine("    [");
        foreach (var value in values.Distinct(StringComparer.Ordinal))
        {
            builder.Append("        ").Append(StringLiteral(value)).Append(',');
            if (incubating.Contains(value))
                builder.Append(" // incubating");
            builder.AppendLine();
        }

        builder.AppendLine("    ];");
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

internal sealed class CollectorSemanticPolicyConfig
{
    public CollectorSemanticPrefixPolicy SpanAttributeAllowList { get; init; } = new();
    public CollectorSemanticPrefixPolicy LogAttributeAllowList { get; init; } = new();
    public CollectorSemanticPrefixPolicy MetricAttributeAllowList { get; init; } = new();
    public CollectorSemanticPrefixPolicy ResourceAttributeAllowList { get; init; } = new();
    public CollectorDevelopmentAttributePolicy DevelopmentAttributeAllowList { get; init; } = new();
    public string[] QylResourceAttributeAllowList { get; init; } = [];
    public string[] ProjectIdResourceKeys { get; init; } = [];
    public string[] SessionCorrelation { get; init; } = [];
    public CollectorAppAttributePolicy AppAttributes { get; init; } = new();
    public string[] SpanHotAttributeKeys { get; init; } = [];
    public Dictionary<string, string> ProjectionConstants { get; init; } = [];
    public Dictionary<string, string> WellKnownValueClasses { get; init; } = [];

    public void Validate(string relativePath)
    {
        SpanAttributeAllowList.Validate(relativePath, "spanAttributeAllowList");
        LogAttributeAllowList.Validate(relativePath, "logAttributeAllowList");
        MetricAttributeAllowList.Validate(relativePath, "metricAttributeAllowList");
        ResourceAttributeAllowList.Validate(relativePath, "resourceAttributeAllowList");
        RequireNonEmpty(DevelopmentAttributeAllowList.Span, relativePath, "developmentAttributeAllowList.span");
        RequireNonEmpty(DevelopmentAttributeAllowList.Log, relativePath, "developmentAttributeAllowList.log");
        RequireNonEmpty(DevelopmentAttributeAllowList.Metric, relativePath, "developmentAttributeAllowList.metric");

        RequireNonEmpty(QylResourceAttributeAllowList, relativePath, "qylResourceAttributeAllowList");
        RequireNonEmpty(ProjectIdResourceKeys, relativePath, "projectIdResourceKeys");
        RequireNonEmpty(SessionCorrelation, relativePath, "sessionCorrelation");
        AppAttributes.Validate(relativePath);
        RequireNonEmpty(SpanHotAttributeKeys, relativePath, "spanHotAttributeKeys");

        if (ProjectionConstants.Count is 0)
            throw new InvalidOperationException($"{relativePath}: projectionConstants must not be empty.");

        RequireNoEmptyValues(ProjectionConstants.Keys, relativePath, "projectionConstants keys");
        RequireNoEmptyValues(ProjectionConstants.Values, relativePath, "projectionConstants values");

        RequireNoEmptyValues(WellKnownValueClasses.Keys, relativePath, "wellKnownValueClasses keys");
        RequireNoEmptyValues(WellKnownValueClasses.Values, relativePath, "wellKnownValueClasses values");
    }

    private static void RequireNonEmpty(IReadOnlyCollection<string> values, string relativePath, string section)
    {
        if (values.Count is 0)
            throw new InvalidOperationException($"{relativePath}: {section} must not be empty.");

        RequireNoEmptyValues(values, relativePath, section);
        RequireNoDuplicates(values, relativePath, section);
    }

    public static void ValidatePrefixSet(IReadOnlyCollection<string> values, string relativePath, string section) =>
        RequireNonEmpty(values, relativePath, section);

    private static void RequireNoEmptyValues(IEnumerable<string> values, string relativePath, string section)
    {
        if (values.Any(static value => string.IsNullOrWhiteSpace(value)))
            throw new InvalidOperationException($"{relativePath}: {section} contains an empty value.");
    }

    private static void RequireNoDuplicates(IEnumerable<string> values, string relativePath, string section)
    {
        var duplicates = values
            .GroupBy(static value => value, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length is not 0)
            throw new InvalidOperationException($"{relativePath}: {section} contains duplicate values: {string.Join(", ", duplicates)}.");
    }
}

internal sealed class CollectorSemanticPrefixPolicy
{
    public string[] StablePrefixes { get; init; } = [];
    public string[] IncubatingPrefixes { get; init; } = [];

    public void Validate(string relativePath, string section)
    {
        CollectorSemanticPolicyConfig.ValidatePrefixSet(StablePrefixes, relativePath, $"{section}.stablePrefixes");
        CollectorSemanticPolicyConfig.ValidatePrefixSet(IncubatingPrefixes, relativePath, $"{section}.incubatingPrefixes");
    }
}

internal sealed class CollectorAppAttributePolicy
{
    public string TemplateKey { get; init; } = "";
    public int MaxKeysPerSpan { get; init; }
    public int MaxValueLength { get; init; }

    public void Validate(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(TemplateKey))
            throw new InvalidOperationException($"{relativePath}: appAttributes.templateKey must not be empty.");
        if (MaxKeysPerSpan < 1 || MaxValueLength < 1)
            throw new InvalidOperationException($"{relativePath}: appAttributes.maxKeysPerSpan and maxValueLength must be positive.");
    }
}

internal sealed class CollectorDevelopmentAttributePolicy
{
    public string[] Span { get; init; } = [];
    public string[] Log { get; init; } = [];
    public string[] Metric { get; init; } = [];
    public string[] Resource { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(CollectorSemanticPolicyConfig))]
internal sealed partial class CollectorSemanticPolicyJsonContext : JsonSerializerContext
{
}

internal sealed partial class SemConvAttributeResolver(IReadOnlyDictionary<string, string> packageAssemblyPaths)
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

    public (IReadOnlyList<(string FieldName, string Value)> Values, bool Incubating) WellKnownValues(string typePath)
    {
        foreach (var (packageId, namespaceRoot) in (ReadOnlySpan<(string, string)>)
                 [
                     (ICollectorSemanticCatalog.StablePackageId,
                         "Qyl.Telemetry.SemanticConventions.Attributes."),
                     (ICollectorSemanticCatalog.IncubatingPackageId,
                         "Qyl.Telemetry.SemanticConventions.Incubating.Attributes.")
                 ])
        {
            if (ResolveAssembly(packageId).GetType(namespaceRoot + typePath, throwOnError: false) is not { } type)
                continue;

            var values = type
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static field => field is { IsLiteral: true, IsInitOnly: false } &&
                                       field.FieldType == typeof(string))
                .Select(static field => (field.Name, (string)field.GetRawConstantValue()!))
                .OrderBy(static pair => pair.Item1, StringComparer.Ordinal)
                .ToArray();

            if (values.Length is 0)
            {
                throw new InvalidOperationException(
                    $"Semantic convention value class '{typePath}' declares no public string constants.");
            }

            return (values, packageId == ICollectorSemanticCatalog.IncubatingPackageId);
        }

        throw new InvalidOperationException(
            $"Semantic convention value class '{typePath}' is not present in the configured package references.");
    }

    public string AnalysisMetadataJson()
    {
        var attributes = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        var requirements = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var package in new[] { ICollectorSemanticCatalog.IncubatingPackageId, ICollectorSemanticCatalog.StablePackageId })
        {
            var assembly = ResolveAssembly(package);
            var xml = XDocument.Load(Path.ChangeExtension(packageAssemblyPaths[package], ".xml"));
            var docs = xml.Descendants("member").ToDictionary(e => (string)e.Attribute("name")!, e => WhitespaceRun().Replace(e.Element("summary")?.Value ?? "", " ").Trim(), StringComparer.Ordinal);
            foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (type.FullName?.Contains(".Attributes.", StringComparison.Ordinal) == true && type.Name.EndsWith("Attributes", StringComparison.Ordinal) && field.IsLiteral && field.FieldType == typeof(string))
                {
                    var key = (string)field.GetRawConstantValue()!;
                    attributes[key] = new JsonObject
                    {
                        ["brief"] = docs.GetValueOrDefault("F:" + type.FullName + "." + field.Name, ""),
                        ["stability"] = package == ICollectorSemanticCatalog.StablePackageId ? "stable" : "development",
                        ["requirement_level"] = "recommended"
                    };
                }
                if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition().Name != "SpanDefinition`1") continue;
                var definition = field.GetValue(null)!;
                var id = (string)field.FieldType.GetProperty("Id")!.GetValue(definition)!;
                var kind = (string)field.FieldType.GetProperty("SpanKind")!.GetValue(definition)!;
                var refs = (IEnumerable)field.FieldType.GetProperty("Attributes")!.GetValue(definition)!;
                var required = new List<string>();
                foreach (var attribute in refs)
                    if (attribute.GetType().GetProperty("RequirementLevel")!.GetValue(attribute)!.ToString() == "Required")
                        required.Add((string)attribute.GetType().GetProperty("Name")!.GetValue(attribute)!);
                requirements[id] = new JsonObject { ["type"] = id, ["kind"] = kind,
                    ["required"] = new JsonArray(required.Order(StringComparer.Ordinal).Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()) };
            }
        }
        return new JsonObject
        {
            ["schema_url"] = SchemaUrlCurrent(),
            ["attributes"] = new JsonObject(attributes.Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value))),
            ["span_requirements"] = new JsonArray(requirements.Values.Cast<JsonNode?>().ToArray())
        }.ToJsonString() + "\n";
    }

    public string SchemaUrlCurrent()
    {
        var assembly = ResolveAssembly(ICollectorSemanticCatalog.StablePackageId);
        var type = assembly.GetType("Qyl.Telemetry.SemanticConventions.SchemaUrl", throwOnError: true)!;
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
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
