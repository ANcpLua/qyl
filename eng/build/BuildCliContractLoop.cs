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
/// generator is actually told to emit — the roots of a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> —
/// not what types exist in the assembly. Internal domain records (<c>QylResourceState</c>,
/// <c>QylLogLine</c>) are legitimate and must never be flagged: they sit behind
/// <c>QylRunnerContractMapper</c>, which converts them to contract types before anything is
/// written, so they are unreachable from a serialization root.
///
/// Registered types must be *provably* contract-owned, through a <c>using</c> alias or a
/// fully-qualified name. A bare identifier the verifier cannot resolve is reported rather than
/// assumed innocent: a check that guesses is a check that passes when it should not.
/// </summary>
interface ICliContractLoop : IHazSourcePaths
{
    private const string ContractNamespacePrefix = "Qyl.Api.Contracts.";

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

            var offenders = new List<string>();
            var registeredCount = 0;
            var contextCount = 0;

            foreach (var (file, root) in roots)
            {
                var contexts = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(static type => type.BaseList?.Types.Any(static baseType =>
                        baseType.Type.ToString().EndsWith("JsonSerializerContext", StringComparison.Ordinal)) == true)
                    .ToList();

                if (contexts.Count is 0)
                    continue;

                contextCount += contexts.Count;
                var aliases = new Dictionary<string, string>(globalAliases, StringComparer.Ordinal);
                foreach (var alias in AliasDirectives(root))
                    aliases[alias.Alias!.Name.Identifier.ValueText] = alias.Name!.ToString();

                var relative = repoRoot.GetRelativePathTo(file).ToString().Replace('\\', '/');

                foreach (var context in contexts)
                {
                    foreach (var attribute in context.AttributeLists.SelectMany(static list => list.Attributes)
                                 .Where(static a => a.Name.ToString().Contains("JsonSerializable", StringComparison.Ordinal)))
                    {
                        if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not TypeOfExpressionSyntax typeOf)
                            continue;

                        registeredCount++;
                        var written = UnwrapCollectionType(typeOf.Type.ToString());
                        var resolved = Resolve(written, aliases);
                        if (resolved.StartsWith(ContractNamespacePrefix, StringComparison.Ordinal))
                            continue;

                        var line = attribute.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        offenders.Add(resolved == written
                            ? $"{relative}:{line}: {context.Identifier.ValueText} registers '{written}'"
                            : $"{relative}:{line}: {context.Identifier.ValueText} registers '{written}' (resolves to '{resolved}')");
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
                    "can prove it: a `using Contract... = Qyl.Api.Contracts....;` alias or a fully-qualified name.");
            }

            Log.Information(
                "Qyl.Cli serializes contract types only: {Registered} registrations across {Contexts} JSON context(s)",
                registeredCount, contextCount);

            static IEnumerable<UsingDirectiveSyntax> AliasDirectives(CompilationUnitSyntax root) =>
                root.Usings.Where(static u => u.Alias is not null && u.Name is not null);

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
                    var arguments = typeName[(open + 1)..close].Split(',');
                    // A single-argument collection (List<T>, IReadOnlyList<T>) serializes T; a
                    // multi-argument generic is left whole so it is reported rather than guessed at.
                    if (arguments.Length is 1)
                        return UnwrapCollectionType(arguments[0]);
                }

                return typeName;
            }
        });
}
