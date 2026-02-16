namespace Qyl.Hosting.Resources;

/// <summary>
///     A container resource (Docker/Podman).
/// </summary>
public sealed class ContainerResource : QylResourceBase<ContainerResource>
{
    private readonly List<string> _args = [];
    private readonly List<string> _volumes = [];

    internal ContainerResource(string name, string image, QylAppBuilder builder)
        : base(name, builder) =>
        Image = image;

    /// <summary>
    ///     Container image.
    /// </summary>
    public string Image { get; }

    /// <summary>
    ///     Volume mounts.
    /// </summary>
    public IEnumerable<string> Volumes => _volumes;

    /// <summary>
    ///     Additional container arguments.
    /// </summary>
    public IEnumerable<string> Args => _args;

    public override string Type => "container";

    /// <summary>
    ///     Adds a volume mount.
    /// </summary>
    public ContainerResource WithVolume(string name, string containerPath)
    {
        _volumes.Add($"{name}:{containerPath}");
        return this;
    }

    /// <summary>
    ///     Adds a bind mount.
    /// </summary>
    public ContainerResource WithBindMount(string hostPath, string containerPath)
    {
        _volumes.Add($"{Path.GetFullPath(hostPath)}:{containerPath}");
        return this;
    }

    /// <summary>
    ///     Adds container arguments.
    /// </summary>
    public ContainerResource WithArgs(params string[] args)
    {
        _args.AddRange(args);
        return this;
    }
}

/// <summary>
///     A PostgreSQL database resource.
/// </summary>
public sealed class PostgresResource : QylResourceBase<PostgresResource>
{
    internal PostgresResource(string name, QylAppBuilder builder)
        : base(name, builder)
    {
        AddPort(new PortBinding(5432, 5432, "postgres"));

        // Default credentials for development
        SetEnvironment("POSTGRES_USER", "postgres");
        SetEnvironment("POSTGRES_PASSWORD", "postgres");
        SetEnvironment("POSTGRES_DB", name);
    }

    /// <summary>
    ///     Database name.
    /// </summary>
    public string DatabaseName => Name;

    public override string Type => "postgres";

    /// <summary>
    ///     Adds a database to this PostgreSQL instance.
    /// </summary>
    public PostgresResource AddDatabase(string name)
    {
        SetEnvironment("POSTGRES_DB", name);
        return this;
    }

    /// <summary>
    ///     Sets the PostgreSQL password.
    /// </summary>
    public PostgresResource WithPassword(string password)
    {
        SetEnvironment("POSTGRES_PASSWORD", password);
        return this;
    }

    /// <summary>
    ///     Gets the connection string for this database.
    /// </summary>
    public string GetConnectionString()
    {
        var port = Ports.Count > 0 ? Ports[0].HostPort : 5432;
        var db = Environment.GetValueOrDefault("POSTGRES_DB", Name);
        var user = Environment.GetValueOrDefault("POSTGRES_USER", "postgres");
        var password = Environment.GetValueOrDefault("POSTGRES_PASSWORD", "postgres");
        return $"Host={Name};Port={port};Database={db};Username={user};Password={password}";
    }
}
