namespace Qyl.Hosting.Resources;

/// <summary>
/// A Python application.
/// </summary>
public sealed class PythonResource : QylResourceBase<PythonResource>
{
    internal PythonResource(string name, string workingDirectory, string scriptPath, QylAppBuilder builder)
        : base(name, builder)
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory);
        ScriptPath = scriptPath;

        // Auto-assign port
        var port = PortAllocator.Next();
        AddPort(new PortBinding(port, port, "http"));
        SetEnvironment("PORT", port.ToString());

        // Enable GenAI by default if configured
        if (builder.Options.GenAi)
        {
            WithGenAi();
        }
    }

    /// <summary>
    /// Path to the Python project directory.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Entry script path relative to working directory.
    /// </summary>
    public string ScriptPath { get; }

    /// <summary>
    /// Use uv for package management.
    /// </summary>
    public bool UseUv { get; private set; }

    public override string Type => "python";

    /// <summary>
    /// Use uv for fast package management.
    /// </summary>
    public PythonResource WithUv()
    {
        UseUv = true;
        return this;
    }

    /// <summary>
    /// Sets command-line arguments.
    /// </summary>
    public PythonResource WithArgs(params string[] args)
    {
        SetEnvironment("QYL_ARGS", string.Join(" ", args));
        return this;
    }
}

/// <summary>
/// A FastAPI/Uvicorn ASGI application.
/// </summary>
public sealed class UvicornResource : QylResourceBase<UvicornResource>
{
    internal UvicornResource(string name, string workingDirectory, string appModule, QylAppBuilder builder)
        : base(name, builder)
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory);
        AppModule = appModule;

        // Auto-assign port
        var port = PortAllocator.Next();
        AddPort(new PortBinding(port, port, "http"));
        SetEnvironment("PORT", port.ToString());
        SetEnvironment("HOST", "0.0.0.0");

        // Enable GenAI by default if configured
        if (builder.Options.GenAi)
        {
            WithGenAi();
        }
    }

    /// <summary>
    /// Path to the Python project directory.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// ASGI application module (e.g., "main:app").
    /// </summary>
    public string AppModule { get; }

    /// <summary>
    /// Use uv for package management.
    /// </summary>
    public bool UseUv { get; private set; }

    public override string Type => "uvicorn";

    /// <summary>
    /// Use uv for fast package management.
    /// </summary>
    public UvicornResource WithUv()
    {
        UseUv = true;
        return this;
    }
}
