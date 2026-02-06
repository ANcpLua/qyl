namespace Qyl.Hosting.Resources;

/// <summary>
/// A Node.js application.
/// </summary>
public sealed class NodeResource : QylResourceBase<NodeResource>
{
    internal NodeResource(string name, string workingDirectory, string scriptPath, QylAppBuilder builder)
        : base(name, builder)
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory);
        ScriptPath = scriptPath;

        // Auto-assign port
        var port = PortAllocator.Next();
        AddPort(new PortBinding(port, port, "http"));
        SetEnvironment("PORT", port.ToString());
        SetEnvironment("NODE_ENV", "development");

        // Enable GenAI by default if configured
        if (builder.Options.GenAi)
        {
            WithGenAi();
        }
    }

    /// <summary>
    /// Path to the Node.js project directory.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Entry script path relative to working directory.
    /// </summary>
    public string ScriptPath { get; }

    /// <summary>
    /// Package manager to use. Default: npm
    /// </summary>
    public string PackageManager { get; private set; } = "npm";

    public override string Type => "node";

    /// <summary>
    /// Use npm as package manager.
    /// </summary>
    public NodeResource WithNpm()
    {
        PackageManager = "npm";
        return this;
    }

    /// <summary>
    /// Use yarn as package manager.
    /// </summary>
    public NodeResource WithYarn()
    {
        PackageManager = "yarn";
        return this;
    }

    /// <summary>
    /// Use pnpm as package manager.
    /// </summary>
    public NodeResource WithPnpm()
    {
        PackageManager = "pnpm";
        return this;
    }

    /// <summary>
    /// Use bun as package manager and runtime.
    /// </summary>
    public NodeResource WithBun()
    {
        PackageManager = "bun";
        return this;
    }
}
