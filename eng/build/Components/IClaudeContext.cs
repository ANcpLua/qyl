using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Components;

/// <summary>
///     The "AI Context Compiler" (lol.claude.compiler).
///     Transforms a linked list of CLAUDE.md files into a single, token-efficient artifact.
/// </summary>
internal partial interface IClaudeContext : IHasSolution
{
    // ══════════════════════════════════════════════════════════════════════════
    // CONFIGURATION
    // ══════════════════════════════════════════════════════════════════════════

    AbsolutePath LeafClaude => RootDirectory / "CLAUDE.md";
    AbsolutePath CompiledArtifact => RootDirectory / ".claude" / "context.md";

    // ══════════════════════════════════════════════════════════════════════════
    // CLI TARGETS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// COMPILER (Local): Resolves the linked list and generates the runtime artifact.
    /// Run this before starting an AI session.
    /// </summary>
    Target GenerateContext => d => d
        .Description("Compiles the CLAUDE.md dependency chain into a flattened context file")
        .Executes(() =>
        {
            Log.Information("🧠 Compiler started: Resolving Dependency Graph...");

            // 1. Resolve Linked List (Leaf -> Root)
            var chain = ResolveLinkedList(LeafClaude, []);

            // 2. Compile (Root -> Leaf) to apply override logic
            var compilationUnit = new Dictionary<string, Section>();
            foreach (var node in Enumerable.Reverse(chain))
            {
                var layer = ParseNode(node);
                MergeLayer(compilationUnit, layer, node.Name);
            }

            // 3. Emit Artifact
            WriteArtifact(compilationUnit);
        });

    /// <summary>
    /// GATEKEEPER (CI): strictly validates the artifact against the Enterprise Schema.
    /// </summary>
    Target AuditContext => d => d
        .Description("Validates the compiled context against strict schema rules")
        .DependsOn(GenerateContext)
        .Executes(() =>
        {
            var content = CompiledArtifact.ReadAllText();

            // Schema Rule 1: Security is Mandatory
            if (!content.Contains("## Security"))
                throw new Exception("❌ COMPILER ERROR: Artifact missing mandatory 'Security' section.");

            // Schema Rule 2: Prescriptive Commands
            if (content.Contains("## Commands") && !content.Contains("```"))
                throw new Exception("❌ COMPILER ERROR: 'Commands' section too vague. Must contain code blocks.");

            Log.Information("✅ Artifact validated successfully.");
        });

    // ══════════════════════════════════════════════════════════════════════════
    // COMPILER ENGINE
    // ══════════════════════════════════════════════════════════════════════════

    private List<AbsolutePath> ResolveLinkedList(AbsolutePath current, List<AbsolutePath> visited)
    {
        while (true)
        {
            if (!current.FileExists())
            {
                if (visited.Count == 0) return []; // Allow missing leaf
                throw new Exception($"❌ LINK ERROR: Import target '{current}' not found.");
            }

            if (visited.Contains(current)) throw new Exception($"❌ CYCLE DETECTED: {current} imports itself.");

            visited.Add(current);

            // Scan for @import directive
            var content = current.ReadAllText();
            var match = MyRegex().Match(content);

            if (!match.Success) return visited; // Base case: Root reached
            var importPath = match.Groups["path"].Value;
            var nextNode = ResolvePath(current, importPath);
            current = nextNode;
        }
    }

    private AbsolutePath ResolvePath(AbsolutePath current, string importPath)
    {
        if (!importPath.StartsWith("~")) return current.Parent / importPath;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return importPath.Replace("~", home);
    }

    private class Section
    {
        public string Content;
        public bool Locked;
    }

    private void MergeLayer(Dictionary<string, Section> context, Dictionary<string, string> layer, string layerName)
    {
        foreach (var (header, content) in layer)
        {
            // STRATEGY A: SECURITY (Immutable/Locked)
            if (header.Contains("Security") || header.Contains("Policy"))
            {
                if (context.ContainsKey(header) && context[header].Locked) continue;
                context[header] = new Section { Content = content, Locked = true };
            }
            // STRATEGY B: COMMANDS (Additive/Append)
            else if (header.Contains("Commands"))
            {
                if (!context.TryGetValue(header, out var value))
                    context[header] = new Section { Content = content };
                else
                    value.Content += $"\n\n### [{layerName} Extension]\n{content}";
            }
            // STRATEGY C: HEURISTICS (Replacement/Override)
            else
            {
                context[header] = new Section { Content = content };
            }
        }
    }

    private Dictionary<string, string> ParseNode(AbsolutePath path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var regex = new Regex(@"^##\s+(?<header>.+?)\r?\n(?<content>.*?)(?=^##|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        foreach (Match match in regex.Matches(path.ReadAllText()))
        {
            var cleanContent =
                Regex.Replace(match.Groups["content"].Value, @"^@import.*$", "", RegexOptions.Multiline).Trim();
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
        sb.AppendLine($"");
        sb.AppendLine();

        // Enforce Reading Order
        var sortedKeys = context.Keys.OrderBy(k =>
            k.Contains("Security") ? 0 :
            k.Contains("Commands") ? 1 : 99);

        foreach (var key in sortedKeys)
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
}