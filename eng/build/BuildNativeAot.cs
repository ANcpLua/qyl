using Nuke.Common;
using Nuke.Common.Tooling;

namespace Qyl.Build;

interface INativeAot : IHazSourcePaths
{
    Target NativeAot => d => d
        .Description("Publish and execute the collector NativeAOT smoke")
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
