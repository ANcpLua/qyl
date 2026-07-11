namespace Qyl.Host.Mcp;

/// <summary>
/// Resource kinds for MCP servers, mirroring qyl.mcp's TS runner (<c>ResourceKind</c> in
/// <c>runner/src/resources.ts</c>). All three are connection-only in the engine: the SDK
/// transport owns the connection (and, for stdio, the child process).
/// </summary>
public static class McpResourceKinds
{
    /// <summary>SDK-spawned child speaking MCP over stdio.</summary>
    public const string Stdio = "stdio";

    /// <summary>Already-running MCP server reached over HTTP (streamable HTTP / SSE).</summary>
    public const string Http = "http";

    /// <summary>MCP server hosted inside the runner process over an in-memory stream pair.</summary>
    public const string InProc = "inproc";
}
