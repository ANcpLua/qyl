import type { McpServer } from "@modelcontextprotocol/server";
import { contractSchema } from "./contracts/tool-schemas.js";
import { runTool } from "./request-scope.js";
import { queryAnalysis } from "./analysis-client.js";
import { READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS, TELEMETRY_TOOL_AUTH_META } from "./tools.js";
export function registerMetricsTools(server: McpServer): void {
  server.registerTool(
    "list_metrics",
    {
      title: "List Metrics",
      description: "List instruments with their units, kinds, aggregation support and attribute keys when choosing a metric and how to split it.",
      inputSchema: contractSchema("Mcp.Tools.ListMetricsInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "list_metrics", 1, async (scope) =>
      queryAnalysis("list_metrics", args, "Mcp.Tools.ListMetricsOutput", scope)),
  );
  server.registerTool(
    "query_metric",
    {
      title: "Query Metric",
      description: "Query float64 metric projections, counter changes or ratios when measuring trends across a time window, or with raw one series' original values, buckets and exemplars.",
      inputSchema: contractSchema("Mcp.Tools.QueryMetricInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "query_metric", 1, async (scope) =>
      queryAnalysis("query_metric", args, "Mcp.Tools.QueryMetricOutput", scope)),
  );
}
