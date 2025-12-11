using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Nuke.Common.IO;
using Serilog;

namespace Components;

[ExcludeFromCodeCoverage(Justification = "Build infrastructure - tested via integration")]
public sealed record ExclusionRule(
    string Name,
    string[]? PathContains = null,
    string[]? FileSuffixes = null,
    bool ShouldExclude = true)
{
    public bool Matches(string normalizedPath) =>
        (PathContains is { Length: > 0 } && Array.Exists(PathContains,
            p => normalizedPath.Contains(p, StringComparison.OrdinalIgnoreCase))) ||
        (FileSuffixes is { Length: > 0 } && Array.Exists(FileSuffixes,
            s => normalizedPath.EndsWith(s, StringComparison.OrdinalIgnoreCase)));

    public string CreateReasonTag() =>
        ShouldExclude ? $"ExcludedByRule({Name})" : $"TaggedByRule({Name})";
}

[ExcludeFromCodeCoverage(Justification = "Build infrastructure - tested via integration")]
public static class WellKnownExclusionPatterns
{
    public static readonly ExclusionRule SourceGeneratedFiles = new(
        "SourceGenerated",
        ["/obj/"],
        [".g.cs", ".generated.cs", ".designer.cs"]);

    public static readonly ExclusionRule Migrations = new(
        "Migrations",
        ["/Migrations/"]);

    public static readonly ExclusionRule InfrastructureCode = new(
        "Infrastructure",
        ["/Host/", "/Configuration/", "/Extensions/"]);

    public static readonly ExclusionRule PresentationWrappers = new(
        "PresentationWrapper",
        ["/Presentation/"],
        ["Endpoints.cs", "Listener.cs"],
        false);

    public static readonly ExclusionRule DataTransferObjects = new(
        "DTO",
        FileSuffixes: ["Dto.cs", "DTOs.cs", "Request.cs", "Response.cs"],
        ShouldExclude: false);

    public static readonly ExclusionRule MappingConfiguration = new(
        "MappingConfig",
        FileSuffixes: ["MappingConfig.cs", "Profile.cs"]);

    public static readonly IReadOnlyList<ExclusionRule> AllRules =
    [
        SourceGeneratedFiles,
        Migrations,
        InfrastructureCode,
        MappingConfiguration,
        PresentationWrappers,
        DataTransferObjects
    ];

    public static ExclusionRule? GetMatchingRule(string normalizedPath) =>
        AllRules.FirstOrDefault(rule => rule.Matches(normalizedPath));

    public static bool ShouldExcludePath(string normalizedPath) =>
        GetMatchingRule(normalizedPath) is { ShouldExclude: true };
}

[ExcludeFromCodeCoverage(Justification = "Build infrastructure - tested via integration")]
public static class StateMachinePatterns
{
    private const string _stateMachineMarker = "+<";
    private const string _stateMachineSuffix = ">d__";

    public static bool TryExtractStateMachineMethod(string className, out string? methodName)
    {
        methodName = null;

        var plusIndex = className.IndexOf(_stateMachineMarker, StringComparison.Ordinal);
        if (plusIndex < 0) return false;

        var methodStart = plusIndex + 2;
        var methodEnd = className.IndexOf(_stateMachineSuffix, methodStart, StringComparison.Ordinal);
        if (methodEnd <= methodStart) return false;

        methodName = className[methodStart..methodEnd];
        return methodName.Length > 0;
    }

    public static bool IsStateMachineClass(string className) =>
        className.Contains(_stateMachineMarker, StringComparison.Ordinal) &&
        className.Contains(">d__", StringComparison.Ordinal);

    public static bool IsMoveNextMethod(string methodName) =>
        methodName.Equals("MoveNext", StringComparison.Ordinal);

    public static string CreateStateMachineReason(string? originalMethodName) =>
        originalMethodName is { Length: > 0 }
            ? $"CompilerGeneratedStateMachine({originalMethodName})"
            : "CompilerGeneratedStateMachine";
}

