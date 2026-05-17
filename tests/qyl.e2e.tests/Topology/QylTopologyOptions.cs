namespace Qyl.E2E.Tests.Topology;

public sealed record QylTopologyOptions
{
    public string CollectorImage { get; init; } = "qyl-collector:latest";

    public string McpImage { get; init; } = "qyl-mcp:latest";

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(90);

    public static QylTopologyOptions Default { get; } = new();
}
