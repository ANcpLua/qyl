using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// G10(a) for <c>Qyl.Cli</c>: every type the CLI serializes across its HTTP/SSE boundary is a
/// <c>Qyl.Api.Contracts.*</c> type. The CLI is a client of the collector API and owns none of it,
/// so a locally-declared record reaching a wire is the shadow-contract failure loop 2 deletes.
///
/// The rule is reachability-based, and that is the whole point: it asks what the serializer
/// generator is actually told to emit — every <c>[JsonSerializable]</c> registration on any type
/// declaration, including partial context parts that omit the base list — not what types exist in
/// the assembly. Internal domain records (<c>QylResourceState</c>, <c>QylLogLine</c>) are
/// legitimate and must never be flagged: they sit behind <c>QylRunnerContractMapper</c>, which
/// converts them to contract types before anything is written, so they are unreachable from a
/// serialization root. A context marked <c>LocalJsonStateContext</c> is an at-rest implementation
/// detail and cannot be passed to <c>JsonContent.Create</c> or <c>ReadFromJsonAsync</c>.
///
/// Registered types must be *provably* contract-owned, through a <c>using</c> alias (top-level or
/// namespace-scoped) or a fully-qualified name. A bare identifier the verifier cannot resolve is
/// reported rather than assumed innocent, only known collection wrappers are unwrapped to their
/// element (any other generic is itself the wire shape and is judged whole), and finding nothing
/// at all is a failure: a gate whose scope moved out from under it must say so, not report success
/// over an empty set. <c>System.Text.Json.JsonElement</c> is the single serialization intrinsic:
/// generated contracts use <c>object?</c> for an explicitly open JSON value, and source-generated
/// serialization needs metadata for the runtime carrier without making it a CLI-owned wire model.
/// </summary>
interface ICliContractLoop : IHazSourcePaths
{
    private const string ContractNamespacePrefix = "Qyl.Api.Contracts.";
    private const string OpenJsonValueCarrier = "System.Text.Json.JsonElement";

