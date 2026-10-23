/** Shared HTTP transport; every callable analysis operation is registered by name. */
import type { CallToolResult } from "@modelcontextprotocol/server";
import { CollectorError, collectorPost, parseCollectorBody } from "./collector.js";
import type { ContractSchemaName } from "./contracts/tool-schemas.js";
import { ANALYSIS_RESPONSE_BYTES, compactAnalysisResult, responseBytes } from "./analysis-budget.js";
import { analysisHeadline } from "./analysis-summary.js";
import type { ToolScope } from "./request-scope.js";
import { telemetryToolResult } from "./telemetry-redaction.js";

export async function queryAnalysis<Input extends object>(
  operation: string,
  input: Input,
  schema: Extract<ContractSchemaName, `Mcp.Tools.${string}Output`>,
  scope: ToolScope,
): Promise<CallToolResult> {
  const path = `/api/v1/analysis/${operation}`;
  const deadline = Date.now() + 55_000;
  let query: object = input;
  let previousPageSize = Infinity;
  let previousPhases = Infinity;
  for (;;) {
    const remaining = deadline - Date.now();
    if (remaining <= 0) throw new CollectorError("The query exceeded its time budget; narrow the time window and matchers.");
    const response = await collectorPost(path, query, { ...scope.collector, timeoutMs: remaining });
    const output = parseCollectorBody(response, path, schema);
    const result = telemetryToolResult(analysisHeadline(operation, input, output), output);
    // Bytes give a conservative upper bound even for token-dense identifiers.
    const bytes = responseBytes(result);
    if (bytes <= ANALYSIS_RESPONSE_BYTES) {
      await scope.step(`Queried ${operation}`);
      return result;
    }
    // Re-query the collector so its cursor, coverage and totals describe the
    // smaller page. Never slice a response and leave its paging metadata stale.
    const args = query as Record<string, unknown>;
    const page = output as Record<string, unknown>;
    const originalPoints = operation === "query_metric" && args.raw === true;
    const sizeKey = operation === "get_trace" ? "max_spans" : "limit";
    const trace = page.trace as { spans?: unknown[] } | undefined;
    const observed = page.observed as { spans?: unknown[]; logs?: unknown[] } | undefined;
    // ci_log pages a run's phases apart from its runs, by phase_limit: each shrinks by its own size, and a smaller page
    // of phases comes with the next_phase_cursor that resumes at the first phase it does not return.
    const phases = operation === "ci_log" ? (page.phases as unknown[] | undefined)?.length ?? 0 : 0;
    const sizes = [trace?.spans?.length, observed?.spans?.length, observed?.logs?.length,
      ...Object.entries(page).filter(([key, items]) => Array.isArray(items) && !(operation === "ci_log" && key === "phases"))
        .map(([, items]) => (items as unknown[]).length)].filter((size): size is number => size !== undefined);
    const pageSize = originalPoints ? (page.points as unknown[] | undefined)?.length ?? 0 : Math.max(0, ...sizes);
    const shrink = (size: number): number => Math.max(1, Math.floor(size * ANALYSIS_RESPONSE_BYTES / bytes * 0.8));
    const fewerItems = pageSize > 1 && pageSize < previousPageSize;
    const fewerPhases = phases > 1 && phases < previousPhases;
    if (fewerItems || fewerPhases) {
      if (fewerItems) previousPageSize = pageSize;
      if (fewerPhases) previousPhases = phases;
      const limit = shrink(pageSize);
      // The collector creates the smaller page and its continuation together.
      query = { ...args, ...(page.window as object),
        ...(fewerItems ? { [sizeKey]: limit } : {}),
        ...(fewerItems && operation === "get_failure_context" ? { max_spans: limit, max_logs: limit } : {}),
        ...(fewerPhases ? { phase_limit: shrink(phases) } : {}) };
      continue;
    }
    // Original numeric oneofs, buckets and exemplars must remain complete.
    // A smaller page must come from the Collector with its signed continuation.
    if (originalPoints) {
      const route = `/api/v1/metrics/${encodeURIComponent(String(args.metric_name))}/series/${encodeURIComponent(String(page.series_id))}/points`;
      throw new CollectorError(
        `The complete original metric point page exceeds the MCP response budget. Read it through GET ${route} using the same time window and point cursor.`,
        false, undefined, "response_too_large",
      );
    }
    await scope.step(`Queried ${operation}; large evidence compacted`);
    return compactAnalysisResult(operation, input, output);
  }
}
