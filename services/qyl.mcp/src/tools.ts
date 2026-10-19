import type { McpServer } from "@modelcontextprotocol/server";
import { contractSchema } from "./contracts/tool-schemas.js";
import { runTool } from "./request-scope.js";
import { queryAnalysis } from "./analysis-client.js";
import type { ToolAnnotations } from "@modelcontextprotocol/server";
import { QYL_MCP_SCOPE } from "./oauth.js";
/**
 * The qyl telemetry tools only query the configured collector. They neither
 * mutate it nor reach an unbounded set of external entities.
 */
export const READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS = {
  readOnlyHint: true,
  destructiveHint: false,
  idempotentHint: true,
  openWorldHint: false,
} as const satisfies ToolAnnotations;

/** SDK v2.0.0 preserves tool _meta, but has no top-level securitySchemes
 * registration field. OpenAI reads this documented compatibility form;
 * other MCP clients can follow the RFC 9728 challenge.
 */
export const TELEMETRY_TOOL_AUTH_META = {
  securitySchemes: [{ type: "oauth2", scopes: [QYL_MCP_SCOPE] }],
} as const;

export function registerTelemetryTools(server: McpServer): void {
  server.registerTool(
    "list_traces",
    {
      title: "List Traces",
      description: "List filtered trace summaries when finding failed, slow or matching executions.",
      inputSchema: contractSchema("Mcp.Tools.ListTracesInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "list_traces", 1, async (scope) =>
      queryAnalysis("list_traces", args, "Mcp.Tools.ListTracesOutput", scope)),
  );
  server.registerTool(
    "get_trace",
    {
      title: "Get Trace",
      description: "Return selected spans, correlated logs and annotations when examining one execution.",
      inputSchema: contractSchema("Mcp.Tools.GetTraceInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "get_trace", 1, async (scope) =>
      queryAnalysis("get_trace", args, "Mcp.Tools.GetTraceOutput", scope)),
  );
  server.registerTool(
    "list_sessions",
    {
      title: "List Sessions",
      description: "List filtered sessions with failures and token usage when locating active or problematic agent sessions.",
      inputSchema: contractSchema("Mcp.Tools.ListSessionsInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "list_sessions", 1, async (scope) =>
      queryAnalysis("list_sessions", args, "Mcp.Tools.ListSessionsOutput", scope)),
  );
  server.registerTool(
    "search_logs",
    {
      title: "Search Logs",
      description: "Search log records and surrounding context when locating evidence linked to an execution.",
      inputSchema: contractSchema("Mcp.Tools.SearchLogsInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "search_logs", 1, async (scope) =>
      queryAnalysis("search_logs", args, "Mcp.Tools.SearchLogsOutput", scope)),
  );
}
