// Copyright (c) 2025-2026 ancplua
//
// Hand-maintained OTel 1.40.0 semconv facade for qyl consumers.
// Previously generated from eng/semconv/qyl-extensions.json — migrated to
// hand-edit on 2026-04-21 during the Weaver migration. Bump semconv keys
// by hand when upstream moves; qyl-specific enum extensions live here.

namespace qyl.contracts.Attributes;

/// <summary>
///     OTel 1.40.0 MCP (Model Context Protocol) semantic convention attribute keys.
///     Status: Experimental
///     https://github.com/open-telemetry/semantic-conventions/blob/main/docs/gen-ai/mcp.md
/// </summary>
public static class McpAttributes
{
    /// <summary>SchemaUrl.</summary>
    public const string SchemaUrl = "https://opentelemetry.io/schemas/1.40.0";

    /// <summary>SourceName.</summary>
    public const string SourceName = "qyl.mcp";

    /// <summary>mcp.method.name</summary>
    public const string MethodName = "mcp.method.name";

    /// <summary>mcp.protocol.version</summary>
    public const string ProtocolVersion = "mcp.protocol.version";

    /// <summary>mcp.session.id</summary>
    public const string SessionId = "mcp.session.id";

    /// <summary>mcp.server.name</summary>
    public const string ServerName = "mcp.server.name";

    /// <summary>jsonrpc.request.id</summary>
    public const string JsonrpcRequestId = "jsonrpc.request.id";

    /// <summary>jsonrpc.protocol.version</summary>
    public const string JsonrpcProtocolVersion = "jsonrpc.protocol.version";

    /// <summary>rpc.system</summary>
    public const string RpcSystem = "rpc.system";

    /// <summary>rpc.method</summary>
    public const string RpcMethod = "rpc.method";

    /// <summary>error.type</summary>
    public const string ErrorType = "error.type";

    /// <summary>server.address</summary>
    public const string ServerAddress = "server.address";

    /// <summary>server.port</summary>
    public const string ServerPort = "server.port";

    /// <summary>Well-known MCP method name values.</summary>
    public static class Methods
    {
        /// <summary>tools/call</summary>
        public const string ToolsCall = "tools/call";

        /// <summary>tools/list</summary>
        public const string ToolsList = "tools/list";

        /// <summary>prompts/get</summary>
        public const string PromptsGet = "prompts/get";

        /// <summary>prompts/list</summary>
        public const string PromptsList = "prompts/list";

        /// <summary>resources/read</summary>
        public const string ResourcesRead = "resources/read";

        /// <summary>resources/list</summary>
        public const string ResourcesList = "resources/list";

        /// <summary>initialize</summary>
        public const string Initialize = "initialize";

        /// <summary>ping</summary>
        public const string Ping = "ping";

    }

    /// <summary>Well-known RPC system values.</summary>
    public static class Systems
    {
        /// <summary>jsonrpc</summary>
        public const string JsonRpc = "jsonrpc";

    }

    /// <summary>Well-known JSON-RPC protocol version values.</summary>
    public static class JsonrpcVersions
    {
        /// <summary>2.0</summary>
        public const string V2 = "2.0";

    }

}
