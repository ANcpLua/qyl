/** Named analysis tools backed by the collector's generated query contracts. */
import type { McpServer } from "@modelcontextprotocol/server";
import { contractSchema } from "./contracts/tool-schemas.js";
import { runTool } from "./request-scope.js";
import { READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS, TELEMETRY_TOOL_AUTH_META } from "./tools.js";
import { queryAnalysis } from "./analysis-client.js";
import { collectorPatch, parseCollectorBody } from "./collector.js";
import { ifMatch, refusals, changeRefusals } from "./collector-resources.js";
import { toolScopeChallenge } from "./tool-scopes.js";
import { WRITE_SCOPES, UPDATE_TOOL_ANNOTATIONS, securityMeta } from "./write-tools.js";
import { CollectorError } from "./collector.js";
import { telemetryToolResult } from "./telemetry-redaction.js";

export function registerAnalysisTools(server: McpServer): void {
  server.registerTool(
    "triage",
    {
      title: "Triage",
      description: "Rank new or worsening failures, performance regressions, releases and silent services when investigating what changed.",
      inputSchema: contractSchema("Mcp.Tools.TriageInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "triage", 1, async (scope) =>
      queryAnalysis("triage", args, "Mcp.Tools.TriageOutput", scope)),
  );
  server.registerTool(
    "list_failures",
    {
      title: "List Failures",
      description: "List failures grouped by stable fingerprints when investigating recurring errors or their recorded status.",
      inputSchema: contractSchema("Mcp.Tools.ListFailuresInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "list_failures", 1, async (scope) =>
      queryAnalysis("list_failures", args, "Mcp.Tools.ListFailuresOutput", scope)),
  );
  server.registerTool(
    "get_failure_context",
    {
      title: "Get Failure Context",
      description: "Return the recorded failure path, correlated logs and comparison evidence when investigating a failed execution. Pass exactly one of trace_id, session_id, failure_id or ci_run_id.",
      inputSchema: contractSchema("Mcp.Tools.GetFailureContextInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "get_failure_context", 1, async (scope) =>
      queryAnalysis("get_failure_context", args, "Mcp.Tools.GetFailureContextOutput", scope)),
  );
  server.registerTool(
    "compare_groups",
    {
      title: "Compare Groups",
      description: "Compare attribute distributions and durations between two telemetry groups when identifying associations with a failure.",
      inputSchema: contractSchema("Mcp.Tools.CompareGroupsInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "compare_groups", 1, async (scope) =>
      queryAnalysis("compare_groups", args, "Mcp.Tools.CompareGroupsOutput", scope)),
  );
  server.registerTool(
    "compare_traces",
    {
      title: "Compare Traces",
      description: "Align two executions and locate their first divergence when comparing a failing path with another execution.",
      inputSchema: contractSchema("Mcp.Tools.CompareTracesInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "compare_traces", 1, async (scope) =>
      queryAnalysis("compare_traces", args, "Mcp.Tools.CompareTracesOutput", scope)),
  );
  server.registerTool(
    "verify_fix",
    {
      title: "Verify Fix",
      description: "Measure failure recurrence and traffic after a change when checking whether enough evidence supports a fix.",
      inputSchema: contractSchema("Mcp.Tools.VerifyFixInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "verify_fix", 1, async (scope) =>
      queryAnalysis("verify_fix", args, "Mcp.Tools.VerifyFixOutput", scope)),
  );
  server.registerTool(
    "aggregate_telemetry",
    {
      title: "Aggregate Telemetry",
      description: "Group spans or logs and calculate counts, error rates and durations when comparing operations or attributes.",
      inputSchema: contractSchema("Mcp.Tools.AggregateTelemetryInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "aggregate_telemetry", 1, async (scope) =>
      queryAnalysis("aggregate_telemetry", args, "Mcp.Tools.AggregateTelemetryOutput", scope)),
  );
  server.registerTool(
    "discover_attributes",
    {
      title: "Discover Attributes",
      description: "List recorded attribute keys, value distributions and convention descriptions when choosing telemetry filters.",
      inputSchema: contractSchema("Mcp.Tools.DiscoverAttributesInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "discover_attributes", 1, async (scope) =>
      queryAnalysis("discover_attributes", args, "Mcp.Tools.DiscoverAttributesOutput", scope)),
  );
  server.registerTool(
    "get_agent_run",
    {
      title: "Get Agent Run",
      description: "Reconstruct agent and tool execution steps, token usage and repeated calls when investigating an agent run.",
      inputSchema: contractSchema("Mcp.Tools.GetAgentRunInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "get_agent_run", 1, async (scope) =>
      queryAnalysis("get_agent_run", args, "Mcp.Tools.GetAgentRunOutput", scope)),
  );
  server.registerTool(
    "list_releases",
    {
      title: "List Releases",
      description: "List observed service versions and their before-and-after impact when investigating a release.",
      inputSchema: contractSchema("Mcp.Tools.ListReleasesInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "list_releases", 1, async (scope) =>
      queryAnalysis("list_releases", args, "Mcp.Tools.ListReleasesOutput", scope)),
  );
  server.registerTool(
    "get_service_map",
    {
      title: "Get Service Map",
      description: "Return service dependencies and their traffic, error rates and latency when investigating calls between services.",
      inputSchema: contractSchema("Mcp.Tools.GetServiceMapInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "get_service_map", 1, async (scope) =>
      queryAnalysis("get_service_map", args, "Mcp.Tools.GetServiceMapOutput", scope)),
  );
  server.registerTool(
    "check_telemetry_health",
    {
      title: "Check Telemetry Health",
      description: "Report silent services, orphan spans and missing convention attributes when checking telemetry coverage.",
      inputSchema: contractSchema("Mcp.Tools.CheckTelemetryHealthInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "check_telemetry_health", 1, async (scope) =>
      queryAnalysis("check_telemetry_health", args, "Mcp.Tools.CheckTelemetryHealthOutput", scope)),
  );
  server.registerTool(
    "get_mcp_stats",
    {
      title: "Get MCP Stats",
      description: "Return MCP request totals, failure classes, latency, per-tool statistics, time buckets and server, transport and method breakdowns when investigating MCP tool health.",
      inputSchema: contractSchema("Mcp.Tools.GetMcpStatsInput"),
      annotations: READ_ONLY_TELEMETRY_TOOL_ANNOTATIONS,
      _meta: TELEMETRY_TOOL_AUTH_META,
    },
    (args, ctx) => runTool(ctx, "get_mcp_stats", 1, async (scope) =>
      queryAnalysis("get_mcp_stats", args, "Mcp.Tools.GetMcpStatsOutput", scope)),
  );
  server.registerTool(
    "update_failure_status",
    {
      title: "Update Failure Status",
      description: "Set a failure's status or assignee when recording triage decisions, returning the stored failure available through list_failures.",
      inputSchema: contractSchema("Mcp.Tools.UpdateFailureStatusInput"),
      annotations: UPDATE_TOOL_ANNOTATIONS,
      scopeChallenge: toolScopeChallenge("update_failure_status", WRITE_SCOPES),
      _meta: securityMeta(WRITE_SCOPES),
    },
    (args, ctx) => runTool(ctx, "update_failure_status", 1, async (scope) => {
      if (args.ignore_until !== undefined && args.status !== "ignored") {
        throw new CollectorError("ignore_until: is valid only with status=ignored", false, 400, "invalid_argument", undefined,
          [{ field: "ignore_until", message: "is valid only with status=ignored", code: "ignore_until.requires_ignored" }]);
      }
      const { failure_id, etag, ...changes } = args;
      const path = `/api/v1/failures/${encodeURIComponent(failure_id)}`;
      const response = await refusals(changeRefusals("failure", failure_id, "write"), () => collectorPatch(path, changes, {
        ...scope.collector,
        headers: { ...scope.collector.headers, ...ifMatch(etag) },
      }));
      const output = parseCollectorBody(response, path, "Mcp.Tools.UpdateFailureStatusOutput");
      await scope.step("Failure status saved");
      const { failure } = output;
      return telemetryToolResult(
        `update_failure_status: failure ${failure.failure_id} is now ${failure.status}${failure.assignee ? `, assignee ${failure.assignee}` : ""}, etag ${failure.etag}. → list_failures failure_id=${failure.failure_id}`,
        output,
      );
    }),
  );
}