[ExcludeFromCodeCoverage(Justification = "Build infrastructure - tested via integration")]
public static class CoverageSummaryConverter
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void ConvertPerProject(
        AbsolutePath coberturaPath,
        AbsolutePath sourceRoot,
        IReadOnlyDictionary<string, AbsolutePath> projectOutputs)
    {
        if (!coberturaPath.FileExists())
        {
            Log.Warning("Cobertura file not found: {Path}", coberturaPath);
            return;
        }

        var cobertura = XDocument.Load(coberturaPath);
        if (cobertura.Root is not { } coverageElement)
        {
            Log.Warning("Invalid Cobertura file: no root element");
            return;
        }

        var generatedAtUtc = DateTime.UtcNow;
        var relativeCoberturaPath = Path.GetFileName(coberturaPath);
        var sourceRootNormalized = NormalizePath(sourceRoot, null);

        var allFileIssues = ExtractAllFileIssues(coverageElement, sourceRootNormalized);

        foreach ((var projectName, var outputPath) in projectOutputs)
        {
            var projectFiles = allFileIssues
                .Where(kvp => kvp.Key.Contains(projectName, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            WriteProjectSummary(projectName, projectFiles, outputPath, generatedAtUtc, relativeCoberturaPath);
        }
    }

    private static Dictionary<string, CoverageFile> ExtractAllFileIssues(XElement coverageElement, string sourceRoot)
    {
        Dictionary<string, CoverageFile> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (var classElement in coverageElement.Descendants("class"))
        {
            var filename = classElement.Attribute("filename")?.Value;
            if (filename is not { Length: > 0 }) continue;

            var normalizedPath = NormalizePath(filename, sourceRoot);

            if (WellKnownExclusionPatterns.ShouldExcludePath(normalizedPath)) continue;

            var tagRule = WellKnownExclusionPatterns.GetMatchingRule(normalizedPath);
            var ruleTag = tagRule is { ShouldExclude: false } ? tagRule.CreateReasonTag() : null;

            var className = classElement.Attribute("name")?.Value;
            string? stateMachineMethod = null;
            if (className is { Length: > 0 } && StateMachinePatterns.IsStateMachineClass(className))
                StateMachinePatterns.TryExtractStateMachineMethod(className, out stateMachineMethod);

            ProcessClassIssues(classElement, normalizedPath, result, ruleTag, stateMachineMethod);
        }

        return result;
    }

    private static void ProcessClassIssues(
        XElement classElement,
        string normalizedPath,
        Dictionary<string, CoverageFile> result,
        string? ruleTag,
        string? stateMachineMethod)
    {
        var file = GetOrCreateFileIssues(result, normalizedPath);

        foreach (var line in classElement.Descendants("line"))
        {
            var hits = int.Parse(line.Attribute("hits")?.Value ?? "0", CultureInfo.InvariantCulture);
            var lineNumber = int.Parse(line.Attribute("number")?.Value ?? "0", CultureInfo.InvariantCulture);
            var isBranch = string.Equals(line.Attribute("branch")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var conditionCoverage = line.Attribute("condition-coverage")?.Value;

            if (isBranch && conditionCoverage is { Length: > 0 })
            {
                if (CreateBranchIssue(lineNumber, hits, conditionCoverage, ruleTag, stateMachineMethod) is
                    { } branchIssue)
                    file.BranchDict.TryAdd(lineNumber, branchIssue);
            }
            else if (hits is 0)
            {
                var reason = DetermineLineReason(ruleTag, stateMachineMethod);
                file.LineDict.TryAdd(lineNumber, new CoverageLine(lineNumber, 0, reason));
            }
        }
    }

    private static void WriteProjectSummary(
        string projectName,
        Dictionary<string, CoverageFile> fileIssues,
        AbsolutePath outputPath,
        DateTime generatedAtUtc,
        string sourceName)
    {
        var jsonSummary = new CoverageSummary
        {
            Project = projectName,
            Source = sourceName,
            GeneratedAtUtc = generatedAtUtc
        };

        XElement xmlSummary = new("coverage-summary",
            new XAttribute("project", projectName),
            new XAttribute("generatedAtUtc", generatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("source", sourceName));

        var filesWithIssues = 0;

        foreach ((var filePath, var file) in fileIssues.OrderBy(kvp => kvp.Key,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (file.LineDict.Count is 0 && file.BranchDict.Count is 0) continue;

            jsonSummary.Files.Add(new CoverageFileDto
            {
                Path = filePath,
                Lines = file.Lines.ToList(),
                Branches = file.Branches.ToList()
            });

            XElement xmlFile = new("file", new XAttribute("path", filePath));

            foreach (var branch in file.Branches)
                xmlFile.Add(new XElement("branch",
                    new XAttribute("line", branch.Line),
                    new XAttribute("coveredBranches", branch.CoveredBranches),
                    new XAttribute("totalBranches", branch.TotalBranches),
                    new XAttribute("coveragePercent",
                        branch.CoveragePercent.ToString("F1", CultureInfo.InvariantCulture)),
                    new XAttribute("hits", branch.Hits),
                    new XAttribute("reason", branch.Reason)));

            foreach (var line in file.Lines)
                xmlFile.Add(new XElement("line",
                    new XAttribute("number", line.Line),
                    new XAttribute("hits", line.Hits),
                    new XAttribute("reason", line.Reason)));

            xmlSummary.Add(xmlFile);
            filesWithIssues++;
        }

        EnsureDirectoryExists(outputPath);

        new XDocument(new XDeclaration("1.0", "utf-8", null), xmlSummary).Save(outputPath);

        var jsonPath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(outputPath) + ".json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonSummary, _jsonOptions));

        Log.Information("Coverage summary: {XmlPath} + {JsonPath} ({FileCount} files with issues)",
            outputPath, jsonPath, filesWithIssues);
    }

    private static string NormalizePath(string path, string? sourceRoot)
    {
        path = path.Replace((char)92, '/');

        if (sourceRoot is { Length: > 0 })
        {
            var normalizedRoot = sourceRoot.Replace((char)92, '/');
            if (!normalizedRoot.EndsWith('/')) normalizedRoot += '/';

            if (path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                path = path[normalizedRoot.Length..];
        }

        return path;
    }

    private static string DetermineLineReason(string? ruleTag, string? stateMachineMethod) =>
        stateMachineMethod is { Length: > 0 } ? StateMachinePatterns.CreateStateMachineReason(stateMachineMethod) :
        ruleTag is { Length: > 0 } ? ruleTag :
        "LineNotExecuted";

    private static CoverageBranch? CreateBranchIssue(
        int lineNumber, int hits, string conditionCoverage,
        string? ruleTag, string? stateMachineMethod)
    {
        if (ParseConditionCoverage(conditionCoverage) is not { } info)
        {
            if (hits is 0)
            {
                var reason = DetermineLineReason(ruleTag, stateMachineMethod);
                return new CoverageBranch(lineNumber, hits, 0, 1, 0, reason);
            }

            return null;
        }

        if (info.CoveredBranches >= info.TotalBranches) return null;

        var branchReason =
            stateMachineMethod is { Length: > 0 } ? StateMachinePatterns.CreateStateMachineReason(stateMachineMethod) :
            ruleTag is { Length: > 0 } ? ruleTag :
            info.CoveredBranches is 0 ? "BranchNotCovered" :
            "BranchPartiallyCovered";

        return new CoverageBranch(lineNumber, hits, info.CoveredBranches, info.TotalBranches, info.Percent,
            branchReason);
    }

    private static BranchCoverageInfo? ParseConditionCoverage(ReadOnlySpan<char> input)
    {
        var parenStart = input.IndexOf('(');
        var parenEnd = input.IndexOf(')');
        if (parenStart < 0 || parenEnd <= parenStart) return null;

        var percentPart = input[..parenStart].Trim();
        if (percentPart is not [.., '%']) return null;

        if (!double.TryParse(percentPart[..^1], NumberStyles.Float, CultureInfo.InvariantCulture,
                out var percent)) return null;

        var fraction = input[(parenStart + 1)..parenEnd];
        var slashIdx = fraction.IndexOf('/');
        if (slashIdx < 0) return null;

        if (!int.TryParse(fraction[..slashIdx], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var covered)) return null;

        if (!int.TryParse(fraction[(slashIdx + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var total))
            return null;

        return new BranchCoverageInfo(covered, total, percent);
    }

    private static CoverageFile GetOrCreateFileIssues(Dictionary<string, CoverageFile> dict, string path) =>
        dict.TryGetValue(path, out var issues) ? issues : dict[path] = new CoverageFile();

    private static void EnsureDirectoryExists(string path)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
    }

    private sealed class CoverageFile
    {
        [JsonIgnore]
        public Dictionary<int, CoverageLine> LineDict { get; } = [];

        [JsonIgnore]
        public Dictionary<int, CoverageBranch> BranchDict { get; } = [];

        public IEnumerable<CoverageLine> Lines => LineDict.Values.OrderBy(l => l.Line);
        public IEnumerable<CoverageBranch> Branches => BranchDict.Values.OrderBy(b => b.Line);
    }

    private sealed class CoverageSummary
    {
        public string Project { get; init; } = "";
        public string Source { get; init; } = "";
        public DateTime GeneratedAtUtc { get; init; }
        public List<CoverageFileDto> Files { get; } = [];
    }

    private sealed class CoverageFileDto
    {
        public string Path { get; init; } = "";
        public List<CoverageLine> Lines { get; init; } = [];
        public List<CoverageBranch> Branches { get; init; } = [];
    }

    private sealed record CoverageLine(int Line, int Hits, string Reason = "");

    private sealed record CoverageBranch(
        int Line,
        int Hits,
        int CoveredBranches,
        int TotalBranches,
        double CoveragePercent,
        string Reason = "");

    private sealed record BranchCoverageInfo(int CoveredBranches, int TotalBranches, double Percent);
}
