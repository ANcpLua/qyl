using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Components;

internal partial interface IClaudeContext : IHasSolution
{
    AbsolutePath LeafClaude => RootDirectory / "CLAUDE.md";
    AbsolutePath CompiledArtifact => RootDirectory / ".claude" / "context.md";

    Target GenerateContext => d => d
        .Description("Compiles the CLAUDE.md dependency chain into a flattened context file")
        .Executes(() =>
        {
            Log.Information("🧠 Compiler started: Resolving Dependency Graph...");

            List<AbsolutePath> chain = ResolveLinkedList(LeafClaude, []);

            var compilationUnit = new Dictionary<string, Section>();
            foreach (AbsolutePath node in Enumerable.Reverse(chain))
            {
                Dictionary<string, string> layer = ParseNode(node);
                MergeLayer(compilationUnit, layer, node.Name);
            }

            WriteArtifact(compilationUnit);
        });

    Target AuditContext => d => d
        .Description("Validates the compiled context against strict schema rules")
        .DependsOn(GenerateContext)
        .Executes(() =>
        {
            string? content = CompiledArtifact.ReadAllText();

            if (!content.Contains("## Security", StringComparison.Ordinal))
                throw new Exception("❌ COMPILER ERROR: Artifact missing mandatory 'Security' section.");

            if (content.Contains("## Commands", StringComparison.Ordinal) && !content.Contains("```", StringComparison.Ordinal))
                throw new Exception("❌ COMPILER ERROR: 'Commands' section too vague. Must contain code blocks.");

            Log.Information("✅ Artifact validated successfully.");
        });

    private List<AbsolutePath> ResolveLinkedList(AbsolutePath current, List<AbsolutePath> visited)
    {
        while (true)
        {
            if (!current.FileExists())
            {
                if (visited.Count == 0) return [];
                throw new Exception($"❌ LINK ERROR: Import target '{current}' not found.");
            }

            if (visited.Contains(current)) throw new Exception($"❌ CYCLE DETECTED: {current} imports itself.");

            visited.Add(current);

            string? content = current.ReadAllText();
            Match match = MyRegex().Match(content);

            if (!match.Success) return visited;
            string importPath = match.Groups["path"].Value;
            AbsolutePath nextNode = ResolvePath(current, importPath);
            current = nextNode;
        }
    }

    private AbsolutePath ResolvePath(AbsolutePath current, string importPath)
    {
        if (!importPath.StartsWith('~')) return current.Parent / importPath;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return importPath.Replace("~", home, StringComparison.Ordinal);
    }

    private void MergeLayer(Dictionary<string, Section> context, Dictionary<string, string> layer, string layerName)
    {
        foreach ((string header, string content) in layer)

            if (header.Contains("Security", StringComparison.Ordinal) || header.Contains("Policy", StringComparison.Ordinal))
            {
                if (context.TryGetValue(header, out Section? existing) && existing.Locked)
                    continue;

                context[header] = new Section
                {
                    Content = content,
                    Locked = true
                };
            }

            else if (header.Contains("Commands", StringComparison.Ordinal))
            {
                if (!context.TryGetValue(header, out Section? value))
                    context[header] = new Section
                    {
                        Content = content
                    };
                else
                    value.Content += $"\n\n### [{layerName} Extension]\n{content}";
            }

            else
            {
                context[header] = new Section
                {
                    Content = content
                };
            }
    }

    private Dictionary<string, string> ParseNode(AbsolutePath path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var regex = new Regex(@"^##\s+(?<header>.+?)\r?\n(?<content>.*?)(?=^##|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        foreach (Match match in regex.Matches(path.ReadAllText()))
        {
            string cleanContent =
                MyRegex1().Replace(match.Groups["content"].Value, "").Trim();
            if (!string.IsNullOrWhiteSpace(cleanContent))
                result[match.Groups["header"].Value.Trim()] = cleanContent;
        }

        return result;
    }

    private void WriteArtifact(Dictionary<string, Section> context)
    {
        CompiledArtifact.Parent.CreateDirectory();
        var sb = new StringBuilder();

        sb.AppendLine("");
        sb.AppendLine("");
        sb.AppendLine();

        IOrderedEnumerable<string> sortedKeys = context.Keys.OrderBy(k =>
            k.Contains("Security", StringComparison.Ordinal) ? 0 :
            k.Contains("Commands", StringComparison.Ordinal) ? 1 : 99);

        foreach (string key in sortedKeys)
        {
            sb.AppendLine($"## {key}");
            sb.AppendLine(context[key].Content);
            sb.AppendLine();
        }

        CompiledArtifact.WriteAllText(sb.ToString());
        Log.Information("💾 Compiled Artifact: {Path}", CompiledArtifact);
    }

    [GeneratedRegex("""
                    ^@import\s+"(?<path>.+)"
                    """, RegexOptions.Multiline)]
    private static partial Regex MyRegex();

    [GeneratedRegex("^@import.*$", RegexOptions.Multiline)]
    private static partial Regex MyRegex1();

    private class Section
    {
        public required string Content;
        public bool Locked;
    }
}
