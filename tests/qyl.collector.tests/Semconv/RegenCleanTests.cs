using System.Diagnostics;
using System.Text;

namespace Qyl.Collector.Tests.Semconv;

/// <summary>
/// T2 — local mirror of the <c>regen-clean</c> CI gate. A developer can run this
/// before pushing and get the same answer CI will give: <c>nuke OtelConventions</c>
/// on the working tree must leave <c>git status --porcelain</c> empty.
/// </summary>
public sealed class RegenCleanTests
{
    [Fact]
    [Trait("Category", "regen")]
    public async Task Generate_OnCleanTree_ProducesNoChanges()
    {
        var repoRoot = LocateRepoRoot();

        var buildScript = Path.Combine(repoRoot, "eng", "build.sh");
        var nukeResult = await RunAsync(repoRoot, buildScript, "OtelConventions");
        Assert.True(
            nukeResult.ExitCode == 0,
            $"nuke OtelConventions exited {nukeResult.ExitCode}.\nstdout:\n{nukeResult.StdOut}\nstderr:\n{nukeResult.StdErr}");

        var porcelain = await RunAsync(repoRoot, "git", "status", "--porcelain");
        Assert.Equal(0, porcelain.ExitCode);

        if (!string.IsNullOrWhiteSpace(porcelain.StdOut))
        {
            var diffStat = await RunAsync(repoRoot, "git", "diff", "--stat");
            Assert.Fail(
                "Regeneration produced uncommitted changes. Run 'nuke OtelConventions' locally and commit the result.\n\n" +
                $"Files that drifted:\n{porcelain.StdOut}\n" +
                $"Diff summary:\n{diffStat.StdOut}");
        }
    }

    static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "qyl.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    static async Task<ProcessResult> RunAsync(string workingDir, string fileName, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
