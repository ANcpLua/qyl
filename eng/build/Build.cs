using Nuke.Common;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    Target Compile => _ => _
        .Executes(() =>
        {
            Serilog.Log.Information("Build executed");
        });
}
