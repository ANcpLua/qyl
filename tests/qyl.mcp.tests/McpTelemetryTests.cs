using qyl.contracts.Attributes;

namespace Qyl.Mcp.Tests;

public sealed class McpTelemetryTests : IDisposable
{
    private readonly List<Activity> _collected = [];
    private readonly ActivityListener _listener;

    public McpTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == McpAttributes.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _collected.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void ActivitySource_EmitsUnderQylMcpName()
    {
        TelemetryConstants.ActivitySource.Name.Should().Be("qyl.mcp");

        using var activity = TelemetryConstants.ActivitySource.StartActivity("mcp.receive ping", ActivityKind.Server);

        activity.Should().NotBeNull();
        _collected.Should().ContainSingle(static a => a.OperationName == "mcp.receive ping");
    }

    [Fact]
    public void McpAttributes_WireValues_MatchSemconv()
    {
        // Protects the wire format — if someone renames a value, downstream breaks silently
        McpAttributes.MethodName.Should().Be("mcp.method.name");
        McpAttributes.ProtocolVersion.Should().Be("mcp.protocol.version");
        McpAttributes.SessionId.Should().Be("mcp.session.id");
        McpAttributes.ServerName.Should().Be("mcp.server.name");
        McpAttributes.JsonrpcRequestId.Should().Be("jsonrpc.request.id");
        McpAttributes.JsonrpcProtocolVersion.Should().Be("jsonrpc.protocol.version");
        McpAttributes.ErrorType.Should().Be("error.type");
    }

    [Fact]
    public void McpAttributes_Methods_MatchMcpSpec()
    {
        McpAttributes.Methods.ToolsCall.Should().Be("tools/call");
        McpAttributes.Methods.PromptsGet.Should().Be("prompts/get");
        McpAttributes.Methods.ResourcesRead.Should().Be("resources/read");
        McpAttributes.Methods.Initialize.Should().Be("initialize");
    }
}
