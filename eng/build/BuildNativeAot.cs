using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

namespace Qyl.Build;

interface INativeAot : IHazSourcePaths
{
    AbsolutePath McpAotSmokeProject =>
        RootDirectory / "tests" / "Qyl.Host.Mcp.AotSmoke" / "Qyl.Host.Mcp.AotSmoke.csproj";

    AbsolutePath McpAotSmokePublishDirectory =>
        RootDirectory / "artifacts" / "publish" / "Qyl.Host.Mcp.AotSmoke" / "release";

    Target NativeAot => d => d
        .Description("Publish and execute the collector and MCP NativeAOT runtime smokes")
        .Executes(() =>
        {
            var collectorSmoke = RootDirectory / "eng" / "scripts" / "collector-aot-smoke.sh";
            ProcessTasks.StartProcess(
                    "bash",
                    $"\"{collectorSmoke}\"",
                    RootDirectory,
                    logOutput: true)
                .AssertZeroExitCode();

            McpAotSmokePublishDirectory.CreateOrCleanDirectory();
            DotNetTasks.DotNetPublish(s => s
                .SetProject(McpAotSmokeProject)
                .SetConfiguration("Release")
                .SetOutput(McpAotSmokePublishDirectory)
                .SetProperty("PublishAot", true));

            var executable = McpAotSmokePublishDirectory /
                             (EnvironmentInfo.IsWin ? "Qyl.Host.Mcp.AotSmoke.exe" : "Qyl.Host.Mcp.AotSmoke");
            ProcessTasks.StartProcess(executable, workingDirectory: McpAotSmokePublishDirectory, logOutput: true)
                .AssertZeroExitCode();
        });
}
