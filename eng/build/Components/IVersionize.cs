using Nuke.Common;
using Nuke.Common.Tooling;

namespace Components;

interface IVersionize : IHasSolution
{
    [PathVariable] Tool Versionize => TryGetValue(() => Versionize)!;

    Target Changelog => d => d
        .Description("Generate CHANGELOG from conventional commits")
        .Executes(() => Versionize("--dry-run", RootDirectory));

    Target Release => d => d
        .Description("Bump version, update CHANGELOG, create tag")
        .DependsOn<ICompile>(x => x.Compile)
        .Executes(() => Versionize(null, RootDirectory));
}