// =============================================================================
// qyl Build System - qyl.mcp NuGet packaging
// =============================================================================
// IPackMcp: pack services/qyl.mcp as a dotnet-tool .nupkg into Artifacts/packages/
// and (in CI only) push to nuget.org. First-class release path is
// .github/workflows/release-mcp.yml triggered by 'mcp-v*' tag push.
// =============================================================================

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Components;

namespace Qyl.Build;

// ════════════════════════════════════════════════════════════════════════════════
// IPackMcp - Pack & push Qyl.Mcp dotnet tool
// ════════════════════════════════════════════════════════════════════════════════

interface IPackMcp : IHazSourcePaths, IHazConfiguration
{
    AbsolutePath NupkgOutputDirectory => ArtifactsDirectory / "packages";
    AbsolutePath McpProject => ServicesDirectory / "qyl.mcp" / "qyl.mcp.csproj";

    Target PackMcp => d => d
        .Unlisted()
        .Description("Pack qyl.mcp as a framework-dependent dotnet tool (.nupkg) into Artifacts/packages")
        .DependsOn<ICompile>(static x => x.Compile)
        .Produces(NupkgOutputDirectory / "Qyl.Mcp.*.nupkg")
        .Executes(() =>
        {
            NupkgOutputDirectory.CreateDirectory();
            // IsPacking=true suppresses <RuntimeIdentifiers> and PublishSelfContained
            // in qyl.mcp.csproj so pack produces a single portable tool package
            // instead of 6 self-contained per-RID packages (~54 MB each).
            DotNetTasks.DotNetPack(s => s
                .SetProject<DotNetPackSettings>(McpProject)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(NupkgOutputDirectory)
                .SetProperty("IsPacking", "true")
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target PushMcp => d => d
        .Unlisted()
        .Description("Push Qyl.Mcp *.nupkg to nuget.org (requires NUGET_API_KEY)")
        .DependsOn(PackMcp)
        .Requires(() => NuGetApiKey)
        .Executes(() => DotNetTasks.DotNetNuGetPush(s => s
            .SetTargetPath<DotNetNuGetPushSettings>(NupkgOutputDirectory / "Qyl.Mcp.*.nupkg")
            .SetSource("https://api.nuget.org/v3/index.json")
            .SetApiKey(NuGetApiKey)
            .EnableSkipDuplicate()));

    [Parameter("NuGet API key for push", Name = "NUGET_API_KEY")]
    string? NuGetApiKey => TryGetValue(() => NuGetApiKey);
}
