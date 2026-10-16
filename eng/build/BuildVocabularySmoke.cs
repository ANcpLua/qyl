using Fallout.Common;
using Fallout.Common.Tooling;

namespace Qyl.Build;

/// <summary>
/// The G1 vocabulary smoke, <c>eng/scripts/g1-vocabulary-smoke.sh</c>: the cheap cross-check that no
/// hand-written <c>"qyl.</c> vocabulary literal lives in runtime source (QYL0200 is the authoritative
/// gate). <c>CheckFull</c> runs it; a script only a gate definition names is a gate nobody runs.
/// </summary>
interface IVocabularySmoke : IHasSourcePaths
{


    Target G1VocabularySmoke => d => d
        .Unlisted()
        .Description("Run the G1 telemetry-vocabulary smoke")
        .Executes(() =>
        {
            var script = RootDirectory / "eng" / "scripts" / "g1-vocabulary-smoke.sh";
            ProcessTasks.StartProcess("bash", $"\"{script}\"", RootDirectory, logOutput: true)
                .AssertZeroExitCode();
        });
}
