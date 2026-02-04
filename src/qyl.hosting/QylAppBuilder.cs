using System.Collections.Concurrent;
using Qyl.Hosting.Resources;

namespace Qyl.Hosting;

/// <summary>
/// Builder for qyl-orchestrated distributed applications.
/// </summary>
public sealed class QylAppBuilder
{
    private readonly ConcurrentDictionary<string, IQylResource> _resources = new();

    internal QylAppBuilder(string[] _)
    {
        Options = new QylOptions();
    }

    /// <summary>
    /// Application options.
    /// </summary>
    public QylOptions Options { get; }

    /// <summary>
    /// All registered resources.
    /// </summary>
    public IReadOnlyDictionary<string, IQylResource> Resources => _resources;

    /// <summary>
    /// Adds a .NET project to the application.
    /// </summary>
    /// <typeparam name="TProject">Project reference marker type.</typeparam>
    /// <param name="name">Resource name.</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public ProjectResource<TProject> AddProject<TProject>(string name)
        where TProject : IProjectMetadata, new()
    {
        var metadata = new TProject();
        var resource = new ProjectResource<TProject>(name, metadata.ProjectPath, this);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Adds a Vite/React/Vue frontend application.
    /// </summary>
    /// <param name="name">Resource name.</param>
    /// <param name="workingDirectory">Path to package.json directory.</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public ViteResource AddVite(string name, string workingDirectory)
    {
        var resource = new ViteResource(name, workingDirectory, this);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Adds a Node.js application.
    /// </summary>
    /// <param name="name">Resource name.</param>
    /// <param name="workingDirectory">Path to package.json directory.</param>
    /// <param name="scriptPath">Entry script (e.g., "src/server.js").</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public NodeResource AddNode(string name, string workingDirectory, string scriptPath)
    {
        var resource = new NodeResource(name, workingDirectory, scriptPath, this);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Adds a Python application.
    /// </summary>
    /// <param name="name">Resource name.</param>
    /// <param name="workingDirectory">Path to Python project.</param>
    /// <param name="scriptPath">Entry script (e.g., "main.py").</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public PythonResource AddPython(string name, string workingDirectory, string scriptPath)
    {
        var resource = new PythonResource(name, workingDirectory, scriptPath, this);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Adds a FastAPI/Uvicorn application.
    /// </summary>
    /// <param name="name">Resource name.</param>
    /// <param name="workingDirectory">Path to Python project.</param>
    /// <param name="appModule">ASGI module (e.g., "main:app").</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public UvicornResource AddUvicorn(string name, string workingDirectory, string appModule)
    {
        var resource = new UvicornResource(name, workingDirectory, appModule, this);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Adds a container resource.
    /// </summary>
    /// <param name="name">Resource name.</param>
    /// <param name="image">Container image.</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public ContainerResource AddContainer(string name, string image)
    {
        var resource = new ContainerResource(name, image, this);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Adds Redis for caching.
    /// </summary>
    /// <param name="name">Resource name. Default: "cache"</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public ContainerResource AddRedis(string name = "cache")
    {
        var resource = new ContainerResource(name, "redis:alpine", this)
            .WithPort(6379);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Adds PostgreSQL database.
    /// </summary>
    /// <param name="name">Resource name. Default: "postgres"</param>
    /// <returns>Resource builder for fluent configuration.</returns>
    public PostgresResource AddPostgres(string name = "postgres")
    {
        var resource = new PostgresResource(name, this);
        _resources[name] = resource;
        return resource;
    }

    /// <summary>
    /// Builds and runs the distributed application.
    /// </summary>
    public void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Builds and runs the distributed application asynchronously.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        using var runner = new QylRunner(this);
        await runner.RunAsync(ct);
    }
}

/// <summary>
/// Marker interface for project metadata.
/// Generated by the SDK for each project reference.
/// </summary>
public interface IProjectMetadata
{
    /// <summary>
    /// Absolute path to the project file.
    /// </summary>
    string ProjectPath { get; }
}
