/** Collector trace polling for opt-in Events; the same analysis read used by tools. */
import { collectorPost, parseCollectorBody, type CollectorRequestOptions } from "./collector.js";
import { redactTelemetry } from "./telemetry-redaction.js";
import type { ListTracesInput, ToolTraceRow } from "./contracts/generated/api.js";

export async function fetchTraces(filters: ListTracesInput, options: CollectorRequestOptions = {}): Promise<ToolTraceRow[]> {
  const path = "/api/v1/analysis/list_traces";
  const output = parseCollectorBody(await collectorPost(path, filters, options), path, "Mcp.Tools.ListTracesOutput");
  return redactTelemetry(output.traces);
}
