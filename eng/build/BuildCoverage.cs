using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotCov;
using DotCov.Fallout;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Components;

namespace Qyl.Build;

/// <summary>
/// The coverage gate: every .NET test project runs under Microsoft code coverage
/// (Microsoft.Testing.Extensions.CodeCoverage) and writes a Cobertura report into a clean
/// <c>artifacts/coverage</c>; dotcov's <c>ReportCoverage</c> merges them, drops test and generated
/// code and gates line and branch coverage. <c>CheckFull</c> runs it. A failed test run exits 6
/// like <c>dotcov test</c>: coverage of failing tests is not evaluated. ReportCoverage exits with
/// dotcov's gate codes (1 FAIL, 2 invalid parameter, 3 NODATA, 4 DISABLED, 5 unreadable report);
/// eng/scripts/quiet.sh names them.
/// </summary>
interface ICoverage : IQylTests, ICoverageReport
{
    AbsolutePath CoverageDirectory => RootDirectory / "artifacts" / "coverage";

    // A few points under what the four test projects measured when the gate was added (test and
    // generated code excluded, identical over two runs): line 78.4 %, branch 59.9 %.
    // --coverage-min-line and --coverage-min-branch override a threshold for one run.
    string? ICoverageReport.MinLine => TryGetValue(() => ((ICoverageReport)this).MinLine) ?? "75";
    string? ICoverageReport.MinBranch => TryGetValue(() => ((ICoverageReport)this).MinBranch) ?? "57";
    string ICoverageReport.ExcludeTestCodeParam => "true";
    // The source generators' output under artifacts/obj (JSON contexts, regex, storage, logging) is
    // most of the instrumented lines; it measures the generators, not qyl.
    string ICoverageReport.ExcludeGeneratedParam => "true";
    AbsolutePath ICoverageReport.CoverageSearchDirectory => CoverageDirectory;

    Target Coverage => d => d
        .Description("Run every .NET test project with coverage and gate line and branch coverage")
        .DependsOn<ICompile>(static x => x.Compile)
        .Triggers<ICoverageReport>(static x => x.ReportCoverage)
        .Executes(() =>
        {
            CoverageDirectory.CreateOrCleanDirectory();
            // Only sources under this repository: the files NuGet packages compile into qyl's
            // assemblies (ANcpLua.Roslyn.Utilities.Sources in the storage generator, the hosting
            // package's buildTransitive file) are not qyl's code.
            var settings = CoverageDirectory / "settings.xml";
            new XElement("Configuration",
                new XElement("CodeCoverage",
                    new XElement("Sources",
                        new XElement("Include",
                            new XElement("Source", $"^{Regex.Escape(RootDirectory.ToString())}[\\\\/].*")))))
                .Save(settings);
            try
            {
                DotNetTasks.DotNet(
                    $"test --solution \"{Solution}\" --configuration {Configuration} --no-build --no-restore --max-parallel-test-modules 1 " +
                    $"--coverage --coverage-output-format cobertura --coverage-settings \"{settings}\" --results-directory \"{CoverageDirectory}\"",
                    workingDirectory: RootDirectory);
            }
            catch (ProcessException exception)
            {
                ExitCode = ExitCodes.TestRunFailed;
                throw new InvalidOperationException(
                    $"dotnet test exited with code {exception.ExitCode}; coverage was not evaluated.", exception);
            }
        });
}
