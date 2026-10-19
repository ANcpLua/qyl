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
      description: "List instruments and their units, kinds and aggregation support when choosing a metric.",
      inputSchema: contractSchema("Mcp.Tools.ListMetricsInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "list_metrics", 1, async (scope) =>
      queryAnalysis("list_metrics", args, "Mcp.Tools.ListMetricsOutput", scope)),
  );
  server.registerTool(
    "get_metric_series",
    {
      title: "Get Metric Series",
      description: "Discover metric streams and attribute cardinality when choosing filters, or select series_id to page complete original points with points_cursor. Oversized individual points remain available through the HTTP points API.",
      inputSchema: contractSchema("Mcp.Tools.GetMetricSeriesInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "get_metric_series", 1, async (scope) =>
      queryAnalysis("get_metric_series", args, "Mcp.Tools.GetMetricSeriesOutput", scope)),
  );
  server.registerTool(
    "query_metric",
    {
      title: "Query Metric",
      description: "Query float64 metric projections, counter changes or ratios when measuring trends across a time window. Use get_metric_series with series_id for original numeric values, buckets and exemplars.",
      inputSchema: contractSchema("Mcp.Tools.QueryMetricInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "query_metric", 1, async (scope) =>
      queryAnalysis("query_metric", args, "Mcp.Tools.QueryMetricOutput", scope)),
  );
}
