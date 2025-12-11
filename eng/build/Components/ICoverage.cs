using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.ReportGenerator;
using Serilog;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;

namespace Components;

internal interface ICoverage : ITest
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    Target Coverage => d => d
        .Description("Run tests with code coverage")
        .DependsOn<ICompile>(x => x.Compile)
        .TryDependsOn<ITestContainers>()
        .Produces(CoverageDirectory / "**")
        .Executes(() =>
        {
            CoverageDirectory.CreateOrCleanDirectory();
            TestResultsDirectory.CreateOrCleanDirectory();

            foreach (Project project in TestProjects)
            {
                AbsolutePath? coverageFile = CoverageDirectory / $"{project.Name}.cobertura.xml";
                RunTestWithCoverage(project, coverageFile);
            }

            GenerateCoverageReports();
            GenerateAiCoverageSummary();
            GenerateDetailedCoverageSummaries();
        });

    private void RunTestWithCoverage(Project project, AbsolutePath coverageOutput)
    {
        Log.Information("Running tests with coverage: {Project}", project.Name);

        MtpArgumentsBuilder mtp = MtpExtensions.Mtp()
            .ResultsDirectory(TestResultsDirectory)
            .ReportTrx($"{project.Name}.trx")
            .IgnoreExitCode(8)
            .CoverageCobertura(coverageOutput);

        if (StopOnFail == true)
            mtp.StopOnFail();

        if (LiveOutput == true || IsLocalBuild)
            mtp.ShowLiveOutput();

        ExecuteMtpTestInternal(project, mtp);
    }

    private void GenerateCoverageReports()
    {
        IReadOnlyCollection<AbsolutePath>? coverageFiles = CoverageDirectory.GlobFiles("*.cobertura.xml");

        if (coverageFiles.Count is 0)
        {
            Log.Warning("No coverage files found in {Directory}", CoverageDirectory);
            return;
        }

        Log.Information("Generating coverage reports from {Count} file(s)", coverageFiles.Count);

        ReportGeneratorSettings? settings = new ReportGeneratorSettings()
            .SetReports(coverageFiles.Select(f => f.ToString()))
            .SetTargetDirectory(CoverageDirectory)
            .SetReportTypes(
                ReportTypes.Html,
                ReportTypes.Cobertura,
                ReportTypes.TextSummary,
                ReportTypes.Badges)
            .SetAssemblyFilters("-Microsoft.*", "-System.*", "-xunit.*", "-*.tests")
            .SetClassFilters("-*.Migrations.*", "-*.Generated.*", "-*+<*>d__*");

        if (IsServerBuild && GitVersion is not null) settings = settings.SetTag(GitVersion.FullSemVer);

        ReportGenerator(settings);

        Log.Information("Coverage reports generated: {Directory}", CoverageDirectory);
    }

    private void GenerateAiCoverageSummary()
    {
        AbsolutePath? summaryFile = CoverageDirectory / "Summary.txt";
        AbsolutePath? jsonOutput = CoverageDirectory / "coverage.summary.json";

        if (!summaryFile.FileExists())
        {
            Log.Warning("Summary.txt not found, skipping AI summary generation");
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(summaryFile);
            Dictionary<string, object> summary = ParseCoverageSummary(lines);
            File.WriteAllText(jsonOutput, JsonSerializer.Serialize(summary, _jsonOptions));
            Log.Information("AI coverage summary: {Path}", jsonOutput);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to generate AI coverage summary");
        }
    }

    private static Dictionary<string, object> ParseCoverageSummary(string[] lines)
    {
        Dictionary<string, object> metrics = new();

        foreach (string line in lines)
        {
            if (line.Contains("Line coverage:", StringComparison.Ordinal))
            {
                metrics["lineCoverage"] = ExtractPercentage(line);
            }
            else if (line.Contains("Branch coverage:", StringComparison.Ordinal))
            {
                metrics["branchCoverage"] = ExtractPercentage(line);
            }
            else if (line.Contains("Method coverage:", StringComparison.Ordinal))
            {
                metrics["methodCoverage"] = ExtractPercentage(line);
            }
            else if (line.Contains("Coverable lines:", StringComparison.Ordinal))
            {
                metrics["coverableLines"] = ExtractNumber(line);
            }
            else if (line.Contains("Covered lines:", StringComparison.Ordinal))
            {
                metrics["coveredLines"] = ExtractNumber(line);
            }
        }

        return new Dictionary<string, object>
        {
            ["generatedAt"] = DateTime.UtcNow.ToString("O"),
            ["metrics"] = metrics
        };
    }

    private static double ExtractPercentage(ReadOnlySpan<char> line)
    {
        int colonIdx = line.IndexOf(':');
        int percentIdx = line.IndexOf('%');
        if (colonIdx < 0 || percentIdx <= colonIdx) return 0;
        return double.TryParse(line[(colonIdx + 1)..percentIdx].Trim(), out double result) ? result : 0;
    }

    private static int ExtractNumber(ReadOnlySpan<char> line)
    {
        int colonIdx = line.IndexOf(':');
        if (colonIdx < 0) return 0;
        return int.TryParse(line[(colonIdx + 1)..].Trim(), out int result) ? result : 0;
    }

    private void GenerateDetailedCoverageSummaries()
    {
        AbsolutePath? mergedCobertura = CoverageDirectory / "Cobertura.xml";
        if (!mergedCobertura.FileExists())
        {
            Log.Warning("Merged Cobertura.xml not found, skipping detailed summaries");
            return;
        }

        var projectOutputs = TestProjects.ToDictionary(
            p => p.Name,
            p => CoverageDirectory / $"{p.Name}.coverage-issues.xml");

        CoverageSummaryConverter.ConvertPerProject(mergedCobertura, SourceDirectory, projectOutputs);
    }
}
