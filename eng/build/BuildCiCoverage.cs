using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// The workflow's job graph and the <c>Ci</c> target are two hand-maintained answers to
/// "what is the gate". Nothing made them agree, and they drifted twice: <c>PackSmoke</c> and
/// the G1 vocabulary smoke ran only in CI, so the documented local gate was a strict subset of
/// the real one and a trim/AOT regression reached <c>main</c> past a green local run.
/// This gate removes the second answer's freedom to differ — every target and script CI invokes
/// must be reachable from <c>Ci</c>, so adding a CI step without wiring it in fails the build.
/// The reverse direction is deliberately unconstrained: <c>Ci</c> may run more than CI does.
/// </summary>
interface ICiCoverage : IHasSourcePaths
{

    AbsolutePath BuildSourceDirectory => RootDirectory / "eng" / "build";

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
