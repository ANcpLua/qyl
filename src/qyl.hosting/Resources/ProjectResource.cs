namespace Qyl.Hosting.Resources;

/// <summary>
/// A .NET project resource.
/// </summary>
public sealed class ProjectResource<TProject> : QylResourceBase<ProjectResource<TProject>>
    where TProject : IProjectMetadata
{
    internal ProjectResource(string name, string projectPath, QylAppBuilder builder)
        : base(name, builder)
    {
        ProjectPath = projectPath;

        // Auto-assign port
        var port = PortAllocator.Next();
        AddPort(new PortBinding(port, port, "http"));
        SetEnvironment("ASPNETCORE_URLS", $"http://*:{port}");

        // Enable GenAI by default if configured
        if (builder.Options.GenAI)
        {
            WithGenAI();
        }
    }

    /// <summary>
    /// Path to the .csproj file.
    /// </summary>
    public string ProjectPath { get; }

    public override string Type => "project";

    /// <summary>
    /// Sets the launch profile to use.
    /// </summary>
    public ProjectResource<TProject> WithLaunchProfile(string profile)
    {
        SetEnvironment("DOTNET_LAUNCH_PROFILE", profile);
        return this;
    }

    /// <summary>
    /// Adds command-line arguments.
    /// </summary>
    public ProjectResource<TProject> WithArgs(params string[] args)
    {
        SetEnvironment("QYL_ARGS", string.Join(" ", args));
        return this;
    }
}

/// <summary>
/// Allocates unique ports for resources.
/// </summary>
internal static class PortAllocator
{
    private static int _nextPort = 5000;
    private static readonly Lock Lock = new();

    public static int Next()
    {
        lock (Lock)
        {
            return _nextPort++;
        }
    }
}
