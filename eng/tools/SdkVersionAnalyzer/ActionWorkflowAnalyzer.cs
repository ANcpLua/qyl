// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
// qyl adaptation: tolerates repos without .github/actions and setup-dotnet steps
// that use global-json-file instead of a literal dotnet-version.

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace SdkVersionAnalyzer;

internal static class ActionWorkflowAnalyzer
{
    public static bool VerifyVersions(string root, DotnetSdkVersion expectedDotnetSdkVersion)
    {
        return FileAnalyzer.VerifyMultiple(GetYamlFiles(root), VerifySdkVersions, expectedDotnetSdkVersion);
    }

    public static void ModifyVersions(string root, DotnetSdkVersion newDotnetSdkVersion)
    {
        FileAnalyzer.ModifyMultiple(GetYamlFiles(root), ModifySdkVersions, newDotnetSdkVersion);
    }

    private static string ModifySdkVersions(string content, DotnetSdkVersion newDotnetSdkVersion)
    {
        using var stringReader = new StringReader(content);
        var scanner = new Scanner(stringReader, skipComments: false);
        var parser = new Parser(scanner);
        List<(int Start, int End, string Replacement)> replacements = [];

        while (parser.MoveNext())
        {
            var current = parser.Current;
            if (current is Scalar { IsKey: true, Value: "dotnet-version" } scalar)
            {
                if (!parser.MoveNext() || parser.Current is not Scalar valueScalar)
                {
                    throw new InvalidOperationException("dotnet-version key must have a scalar value.");
                }

                replacements.Add((
                    checked((int)valueScalar.Start.Index),
                    checked((int)valueScalar.End.Index),
                    GetNewDotnetVersionScalar(content, scalar, valueScalar, newDotnetSdkVersion)));
            }
        }

        if (replacements.Count == 0)
        {
            return content;
        }

        var stringBuilder = new System.Text.StringBuilder(content.Length);
        var currentPosition = 0;

        foreach (var (start, end, replacement) in replacements.OrderBy(r => r.Start))
        {
            stringBuilder = stringBuilder
                .Append(content, currentPosition, start - currentPosition)
                .Append(replacement);
            currentPosition = end;
        }

        return stringBuilder.Append(content, currentPosition, content.Length - currentPosition).ToString();
    }

    private static string GetNewDotnetVersionScalar(string content, Scalar keyScalar, Scalar valueScalar, DotnetSdkVersion newDotnetSdkVersion)
    {
        var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var indentation = GetLineIndentation(content, checked((int)keyScalar.Start.Index)) + "  ";
        string?[] versions = [newDotnetSdkVersion.Net8SdkVersion, newDotnetSdkVersion.Net9SdkVersion, newDotnetSdkVersion.Net10SdkVersion];
        List<string> lines = ["|"];
        lines.AddRange(versions.Where(v => v is not null).Select(v => $"{indentation}{v}"));
        var replacement = string.Join(newline, lines);

        var originalValue = content[checked((int)valueScalar.Start.Index)..checked((int)valueScalar.End.Index)];
        if (originalValue.EndsWith(newline, StringComparison.Ordinal))
        {
            replacement += newline;
        }

        return replacement;
    }

    private static string GetLineIndentation(string content, int index)
    {
        var lineStart = content.LastIndexOf('\n', Math.Max(index - 1, 0));
        lineStart = lineStart == -1 ? 0 : lineStart + 1;
        return content[lineStart..index];
    }

    private static string[] GetYamlFiles(string root)
    {
        var workflowsDirectory = GetWorkflowsDirectory(root);
        var actionsDirectory = GetActionsDirectory(root);

        var workflows = Directory.Exists(workflowsDirectory)
            ? Directory.GetFiles(workflowsDirectory, "*.yml")
            : [];
        var actions = Directory.Exists(actionsDirectory)
            ? Directory.GetFiles(actionsDirectory, "action.yml", SearchOption.AllDirectories)
            : [];
        return [.. workflows, .. actions];
    }

    private static string GetWorkflowsDirectory(string root)
    {
        return Path.Combine(root, ".github", "workflows");
    }

