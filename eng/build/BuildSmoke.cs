
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

namespace Qyl.Build;

interface ISmoke : IHazSourcePaths
{
    AbsolutePath SmokeScript => RootDirectory / "eng" / "smoke" / "run.sh";

    Target Smoke => d => d
        .Description("PRD #173 quality gate — Ollama + qyl stack + cost/activity assertions")
        .OnlyWhenStatic(() => SmokeScript.FileExists())
        .Executes(() =>
            ProcessTasks.StartProcess("bash", SmokeScript, RootDirectory, logOutput: true)
                .AssertZeroExitCode());
}
