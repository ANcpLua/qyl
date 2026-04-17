// =============================================================================
// qyl Build System - qyl.mcp NuGet packaging
// =============================================================================
// IPackMcp: pack src/qyl.mcp as a dotnet-tool .nupkg into Artifacts/packages/
// and (in CI only) push to nuget.org. First-class release path is
// .github/workflows/release-mcp.yml triggered by 'mcp-v*' tag push.
// =============================================================================

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Components;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// ════════════════════════════════════════════════════════════════════════════════
// IPackMcp - Pack & push Qyl.Mcp dotnet tool
// ════════════════════════════════════════════════════════════════════════════════

interface IPackMcp : IHazSourcePaths, IHazConfiguration
{
    AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";
    AbsolutePath McpProject => SourceDirectory / "qyl.mcp" / "qyl.mcp.csproj";

    Target PackMcp => d => d
        .Description("Pack qyl.mcp as a framework-dependent dotnet tool (.nupkg) into Artifacts/packages")
        .DependsOn<ICompile>(static x => x.Compile)
        .Produces(PackagesDirectory / "Qyl.Mcp.*.nupkg")
        .Executes(() =>
        {
            PackagesDirectory.CreateDirectory();
            // IsPacking=true suppresses <RuntimeIdentifiers> and PublishSelfContained
            // in qyl.mcp.csproj so pack produces a single portable tool package
            // instead of 6 self-contained per-RID packages (~54 MB each).
            DotNetPack(s => s
                .SetProject(McpProject)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(PackagesDirectory)
                .SetProperty("IsPacking", "true")
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target PushMcp => d => d
        .Description("Push Qyl.Mcp *.nupkg to nuget.org (requires NUGET_API_KEY)")
        .DependsOn(PackMcp)
        .Requires(() => NuGetApiKey)
        .Executes(() => DotNetNuGetPush(s => s
            .SetTargetPath(PackagesDirectory / "Qyl.Mcp.*.nupkg")
            .SetSource("https://api.nuget.org/v3/index.json")
            .SetApiKey(NuGetApiKey)
            .EnableSkipDuplicate()));

    [Parameter("NuGet API key for push", Name = "NUGET_API_KEY")]
    string NuGetApiKey => TryGetValue(() => NuGetApiKey);
}
