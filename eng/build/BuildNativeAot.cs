using Fallout.Common;
using Fallout.Common.Tooling;

namespace Qyl.Build;

interface INativeAot : IHasSourcePaths
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