    Target VerifyCliMcpToolSchemasAreGenerated => d => d
        .Unlisted()
        .Description("Verify Qyl.Cli MCP tools publish generated input and output schemas")
        .Executes(() =>
        {
            var repoRoot = NukeBuild.RootDirectory;
            var cliDirectory = repoRoot / "packages" / "Qyl.Cli";
            var roots = cliDirectory.GlobFiles("**/*.cs")
                .Where(static file => !file.ToString().Contains("/obj/", StringComparison.Ordinal)
                                      && !file.ToString().Contains("/bin/", StringComparison.Ordinal)
                                      && !file.Name.EndsWith(".g.cs", StringComparison.Ordinal)
                                      && !file.Name.EndsWith(".Designer.cs", StringComparison.Ordinal))
                .OrderBy(static file => file.ToString(), StringComparer.Ordinal)
                .Select(file => (
                    File: file,
                    Root: CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file.ToString())
                        .GetCompilationUnitRoot()))
                .ToList();

            var generatedSchemaFields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (_, root) in roots)
            {
                foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    if (variable.Initializer?.Value is not InvocationExpressionSyntax
                        {
                            Expression: IdentifierNameSyntax { Identifier.ValueText: "ParseSchema" },
                            ArgumentList.Arguments.Count: 1,
                        } parse ||
                        parse.ArgumentList.Arguments[0].Expression is not MemberAccessExpressionSyntax artifact ||
                        !artifact.Expression.ToString().EndsWith("ToolSchemas", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    generatedSchemaFields[variable.Identifier.ValueText] = artifact.Name.Identifier.ValueText;
                }
            }

            var offenders = new List<string>();
            var usedArtifacts = new HashSet<string>(StringComparer.Ordinal);
            var schemaPropertyCount = 0;
            foreach (var (file, root) in roots)
            {
                var relative = repoRoot.GetRelativePathTo(file).ToString().Replace('\\', '/');
                foreach (var literal in root.DescendantNodes().OfType<LiteralExpressionSyntax>())
                {
                    var text = literal.Token.ValueText;
                    if (!text.Contains("\"inputSchema\"", StringComparison.Ordinal) &&
                        !text.Contains("\"outputSchema\"", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var line = literal.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    offenders.Add(
                        $"{relative}:{line}: raw JSON declares an inputSchema/outputSchema; " +
                        "write a generated ToolSchemas artifact instead");
                }

                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax propertyWrite ||
                        propertyWrite.Name.Identifier.ValueText is not "WritePropertyName" ||
                        invocation.ArgumentList.Arguments.Count is not 1 ||
                        invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax literal ||
                        literal.Token.ValueText is not ("inputSchema" or "outputSchema"))
                    {
                        continue;
                    }

                    schemaPropertyCount++;
                    var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    if (invocation.Parent is not ExpressionStatementSyntax statement ||
                        statement.Parent is not BlockSyntax block)
                    {
                        offenders.Add($"{relative}:{line}: {literal.Token.ValueText} is not a standalone generated-schema write");
                        continue;
                    }

                    var statementIndex = block.Statements.IndexOf(statement);
                    if (statementIndex < 0 || statementIndex + 1 >= block.Statements.Count ||
                        block.Statements[statementIndex + 1] is not ExpressionStatementSyntax
                        {
                            Expression: InvocationExpressionSyntax
                            {
                                Expression: MemberAccessExpressionSyntax writeTo,
                                ArgumentList.Arguments.Count: 1,
                            } generatedWrite,
                        } ||
                        writeTo.Name.Identifier.ValueText is not "WriteTo" ||
                        writeTo.Expression is not IdentifierNameSyntax schemaField ||
                        generatedWrite.ArgumentList.Arguments[0].Expression.ToString() != propertyWrite.Expression.ToString() ||
                        !generatedSchemaFields.TryGetValue(schemaField.Identifier.ValueText, out var artifactName))
                    {
                        offenders.Add(
                            $"{relative}:{line}: {literal.Token.ValueText} must be written directly from a " +
                            "JsonElement parsed from Qyl.Api.Contracts.Mcp.ToolSchemas");
                        continue;
                    }

                    usedArtifacts.Add(artifactName);
                }
            }

            string[] requiredArtifacts =
            [
                "GetActiveWorkflowRunInput",
                "GetActiveWorkflowRunOutput",
                "RecordDiagnosticSnapshotInput",
                "RecordDiagnosticSnapshotOutput",
            ];
            foreach (var artifact in requiredArtifacts.Where(artifact => !usedArtifacts.Contains(artifact)))
                offenders.Add($"generated ToolSchemas.{artifact} is not published by the Qyl.Cli MCP bridge");

            if (schemaPropertyCount is 0)
                offenders.Add("no MCP inputSchema/outputSchema properties were found under packages/Qyl.Cli");

            if (offenders.Count > 0)
            {
                throw new InvalidOperationException(
                    "Qyl.Cli publishes a hand-maintained or missing MCP tool schema:" + Environment.NewLine +
                    string.Join(Environment.NewLine, offenders) + Environment.NewLine +
                    "Define named tool input/output models in qyl-api-schema, regenerate Qyl.Api.Contracts, " +
                    "and write the matching Qyl.Api.Contracts.Mcp.ToolSchemas artifact verbatim.");
            }

            Log.Information(
                "Qyl.Cli publishes {SchemaProperties} MCP input/output schemas from {Artifacts} generated artifacts",
                schemaPropertyCount,
                usedArtifacts.Count);
        });

    /// <summary>
    /// Generic wrappers that serialize their single element rather than themselves. Anything not
    /// listed — an envelope, a pair, a dictionary — is judged whole so a CLI-owned generic around
    /// a contract payload cannot pass on the payload's innocence.
    /// </summary>
    private static readonly string[] s_collectionWrappers =
    [
        "List", "IList", "IReadOnlyList", "IEnumerable", "ICollection", "IReadOnlyCollection",
        "ImmutableArray", "ImmutableList", "IImmutableList", "HashSet", "ISet", "IReadOnlySet",
    ];

    Target VerifyCliSerializesContractsOnly => d => d
        .Unlisted()
        .Description("Verify Qyl.Cli JSON serializer contexts register only Qyl.Api.Contracts types")
        .Executes(() =>
        {
            var repoRoot = NukeBuild.RootDirectory;
            var cliDirectory = repoRoot / "packages" / "Qyl.Cli";
            var sources = cliDirectory.GlobFiles("**/*.cs")
                .Where(static file => !file.ToString().Contains("/obj/", StringComparison.Ordinal)
                                      && !file.ToString().Contains("/bin/", StringComparison.Ordinal)
                                      && !file.Name.EndsWith(".g.cs", StringComparison.Ordinal)
                                      && !file.Name.EndsWith(".Designer.cs", StringComparison.Ordinal))
                .OrderBy(static file => file.ToString(), StringComparer.Ordinal)
                .ToList();

            var roots = sources
                .Select(file => (
                    File: file,
                    Root: CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file.ToString())
                        .GetCompilationUnitRoot()))
                .ToList();

            // `global using X = ...;` applies to every file in the project, so the aliases are
            // collected across the whole CLI before any one file is resolved.
            var globalAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (_, root) in roots)
            {
                foreach (var alias in AliasDirectives(root).Where(static a => a.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)))
                    globalAliases[alias.Alias!.Name.Identifier.ValueText] = alias.Name!.ToString();
            }

            // C# lets a partial part of a JsonSerializerContext omit the base list, so context
            // identity is collected across all files before any registration is judged.
            var contextNames = roots
                .SelectMany(static entry => entry.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                .Where(static type => type.BaseList?.Types.Any(static baseType =>
                    baseType.Type.ToString().EndsWith("JsonSerializerContext", StringComparison.Ordinal)) == true)
                .Select(static type => type.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);
            var localContextNames = roots
                .SelectMany(static entry => entry.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                .Where(type => contextNames.Contains(type.Identifier.ValueText))
                .Where(static type => type.AttributeLists
                    .SelectMany(static list => list.Attributes)
                    .Any(static attribute => attribute.Name.ToString()
                        .EndsWith("LocalJsonStateContext", StringComparison.Ordinal)))
                .Select(static type => type.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            var offenders = new List<string>();
            var registeredCount = 0;
            var intrinsicRegisteredCount = 0;
            var localRegisteredCount = 0;

            foreach (var (file, root) in roots)
            {
                var relative = repoRoot.GetRelativePathTo(file).ToString().Replace('\\', '/');
                Dictionary<string, string>? aliases = null;

                // Every [JsonSerializable] on any type declaration is a registration the source
                // generator will honor — base-list-bearing part or not.
                foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var isLocalStateContext = localContextNames.Contains(type.Identifier.ValueText);
                    var declaresLocalStateContext = type.AttributeLists
                        .SelectMany(static list => list.Attributes)
                        .Any(static attribute =>
                            attribute.Name.ToString().EndsWith(
                                "LocalJsonStateContext",
                                StringComparison.Ordinal));
                    if (declaresLocalStateContext &&
                        !type.Modifiers.Any(static modifier =>
                            modifier.IsKind(SyntaxKind.InternalKeyword)))
                    {
                        offenders.Add(
                            $"{relative}: local JSON state context " +
                            $"'{type.Identifier.ValueText}' must be internal");
                    }

                    foreach (var attribute in type.AttributeLists.SelectMany(static list => list.Attributes)
                                 .Where(static a => a.Name.ToString().Contains("JsonSerializable", StringComparison.Ordinal)))
                    {
                        if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not TypeOfExpressionSyntax typeOf)
                            continue;

                        if (isLocalStateContext)
                        {
                            localRegisteredCount++;
                            continue;
                        }

                        if (aliases is null)
                        {
                            aliases = new Dictionary<string, string>(globalAliases, StringComparer.Ordinal);
                            foreach (var alias in AliasDirectives(root))
                                aliases[alias.Alias!.Name.Identifier.ValueText] = alias.Name!.ToString();
                        }

                        registeredCount++;
                        var written = UnwrapCollectionType(typeOf.Type.ToString());
                        var resolved = Resolve(written, aliases);
                        if (resolved.StartsWith(ContractNamespacePrefix, StringComparison.Ordinal))
                            continue;
                        if (resolved == OpenJsonValueCarrier)
                        {
                            intrinsicRegisteredCount++;
                            continue;
                        }

                        var line = attribute.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        offenders.Add(resolved == written
                            ? $"{relative}:{line}: {type.Identifier.ValueText} registers '{written}'"
                            : $"{relative}:{line}: {type.Identifier.ValueText} registers '{written}' (resolves to '{resolved}')");
                    }
                }
            }

            foreach (var (file, root) in roots)
            {
                var relative = repoRoot.GetRelativePathTo(file).ToString().Replace('\\', '/');
                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var invoked = invocation.Expression.ToString();
                    if (!invoked.EndsWith("JsonContent.Create", StringComparison.Ordinal) &&
                        !invoked.EndsWith("ReadFromJsonAsync", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var localContext = invocation.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Select(static identifier => identifier.Identifier.ValueText)
                        .FirstOrDefault(localContextNames.Contains);
                    if (localContext is not null)
                    {
                        var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        offenders.Add(
                            $"{relative}:{line}: collector boundary uses local JSON state context " +
                            $"'{localContext}'");
                    }
                }
            }

            if (offenders.Count > 0)
            {
                throw new InvalidOperationException(
                    "Qyl.Cli serializes a type it does not get from the contract:" + Environment.NewLine +
                    string.Join(Environment.NewLine, offenders) + Environment.NewLine +
                    "The CLI is a client of the collector API and owns none of it. Map the value to a " +
                    "Qyl.Api.Contracts type first — QylRunnerContractMapper is where that happens — and " +
                    "register the contract type. If the type IS a contract type, name it so the verifier " +
                    "can prove it: a `using Contract... = Qyl.Api.Contracts....;` alias or a fully-qualified name. " +
                    "JsonElement is allowed only as the runtime carrier for an open JSON member already owned " +
                    "by a generated contract.");
            }

            if (contextNames.Count is 0 || registeredCount is 0)
            {
                throw new InvalidOperationException(
                    "G10(a) found no JsonSerializerContext registrations under packages/Qyl.Cli, so it " +
                    "verified nothing. If the CLI's serialization moved, move this gate's scope with it.");
            }

            Log.Information(
                "Qyl.Cli collector boundaries serialize contract types only: {ContractRegistered} contract, " +
                "{IntrinsicRegistered} open-JSON intrinsic, and {LocalRegistered} local-state registrations " +
                "across {Contexts} JSON context(s)",
                registeredCount - intrinsicRegisteredCount,
                intrinsicRegisteredCount,
                localRegisteredCount,
                contextNames.Count);

            // A using alias is file-scoped for this purpose wherever it sits — after a file-scoped
            // `namespace X;` the directives attach to the namespace node, not the compilation unit.
            static IEnumerable<UsingDirectiveSyntax> AliasDirectives(CompilationUnitSyntax root) =>
                root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                    .Where(static u => u.Alias is not null && u.Name is not null);

            // The alias target may itself be an alias; resolve transitively, and stop rather than
            // spin if the source ever contains a cycle.
            static string Resolve(string typeName, Dictionary<string, string> aliases)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (aliases.TryGetValue(typeName, out var target) && seen.Add(typeName))
                    typeName = UnwrapCollectionType(target);

                return typeName;
            }

            static string UnwrapCollectionType(string typeName)
            {
                typeName = typeName.Trim();
                while (typeName.EndsWith("[]", StringComparison.Ordinal))
                    typeName = typeName[..^2].Trim();

                var open = typeName.IndexOf('<', StringComparison.Ordinal);
                var close = typeName.LastIndexOf('>');
                if (open > 0 && close > open)
                {
                    var outer = typeName[..open].Trim();
                    var outerName = outer[(outer.LastIndexOf('.') + 1)..];
                    var arguments = typeName[(open + 1)..close].Split(',');
                    if (arguments.Length is 1 && s_collectionWrappers.Contains(outerName, StringComparer.Ordinal))
                        return UnwrapCollectionType(arguments[0]);
                }

                return typeName;
            }
        });
}
