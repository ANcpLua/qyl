// =============================================================================
// qyl Build System - Test & Coverage Components
// =============================================================================
// Test execution (xUnit v3 + MTP), coverage reporting, quality gates
// =============================================================================


// ════════════════════════════════════════════════════════════════════════════════
// MTP (Microsoft Testing Platform) Extensions
// ════════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Utilities;
using Nuke.Components;
using Serilog;

public static class MtpExtensions
{
    public static MtpArgumentsBuilder Mtp() => new();
}

public sealed class MtpArgumentsBuilder
{
    readonly List<string> _args = [];

    MtpArgumentsBuilder AddFilter(string option, params ReadOnlySpan<string> patterns)
    {
        foreach (var p in patterns)
        {
            if (string.IsNullOrEmpty(p)) continue;
            _args.Add(option);
            _args.Add(p);
        }

        return this;
    }

    MtpArgumentsBuilder AddOption(string option, string value)
    {
        _args.Add(option);
        _args.Add(value);
        return this;
    }

    MtpArgumentsBuilder AddFlag(string option)
    {
        _args.Add(option);
        _args.Add("on");
        return this;
    }

    // ─── Filters ────────────────────────────────────────────────────────────
    public MtpArgumentsBuilder FilterNamespace(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-namespace", patterns);

    public MtpArgumentsBuilder FilterNotNamespace(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-not-namespace", patterns);

    public MtpArgumentsBuilder FilterClass(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-class", patterns);

    public MtpArgumentsBuilder FilterNotClass(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-not-class", patterns);

    public MtpArgumentsBuilder FilterMethod(params ReadOnlySpan<string> patterns) =>
        AddFilter("--filter-method", patterns);

    public MtpArgumentsBuilder FilterTrait(string name, string value) =>
        AddOption("--filter-trait", $"{name}={value}");

    public MtpArgumentsBuilder FilterNotTrait(string name, string value) =>
        AddOption("--filter-not-trait", $"{name}={value}");

    public MtpArgumentsBuilder FilterQuery(string? expression) =>
        string.IsNullOrEmpty(expression) ? this : AddOption("--filter-query", expression);

    // ─── Reports ────────────────────────────────────────────────────────────
    public MtpArgumentsBuilder ReportTrx(string filename)
    {
        _args.Add("--report-trx");
        return AddOption("--report-trx-filename", filename);
    }

    public MtpArgumentsBuilder ReportXunit(string filename)
    {
        _args.Add("--report-xunit");
        return AddOption("--report-xunit-filename", filename);
    }

    public MtpArgumentsBuilder ReportJunit(string filename)
    {
        _args.Add("--report-junit");
        return AddOption("--report-junit-filename", filename);
    }

    // ─── Execution Options ──────────────────────────────────────────────────
    public MtpArgumentsBuilder StopOnFail() => AddFlag("--stop-on-fail");

    public MtpArgumentsBuilder MaxThreads(int count) =>
        AddOption("--max-threads", count.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder Timeout(TimeSpan duration) =>
        AddOption("--timeout", $"{(int)duration.TotalSeconds}s");

    public MtpArgumentsBuilder IgnoreExitCode(int code) =>
        AddOption("--ignore-exit-code", code.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder MinimumExpectedTests(int count) =>
        AddOption("--minimum-expected-tests", count.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder Seed(int seed) =>
        AddOption("--seed", seed.ToString(CultureInfo.InvariantCulture));

    public MtpArgumentsBuilder ShowLiveOutput() => AddFlag("--show-live-output");

    public MtpArgumentsBuilder Diagnostics() => AddFlag("--xunit-diagnostics");

    // ─── Coverage ───────────────────────────────────────────────────────────
    public MtpArgumentsBuilder CoverageCobertura(AbsolutePath outputPath)
    {
        _args.Add("--coverage");
        _args.Add("--coverage-output-format");
        _args.Add("cobertura");
        return AddOption("--coverage-output", outputPath.ToString());
    }

    // ─── Output ─────────────────────────────────────────────────────────────
    public string Build()
    {
        if (_args is []) return string.Empty;

        StringBuilder sb = new();
        foreach (var arg in _args)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(arg.DoubleQuoteIfNeeded());
        }

        return sb.ToString();
    }

    public IReadOnlyList<string> BuildArgs() => _args;

    /// <summary>
    ///     Builds a properly-escaped argument string for passthrough after --.
    ///     Returns empty string if no args, otherwise "-- arg1 arg2 ...".
    /// </summary>
    public string BuildProcessArgs()
    {
        if (_args.Count is 0) return string.Empty;
        return "-- " + string.Join(" ", _args.Select(StringExtensions.DoubleQuoteIfNeeded));
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// IQylTest - Test Execution via Nuke.Components.ITest + MTP
// ════════════════════════════════════════════════════════════════════════════════

[ParameterPrefix(nameof(IQylTest))]
interface IQylTest : Nuke.Components.ITest, IHazSourcePaths
{
    [Parameter("Test filter expression (xUnit v3 query syntax)")]
    string? TestFilter => TryGetValue(() => TestFilter);

    [Parameter("Stop on first test failure")]
    bool? StopOnFail => TryGetValue<bool?>(() => StopOnFail);

    [Parameter("Show live test output")]
    bool? LiveOutput => TryGetValue<bool?>(() => LiveOutput);

    // Override test project discovery (tests/ dir, not *.Tests naming)
    IEnumerable<Project> Nuke.Components.ITest.TestProjects =>
        Solution.AllProjects.Where(static p =>
            p.Path?.ToString().Contains("/tests/", StringComparison.Ordinal) == true);

    // Override results directory
    Configure<DotNetTestSettings> Nuke.Components.ITest.TestSettings => s =>
    {
        EnsureTestcontainersConfigured();
        return s.SetResultsDirectory(TestResultsDirectory);
    };

    // MTP arguments per project (replaces ExecuteMtpTestInternal)
    // .NET 10 SDK: dotnet test requires --project flag (positional arg removed)
    Configure<DotNetTestSettings, Project> Nuke.Components.ITest.TestProjectSettings => (s, project) =>
    {
        var mtp = MtpExtensions.Mtp()
            .ReportTrx($"{project.Name}.trx")
            .IgnoreExitCode(8);

        if (TestFilter is { Length: > 0 } f) mtp.FilterQuery(f);
        if (StopOnFail == true) mtp.StopOnFail();
        if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

        // NUKE 10.1.0 uses positional arg for project, but .NET 10 requires --project flag.
        // Clear --logger (conflicts with MTP's --report-trx), reset positional arg,
        // pass --project + MTP args via additional arguments.
        string[] additionalArgs = ["--project", project.Path!.ToString(), .. mtp.BuildArgs().Prepend("--")];
        return s.ClearLoggers().ResetProjectFile().SetProcessAdditionalArguments(additionalArgs);
    };

    sealed void EnsureTestcontainersConfigured()
    {
        if (IsServerBuild)
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///var/run/docker.sock");
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "false");
            Log.Information("Testcontainers: Configured for CI");
        }
    }

    Target UnitTests => d => d
        .Description("Run unit tests only")
        .DependsOn<Nuke.Components.ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            DotNetTasks.DotNetTest(s => s
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetResultsDirectory(TestResultsDirectory)
                .CombineWith(TestProjects, (ss, project) =>
                {
                    var mtp = MtpExtensions.Mtp()
                        .ReportTrx($"{project.Name}.Unit.trx")
                        .IgnoreExitCode(8)
                        .FilterNamespace("*.Unit.*");

                    if (StopOnFail == true) mtp.StopOnFail();
                    if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

                    string[] unitArgs = ["--project", project.Path!.ToString(), .. mtp.BuildArgs().Prepend("--")];
                    return ss.SetProcessAdditionalArguments(unitArgs);
                }), completeOnFailure: true);
        });

    Target IntegrationTests => d => d
        .Description("Run integration tests only")
        .DependsOn<Nuke.Components.ICompile>(static x => x.Compile)
        .Executes(() =>
        {
            EnsureTestcontainersConfigured();
            DotNetTasks.DotNetTest(s => s
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetResultsDirectory(TestResultsDirectory)
                .CombineWith(TestProjects, (ss, project) =>
                {
                    var mtp = MtpExtensions.Mtp()
                        .ReportTrx($"{project.Name}.Integration.trx")
                        .IgnoreExitCode(8)
                        .FilterNamespace("*.Integration.*");

                    if (StopOnFail == true) mtp.StopOnFail();
                    if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

                    string[] integrationArgs = ["--project", project.Path!.ToString(), .. mtp.BuildArgs().Prepend("--")];
                    return ss.SetProcessAdditionalArguments(integrationArgs);
                }), completeOnFailure: true);
        });
}

// ════════════════════════════════════════════════════════════════════════════════
// ICoverage - Code Coverage
// ════════════════════════════════════════════════════════════════════════════════

interface ICoverage : IQylTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    Target Coverage => d => d
        .Description("Run tests with code coverage")
        .DependsOn<Nuke.Components.ICompile>(static x => x.Compile)
        .Produces(CoverageDirectory / "**")
        .Executes(() =>
        {
            EnsureTestcontainersConfigured();
            CoverageDirectory.CreateOrCleanDirectory();
            TestResultsDirectory.CreateOrCleanDirectory();

            foreach (var project in TestProjects)
            {
                var coverageFile = CoverageDirectory / $"{project.Name}.cobertura.xml";
                var mtp = MtpExtensions.Mtp()
                    .ReportTrx($"{project.Name}.trx")
                    .IgnoreExitCode(8)
                    .CoverageCobertura(coverageFile);

                if (TestFilter is { Length: > 0 } f) mtp.FilterQuery(f);
                if (StopOnFail == true) mtp.StopOnFail();
                if (LiveOutput == true || IsLocalBuild) mtp.ShowLiveOutput();

                string[] coverageArgs = ["--project", project.Path!.ToString(), .. mtp.BuildArgs().Prepend("--")];
                DotNetTasks.DotNetTest(s => s
                    .SetConfiguration(((IHazConfiguration)this).Configuration)
                    .SetNoBuild(true)
                    .SetNoRestore(true)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetProcessAdditionalArguments(coverageArgs));
            }

            GenerateCoverageReports();
            GenerateAiCoverageSummary();
            GenerateDetailedCoverageSummaries();
        });

    private void GenerateCoverageReports()
    {
        var coverageFiles = CoverageDirectory.GlobFiles("*.cobertura.xml");

        if (coverageFiles.Count is 0)
        {
            Log.Warning("No coverage files found in {Directory}", CoverageDirectory);
            return;
        }

        Log.Information("Generating coverage reports from {Count} file(s)", coverageFiles.Count);

        var settings = new ReportGeneratorSettings()
            .SetReports(coverageFiles.Select(static f => f.ToString()))
            .SetTargetDirectory(CoverageDirectory)
            .SetReportTypes(
                ReportTypes.Html,
                ReportTypes.Cobertura,
                ReportTypes.TextSummary,
                ReportTypes.Badges)
            .SetAssemblyFilters("-Microsoft.*", "-System.*", "-xunit.*", "-*.tests")
            .SetClassFilters("-*.Migrations.*", "-*.Generated.*", "-*+<*>d__*");

        if (IsServerBuild && this is Build { Versioning: { } version })
            settings = settings.SetTag(version.FullSemVer);

        ReportGeneratorTasks.ReportGenerator(settings);

        Log.Information("Coverage reports generated: {Directory}", CoverageDirectory);
    }

    private void GenerateAiCoverageSummary()
    {
        var summaryFile = CoverageDirectory / "Summary.txt";
        var jsonOutput = CoverageDirectory / "coverage.summary.json";

        if (!summaryFile.FileExists())
        {
            Log.Warning("Summary.txt not found, skipping AI summary generation");
            return;
        }

        try
        {
            var lines = File.ReadAllLines(summaryFile);
            var summary = ParseCoverageSummary(lines);
            File.WriteAllText(jsonOutput, JsonSerializer.Serialize(summary, JsonOptions));
            Log.Information("AI coverage summary: {Path}", jsonOutput);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to generate AI coverage summary");
        }
    }

    private static Dictionary<string, object> ParseCoverageSummary(IEnumerable<string> lines)
    {
        Dictionary<string, object> metrics = [];

        foreach (var line in lines)
            if (line.Contains("Line coverage:", StringComparison.Ordinal))
                metrics["lineCoverage"] = ExtractPercentage(line);
            else if (line.Contains("Branch coverage:", StringComparison.Ordinal))
                metrics["branchCoverage"] = ExtractPercentage(line);
            else if (line.Contains("Method coverage:", StringComparison.Ordinal))
                metrics["methodCoverage"] = ExtractPercentage(line);
            else if (line.Contains("Coverable lines:", StringComparison.Ordinal))
                metrics["coverableLines"] = ExtractNumber(line);
            else if (line.Contains("Covered lines:", StringComparison.Ordinal))
                metrics["coveredLines"] = ExtractNumber(line);

        return new Dictionary<string, object>
        {
            ["generatedAt"] = TimeProvider.System.GetUtcNow().ToString("O"),
            ["metrics"] = metrics
        };
    }

    private static double ExtractPercentage(ReadOnlySpan<char> line)
    {
        var colonIdx = line.IndexOf(':');
        var percentIdx = line.IndexOf('%');
        if (colonIdx < 0 || percentIdx <= colonIdx) return 0;
        return double.TryParse(line[(colonIdx + 1)..percentIdx].Trim(), out var result) ? result : 0;
    }

    private static int ExtractNumber(ReadOnlySpan<char> line)
    {
        var colonIdx = line.IndexOf(':');
        if (colonIdx < 0) return 0;
        return int.TryParse(line[(colonIdx + 1)..].Trim(), out var result) ? result : 0;
    }

    private void GenerateDetailedCoverageSummaries()
    {
        var mergedCobertura = CoverageDirectory / "Cobertura.xml";
        if (!mergedCobertura.FileExists())
        {
            Log.Warning("Merged Cobertura.xml not found, skipping detailed summaries");
            return;
        }

        var projectOutputs = TestProjects.ToDictionary(
            static p => p.Name,
            p => CoverageDirectory / $"{p.Name}.coverage-issues.xml");

        CoverageSummaryConverter.ConvertPerProject(mergedCobertura, SourceDirectory, projectOutputs);
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// Coverage Patterns - Exclusion rules and utilities
// ════════════════════════════════════════════════════════════════════════════════

[ExcludeFromCodeCoverage(Justification = "Build infrastructure - tested via integration")]
public sealed record ExclusionRule(
    string Name,
    string[]? PathContains = null,
    string[]? FileSuffixes = null,
    bool ShouldExclude = true)
{
    public bool Matches(string normalizedPath) =>
        MatchesPath(normalizedPath) || MatchesSuffix(normalizedPath);

    bool MatchesPath(string path) =>
        PathContains is { Length: > 0 } &&
        Array.Exists(PathContains, p => path.Contains(p, StringComparison.OrdinalIgnoreCase));

    bool MatchesSuffix(string path) =>
        FileSuffixes is { Length: > 0 } suffixes &&
        Array.Exists(suffixes, s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase));

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
    const string StateMachineMarker = "+<";
    const string StateMachineSuffix = ">d__";

    public static bool TryExtractStateMachineMethod(string className, out string? methodName)
    {
        methodName = null;

        var plusIndex = className.IndexOf(StateMachineMarker, StringComparison.Ordinal);
        if (plusIndex < 0) return false;

        var methodStart = plusIndex + 2;
        var methodEnd = className.IndexOf(StateMachineSuffix, methodStart, StringComparison.Ordinal);
        if (methodEnd <= methodStart) return false;

        methodName = className[methodStart..methodEnd];
        return methodName.Length > 0;
    }

    public static bool IsStateMachineClass(string className) =>
        className.Contains(StateMachineMarker, StringComparison.Ordinal) &&
        className.Contains(">d__", StringComparison.Ordinal);

    public static bool IsMoveNextMethod(string methodName) =>
        methodName.Equals("MoveNext", StringComparison.Ordinal);

    public static string CreateStateMachineReason(string? originalMethodName) =>
        originalMethodName is { Length: > 0 }
            ? $"CompilerGeneratedStateMachine({originalMethodName})"
            : "CompilerGeneratedStateMachine";
}

// ════════════════════════════════════════════════════════════════════════════════
// Coverage Summary Converter
// ════════════════════════════════════════════════════════════════════════════════

[ExcludeFromCodeCoverage(Justification = "Build infrastructure - tested via integration")]
public static class CoverageSummaryConverter
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void ConvertPerProject(
        AbsolutePath coberturaPath,
        AbsolutePath sourceRoot,
        IDictionary<string, AbsolutePath> projectOutputs)
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

        var generatedAtUtc = TimeProvider.System.GetUtcNow().DateTime;
        var relativeCoberturaPath = Path.GetFileName(coberturaPath);
        var sourceRootNormalized = NormalizePath(sourceRoot, null);

        var allFileIssues = ExtractAllFileIssues(coverageElement, sourceRootNormalized);

        foreach (var (projectName, outputPath) in projectOutputs)
        {
            var projectFiles = allFileIssues
                .Where(kvp => kvp.Key.Contains(projectName, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            WriteProjectSummary(projectName, projectFiles, outputPath, generatedAtUtc, relativeCoberturaPath);
        }
    }

    static Dictionary<string, CoverageFile> ExtractAllFileIssues(XContainer coverageElement, string sourceRoot)
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

    static void ProcessClassIssues(
        XContainer classElement,
        string normalizedPath,
        IDictionary<string, CoverageFile> result,
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

    static void WriteProjectSummary(
        string projectName,
        Dictionary<string, CoverageFile> fileIssues,
        AbsolutePath outputPath,
        DateTime generatedAtUtc,
        string sourceName)
    {
        var jsonSummary = new CoverageSummary(projectName, sourceName, generatedAtUtc);

        XElement xmlSummary = new("coverage-summary",
            new XAttribute("project", projectName),
            new XAttribute("generatedAtUtc", generatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("source", sourceName));

        var filesWithIssues = 0;

        foreach (var (filePath, file) in fileIssues.OrderBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (file.LineDict.Count is 0 && file.BranchDict.Count is 0) continue;

            jsonSummary.Files.Add(new CoverageFileDto(filePath, [.. file.Lines], [.. file.Branches]));

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
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonSummary, JsonOptions));

        Log.Information("Coverage summary: {XmlPath} + {JsonPath} ({FileCount} files with issues)",
            outputPath, jsonPath, filesWithIssues);
    }

    static string NormalizePath(string path, string? sourceRoot)
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

    static string DetermineLineReason(string? ruleTag, string? stateMachineMethod) =>
        stateMachineMethod is { Length: > 0 } ? StateMachinePatterns.CreateStateMachineReason(stateMachineMethod) :
        ruleTag is { Length: > 0 } ? ruleTag :
        "LineNotExecuted";

    static CoverageBranch? CreateBranchIssue(
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

    static BranchCoverageInfo? ParseConditionCoverage(ReadOnlySpan<char> input)
    {
        var parenStart = input.IndexOf('(');
        var parenEnd = input.IndexOf(')');
        if (parenStart < 0 || parenEnd <= parenStart) return null;

        var percentPart = input[..parenStart].Trim();
        if (percentPart is not [.., '%']) return null;

        if (!double.TryParse(percentPart[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            return null;

        var fraction = input[(parenStart + 1)..parenEnd];
        var slashIdx = fraction.IndexOf('/');
        if (slashIdx < 0) return null;

        if (!int.TryParse(fraction[..slashIdx], NumberStyles.Integer, CultureInfo.InvariantCulture, out var covered))
            return null;

        if (!int.TryParse(fraction[(slashIdx + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var total))
            return null;

        return new BranchCoverageInfo(covered, total, percent);
    }

    static CoverageFile GetOrCreateFileIssues(IDictionary<string, CoverageFile> dict, string path) =>
        dict.TryGetValue(path, out var issues) ? issues : dict[path] = new CoverageFile();

    static void EnsureDirectoryExists(string path)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir)
            Directory.CreateDirectory(dir);
    }

    sealed record CoverageLine(int Line, int Hits, string Reason = "");

    sealed record CoverageBranch(
        int Line,
        int Hits,
        int CoveredBranches,
        int TotalBranches,
        double CoveragePercent,
        string Reason = "");

    sealed record BranchCoverageInfo(int CoveredBranches, int TotalBranches, double Percent);

    // ─── DTOs (properties accessed by JsonSerializer via reflection) ─────────
    sealed class CoverageFile
    {
        [JsonIgnore] public readonly Dictionary<int, CoverageBranch> BranchDict = [];
        [JsonIgnore] public readonly Dictionary<int, CoverageLine> LineDict = [];

        public IEnumerable<CoverageLine> Lines => LineDict.Values.OrderBy(static l => l.Line);
        public IEnumerable<CoverageBranch> Branches => BranchDict.Values.OrderBy(static b => b.Line);
    }

    sealed class CoverageSummary(string project, string source, DateTime generatedAtUtc)
    {
        public string Project { get; } = project;
        public string Source { get; } = source;
        public DateTime GeneratedAtUtc { get; } = generatedAtUtc;
        public List<CoverageFileDto> Files { get; } = [];
        public int FileCount => Files.Count; // Used for serialization
    }

    sealed class CoverageFileDto(string path, List<CoverageLine> lines, List<CoverageBranch> branches)
    {
        public string Path { get; } = path;
        public List<CoverageLine> Lines { get; } = lines;
        public List<CoverageBranch> Branches { get; } = branches;
    }
}
