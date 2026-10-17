using System;
using Fallout.Common;
using Fallout.Common.Tooling;

namespace Qyl.Build;

interface INativeAot : IHasSourcePaths
{
    Target NativeAot => d => d
        .Description("Publish and execute the collector NativeAOT smoke (needs a container daemon)")
        .OnlyWhenDynamic(() => ContainerDaemon.Required(IsServerBuild), ContainerDaemon.Condition)
        .Executes(() =>
        {
            var collectorSmoke = RootDirectory / "eng" / "scripts" / "collector-aot-smoke.sh";
            ProcessTasks.StartProcess(
                    "bash",
                    $"\"{collectorSmoke}\"",
                    RootDirectory,
                    logOutput: true)
                .AssertZeroExitCode();
        });
}

/// <summary>
/// CheckFull's two container stages, the collector NativeAOT smoke and the Qyl.Api.Sdk container
/// scenario, need a container daemon. CI has one (`check.yml`, main pushes and full=true reruns, on
/// ubuntu-latest) and always runs them, so a missing daemon there fails the stage; a local machine without
/// one skips them, and the target table names the reason, instead of failing the gate.
/// </summary>
static class ContainerDaemon
{
    public const string Condition = "a container daemon runs (CI always runs this stage)";

    public static bool Required(bool isServerBuild) => isServerBuild || Running();

    static bool Running()
    {
        try
        {
            var process = ProcessTasks.StartProcess("docker", "version", logOutput: false, logInvocation: false);
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }
}
