namespace qyl.cli.Detection;

/// <summary>
/// Auto-detects the project stack from files in the working directory.
/// </summary>
public static class StackDetector
{
    public static DetectedStack Detect(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return DetectedStack.Unknown;
        }

        // Check in priority order per spec
        if (Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0
            || Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).Length > 0
            || Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly).Length > 0)
        {
            return DetectedStack.Dotnet;
        }

        if (File.Exists(Path.Combine(directory, "package.json")))
        {
            return DetectedStack.Node;
        }

        if (File.Exists(Path.Combine(directory, "requirements.txt"))
            || File.Exists(Path.Combine(directory, "pyproject.toml")))
        {
            return DetectedStack.Python;
        }

        if (File.Exists(Path.Combine(directory, "docker-compose.yml"))
            || File.Exists(Path.Combine(directory, "docker-compose.yaml"))
            || File.Exists(Path.Combine(directory, "compose.yaml")))
        {
            return DetectedStack.Docker;
        }

        return DetectedStack.Unknown;
    }
}

public enum DetectedStack
{
    Unknown,
    Dotnet,
    Node,
    Python,
    Docker,
}
