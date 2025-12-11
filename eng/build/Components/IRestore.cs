using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace Components;

internal interface IRestore : IHasSolution
{
    Target Restore => d => d
        .Description("Restore NuGet packages")
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(GetSolutionPath()));

            Log.Information("Restored: {Solution}", Solution.FileName);
        });
}
