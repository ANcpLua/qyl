using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// The workflow's job graph and the <c>Ci</c> target are two hand-maintained answers to
/// "what is the gate". Nothing made them agree, and they drifted twice: <c>PackSmoke</c> and
/// the G1 vocabulary smoke ran only in CI, so the documented local gate was a strict subset of
/// the real one and a trim/AOT regression reached <c>main</c> past a green local run.
/// This gate removes the second answer's freedom to differ — every target and script CI invokes
/// must be reachable from <c>Ci</c>, so adding a CI step without wiring it in fails the build.
/// The reverse direction is deliberately unconstrained: <c>Ci</c> may run more than CI does.
/// </summary>
interface ICiCoverage : IHazSourcePaths
{
    AbsolutePath CiWorkflowFile => RootDirectory / ".github" / "workflows" / "ci.yml";

    AbsolutePath BuildSourceDirectory => RootDirectory / "eng" / "build";

    Target G1VocabularySmoke => d => d
        .Unlisted()
        .Description("Run the G1 telemetry-vocabulary smoke")
        .Executes(() =>
        {
            var script = RootDirectory / "eng" / "scripts" / "g1-vocabulary-smoke.sh";
            ProcessTasks.StartProcess("bash", $"\"{script}\"", RootDirectory, logOutput: true)
                .AssertZeroExitCode();
        });

    Target VerifyCiTargetCoversWorkflow => d => d
        .Unlisted()
        .Description("Verify every target and script CI invokes is reachable from the Ci target")
        .Executes(() =>
        {
            var workflow = CiWorkflowFile.ReadAllText();
            var offenders = new List<string>();

            var blocks = ReadTargetBlocks();
            if (!blocks.ContainsKey("Ci"))
                throw new InvalidOperationException("No 'Ci' target found in eng/build — this gate cannot compute coverage.");

            var dependencies = ReadTargetDependencies(blocks);
            var reachable = Reachable("Ci", dependencies);

            // `./eng/build.sh <Target> [--Args]` — the target is the first token after the script.
            foreach (Match match in Regex.Matches(workflow, @"\./eng/build\.sh\s+([A-Za-z][A-Za-z0-9_]*)"))
            {
                var target = match.Groups[1].Value;
                if (!reachable.Contains(target))
                    offenders.Add($"CI invokes target '{target}', which the Ci target does not reach");
            }

            // `bash eng/scripts/<name>.sh` — some target reachable from Ci must run the same script.
            // Attribution is per target block, not per file: this gate and G1VocabularySmoke share a
            // file, so a file-level match would let either one vouch for the other.
            foreach (Match match in Regex.Matches(workflow, @"eng/scripts/([A-Za-z0-9._-]+\.sh)"))
            {
                var script = match.Groups[1].Value;
                var runsIt = blocks
                    .Where(block => block.Value.Contains(script, StringComparison.Ordinal))
                    .Select(static block => block.Key)
                    .ToList();

                if (!runsIt.Any(reachable.Contains))
                {
                    offenders.Add(runsIt.Count == 0
                        ? $"CI runs script 'eng/scripts/{script}', which no build target runs"
                        : $"CI runs script 'eng/scripts/{script}', run only by [{string.Join(", ", runsIt)}] which Ci does not reach");
                }
            }

            if (!Regex.IsMatch(
                    workflow,
                    @"(?m)^\s*run:\s+timeout\b[^\r\n]*\bdotnet\s+test\b",
                    RegexOptions.CultureInvariant))
            {
                offenders.Add("CI's dotnet test command is not bounded by the timeout process guard");
            }

            if (offenders.Count > 0)
            {
                throw new InvalidOperationException(
                    "The Ci target no longer covers the CI workflow:" + Environment.NewLine +
                    string.Join(Environment.NewLine, offenders.Distinct(StringComparer.Ordinal)));
            }

            Log.Information("Ci reaches every workflow-invoked target and script ({Count} targets reachable)", reachable.Count);
        });

    /// <summary>
    /// Maps each target name to its <c>Target X =&gt; d =&gt; d ...</c> source block, spanning from its
    /// declaration to the next one. Text-scanned rather than resolved through Nuke's model because
    /// the target graph is only materialised for the invoked target, and this gate must see all of it.
    /// </summary>
    private Dictionary<string, string> ReadTargetBlocks()
    {
        var declaration = new Regex(@"\bTarget\s+([A-Za-z][A-Za-z0-9_]*)\s*=>", RegexOptions.Compiled);
        var blocks = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in BuildSourceDirectory.GlobFiles("*.cs"))
        {
            var text = file.ReadAllText();
            var declarations = declaration.Matches(text).ToList();

            for (var i = 0; i < declarations.Count; i++)
            {
                var name = declarations[i].Groups[1].Value;
                var start = declarations[i].Index;
                var end = i + 1 < declarations.Count ? declarations[i + 1].Index : text.Length;
                var body = text[start..end];

                blocks[name] = blocks.TryGetValue(name, out var existing)
                    ? existing + Environment.NewLine + body
                    : body;
            }
        }

        return blocks;
    }

    /// <summary>
    /// Reads the dependency edges out of each block. Covers both <c>.DependsOn(Local)</c> and
    /// <c>.DependsOn&lt;IFace&gt;(static x =&gt; x.Other)</c>.
    /// </summary>
    private static Dictionary<string, List<string>> ReadTargetDependencies(Dictionary<string, string> blocks)
    {
        var dependsOn = new Regex(
            @"\.DependsOn(?:<[^>]+>)?\(\s*(?:static\s+)?(?:[A-Za-z_][A-Za-z0-9_]*\s*=>\s*[A-Za-z_][A-Za-z0-9_]*\s*\.)?([A-Za-z][A-Za-z0-9_]*)\s*\)",
            RegexOptions.Compiled);

        return blocks.ToDictionary(
            static block => block.Key,
            block => dependsOn.Matches(block.Value).Select(static m => m.Groups[1].Value).ToList(),
            StringComparer.Ordinal);
    }

    private static HashSet<string> Reachable(string root, Dictionary<string, List<string>> dependencies)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { root };
        var queue = new Queue<string>([root]);

        while (queue.Count > 0)
        {
            if (!dependencies.TryGetValue(queue.Dequeue(), out var edges)) continue;
            foreach (var edge in edges.Where(edge => seen.Add(edge)))
                queue.Enqueue(edge);
        }

        return seen;
    }
}