    private static string GetActionsDirectory(string root)
    {
        return Path.Combine(root, ".github", "actions");
    }

    private static bool VerifySdkVersions(string content, DotnetSdkVersion expectedDotnetSdkVersion)
    {
        foreach (var dotnetVersionNode in ExtractDotnetVersionNodes(content))
        {
            if (ContainsGitHubExpression(dotnetVersionNode))
            {
                Console.WriteLine(".NET SDK version must be a literal value. GitHub expression substitutions are not allowed in dotnet-version.");
                return false;
            }

            var extractedSdkVersion = ExtractVersion(dotnetVersionNode);
            if (extractedSdkVersion is null)
            {
                continue;
            }

            if (!VersionComparer.CompareVersions(expectedDotnetSdkVersion, extractedSdkVersion))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<YamlScalarNode> ExtractDotnetVersionNodes(string content)
    {
        var workflow = new YamlStream();
        using var stringReader = new StringReader(content);
        workflow.Load(stringReader);

        foreach (var stepGroup in ExtractStepGroups(workflow))
        {
            foreach (var step in stepGroup)
            {
                if (step is not YamlMappingNode jobStepNode)
                {
                    continue;
                }

                if (!jobStepNode.Children.TryGetValue(new YamlScalarNode("uses"), out var usesNode) ||
                    !usesNode.ToString().StartsWith("actions/setup-dotnet", StringComparison.Ordinal))
                {
                    continue;
                }

                // Steps using global-json-file (or no version at all) have nothing to verify;
                // global.json itself is the source of truth.
                if (jobStepNode.Children.TryGetValue(new YamlScalarNode("with"), out var withValue) &&
                    withValue is YamlMappingNode withNode &&
                    withNode.Children.TryGetValue(new YamlScalarNode("dotnet-version"), out var dotnetVersionValue) &&
                    dotnetVersionValue is YamlScalarNode dotnetVersionNode)
                {
                    yield return dotnetVersionNode;
                }
            }
        }
    }

    private static bool ContainsGitHubExpression(YamlScalarNode dotnetVersionNode)
    {
        return dotnetVersionNode.ToString().Contains("${{", StringComparison.Ordinal);
    }

    private static DotnetSdkVersion? ExtractVersion(YamlScalarNode dotnetVersionNode)
    {
        // Extract versions from the node value e.g.:
        // dotnet-version: |
        //   8.0.404
        //   9.0.100
        //   10.0.100

        string? sdk8Version = null;
        string? sdk9Version = null;
        string? sdk10Version = null;

        foreach (var version in dotnetVersionNode.ToString().Split())
        {
            if (VersionComparer.IsNet8Version(version))
            {
                sdk8Version = version;
            }

            if (VersionComparer.IsNet9Version(version))
            {
                sdk9Version = version;
            }

            if (VersionComparer.IsNet10Version(version))
            {
                sdk10Version = version;
            }
        }

        return sdk8Version is not null || sdk9Version is not null || sdk10Version is not null
            ? new DotnetSdkVersion(sdk8Version, sdk9Version, sdk10Version)
            : null;
    }

    private static IEnumerable<YamlSequenceNode> ExtractStepGroups(YamlStream yaml)
    {
        if (yaml.Documents[0].RootNode is not YamlMappingNode rootNode)
        {
            yield break;
        }

        if (rootNode.Children.TryGetValue(new YamlScalarNode("jobs"), out var jobsNode))
        {
            foreach (var job in ((YamlMappingNode)jobsNode).Children.Select(j => (YamlMappingNode)j.Value))
            {
                if (job.Children.TryGetValue(new YamlScalarNode("steps"), out var stepsNode))
                {
                    yield return (YamlSequenceNode)stepsNode;
                }
            }
        }

        if (rootNode.Children.TryGetValue(new YamlScalarNode("runs"), out var runsNode)
            && runsNode is YamlMappingNode runsMapping
            && runsMapping.Children.TryGetValue(new YamlScalarNode("steps"), out var actionStepsNode))
        {
            yield return (YamlSequenceNode)actionStepsNode;
        }
    }
}
