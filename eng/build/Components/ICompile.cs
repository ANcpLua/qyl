using System.ComponentModel;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace Components;

[TypeConverter(typeof(TypeConverter<Configuration>))]
sealed class Configuration : Enumeration
{
    public static readonly Configuration Debug = new() { Value = nameof(Debug) };
    public static readonly Configuration Release = new() { Value = nameof(Release) };
    public static implicit operator string(Configuration c) => c.Value;
}

[ParameterPrefix(nameof(ICompile))]
interface ICompile : IHasSolution
{
    [GitVersion(Framework = "net10.0", NoCache = true, NoFetch = true)]
    GitVersion? GitVersion => TryGetValue(() => GitVersion);

    [Parameter("Build configuration (Debug/Release)")]
    Configuration Configuration => TryGetValue(() => Configuration)
                                   ?? (IsLocalBuild ? Configuration.Debug : Configuration.Release);

    Target Restore => d => d
        .Description("Restore NuGet packages")
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(GetSolutionPath()));
            Log.Information("Restored: {Solution}", Solution.FileName);
        });

    Target Compile => d => d
        .Description("Build the solution")
        .DependsOn(Restore)
        .Executes(() =>
        {
            var settings = new DotNetBuildSettings()
                .SetProjectFile(GetSolutionPath())
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetDeterministic(IsServerBuild)
                .SetContinuousIntegrationBuild(IsServerBuild);

            if (GitVersion is not null)
                settings = settings
                    .SetAssemblyVersion(GitVersion.AssemblySemVer)
                    .SetFileVersion(GitVersion.AssemblySemFileVer)
                    .SetInformationalVersion(GitVersion.InformationalVersion);

            DotNetBuild(settings);

            Log.Information("Compiled: {Solution} [{Configuration}]",
                Solution.FileName,
                Configuration);
        });

    Target Clean => d => d
        .Description("Clean build outputs")
        .Before(Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("**/bin", "**/obj").DeleteDirectories();
            ArtifactsDirectory.CreateOrCleanDirectory();

            Log.Information("Cleaned: {ArtifactsDirectory}", ArtifactsDirectory);
        });
}