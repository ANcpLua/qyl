// ROOT CAUSE: Qyl.Agents.McpProtocolHandler emits mcp.method.name on transport
// spans but qyl.mcp (the 77-tool server) emits zero MCP semconv span attributes.
// mcp.session.id and mcp.protocol.version constants exist in McpAttributes.g.cs
// but are never referenced by any SetTag call in the serving plane.
//
// Fix location: qyl.mcp request pipeline (not a generator — runtime enrichment).
// The generator could emit these via OTelEmitter if tools carried session context.

namespace Qyl.Mcp.Hosting;

// When implemented: middleware or filter that adds to every MCP span:
//   activity.SetTag(McpAttributes.MethodName, method);         // already in Qyl.Agents
//   activity.SetTag(McpAttributes.ProtocolVersion, version);   // missing everywhere
//   activity.SetTag(McpAttributes.SessionId, sessionId);       // missing everywhere
