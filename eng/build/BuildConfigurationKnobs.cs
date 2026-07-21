using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Qyl.Build;

interface IConfigurationKnobs : IHazSourcePaths
{
    Target VerifyConfigurationKnobs => d => d
        .Unlisted()
        .Description("Verify every QYL_* code binding has exactly one README configuration row")
        .Executes(() => ConfigurationKnobInventory.Verify(RootDirectory));
}

internal static partial class ConfigurationKnobInventory
{
    public static void Verify(AbsolutePath rootDirectory)
    {
        var codeBindings = ReadCodeBindings(rootDirectory);
        var documentedBindings = ReadDocumentedBindings(rootDirectory / "README.md");

        var missingDocumentation = codeBindings.Except(documentedBindings, StringComparer.Ordinal).ToArray();
        var missingImplementation = documentedBindings.Except(codeBindings, StringComparer.Ordinal).ToArray();

        foreach (var variable in missingDocumentation)
            Log.Error("{Variable} is bound in code but absent from the README configuration table: document or delete it",
                variable);

        foreach (var variable in missingImplementation)
            Log.Error("{Variable} is documented in the README configuration table but has no code binding: implement or remove the row",
                variable);

        if (missingDocumentation.Length > 0 || missingImplementation.Length > 0)
        {
            throw new InvalidOperationException(
                "README configuration must have a one-to-one correspondence with QYL_* environment bindings.");
        }

        Log.Information("README documents all {Count} QYL_* environment bindings exactly once", codeBindings.Count);
    }

    private static SortedSet<string> ReadCodeBindings(AbsolutePath rootDirectory)
    {
        var bindings = new SortedSet<string>(StringComparer.Ordinal);
        AbsolutePath[] csharpRoots =
        [
            rootDirectory / "packages",
            rootDirectory / "services",
            rootDirectory / "internal",
            rootDirectory / "eng" / "tools"
        ];

        foreach (var file in csharpRoots.SelectMany(static directory => directory.GlobFiles("**/*.cs"))
                     .Where(static file => !IsGenerated(file)))
        {
            var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetRoot();
            foreach (var literal in syntax.DescendantNodes().OfType<LiteralExpressionSyntax>()
                         .Where(static literal => literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)))
            {
                var value = literal.Token.ValueText;
                if (!IsQylVariable(value))
                    continue;

                if (literal.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax } ||
                    literal.Ancestors().OfType<InvocationExpressionSyntax>()
                        .Any(invocation => InvocationName(invocation) == "GetEnvironmentVariable") ||
                    literal.Ancestors().OfType<ElementAccessExpressionSyntax>().Any())
                {
                    bindings.Add(value);
                }
            }
        }

        foreach (var file in (rootDirectory / "packages").GlobFiles("**/*.ts", "**/*.tsx")
                     .Concat((rootDirectory / "services").GlobFiles("**/*.ts", "**/*.tsx"))
                     .Where(static file => !IsGenerated(file)))
        {
            AddMatches(bindings, TypeScriptBinding(), File.ReadAllText(file));
        }

        foreach (var file in (rootDirectory / "eng" / "scripts").GlobFiles("**/*.sh"))
            AddMatches(bindings, ShellBinding(), File.ReadAllText(file));

        return bindings;
    }

    private static SortedSet<string> ReadDocumentedBindings(AbsolutePath readme)
    {
        var text = File.ReadAllText(readme);
        const string heading = "## Configuration";
        var headingStart = text.IndexOf(heading, StringComparison.Ordinal);
        if (headingStart < 0)
            throw new InvalidOperationException("README.md must contain a '## Configuration' table.");

        var sectionStart = headingStart + heading.Length;
        var sectionEnd = text.IndexOf("\n## ", sectionStart, StringComparison.Ordinal);
        var section = sectionEnd < 0 ? text[sectionStart..] : text[sectionStart..sectionEnd];
        var matches = ConfigurationRow().Matches(section);
        var duplicates = matches.Cast<Match>()
            .Select(static match => match.Groups["name"].Value)
            .GroupBy(static name => name, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        if (duplicates.Length > 0)
            throw new InvalidOperationException(
                $"README configuration table contains duplicate rows: {string.Join(", ", duplicates)}");

        return new SortedSet<string>(
            matches.Cast<Match>().Select(static match => match.Groups["name"].Value),
            StringComparer.Ordinal);
    }

    private static void AddMatches(SortedSet<string> bindings, Regex regex, string text)
    {
        foreach (Match match in regex.Matches(text))
        {
            var name = match.Groups["name"].Success
                ? match.Groups["name"].Value
                : match.Groups["alternate"].Value;
            bindings.Add(name);
        }
    }

    private static string? InvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };

    private static bool IsGenerated(AbsolutePath file)
    {
        var path = file.ToString().Replace('\\', '/');
        return path.EndsWith(".g.cs", StringComparison.Ordinal) ||
               path.EndsWith(".generated.cs", StringComparison.Ordinal) ||
               path.Contains("/bin/", StringComparison.Ordinal) ||
               path.Contains("/obj/", StringComparison.Ordinal) ||
               path.Contains("/node_modules/", StringComparison.Ordinal);
    }

    private static bool IsQylVariable(string value) =>
        value.StartsWith("QYL_", StringComparison.Ordinal) &&
        value.Length > 4 &&
        value.All(static character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    [GeneratedRegex("""process\.env(?:\.(?<name>QYL_[A-Z0-9_]+)|\[\s*['"](?<alternate>QYL_[A-Z0-9_]+)['"]\s*\])""")]
    private static partial Regex TypeScriptBinding();

    [GeneratedRegex(@"\$\{(?<name>QYL_[A-Z0-9_]+)(?=[:}])|\$(?<alternate>QYL_[A-Z0-9_]+)")]
    private static partial Regex ShellBinding();

    [GeneratedRegex(@"^\|\s*`(?<name>QYL_[A-Z0-9_]+)`\s*\|", RegexOptions.Multiline)]
    private static partial Regex ConfigurationRow();
}
