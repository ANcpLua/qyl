// =============================================================================
// qyl Build System - PRD #173 Smoke Gate
// =============================================================================
// nuke Smoke — pre-commit/pre-push validation that the cost processor,
// activity tracker, conversations endpoint, and agent inventory all work
// end-to-end against a live qyl stack and a local Ollama. Native to any
// agent CLI (Claude Code, Codex, aider, Gemini CLI).
// =============================================================================

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
