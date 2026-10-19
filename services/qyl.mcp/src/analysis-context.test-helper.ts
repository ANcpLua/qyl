import type { ToolCoverage, ToolLogRow, ToolTrace, ToolTraceRow } from "./contracts/generated/api.js";
import type { QylLogRecord, QylTrace } from "./wire.js";

/** Fixed metadata for transport/schema tests; query semantics use the OTLP answer-key suite. */
export function readContext(count = 0, paged = true) {
  const coverage: ToolCoverage = {
    examined: count, matched: count, returned: count,
    truncated: false, scan_complete: true, as_of: "2026-10-12T10:00:00Z",
  };
  return { ...(paged ? { has_more: false } : {}), window: { start_time: "2026-10-12T09:00:00Z", end_time: "2026-10-12T10:00:00Z" }, coverage };
}
const instant = (nanos: string) => new Date(Number(BigInt(nanos) / 1_000_000n)).toISOString();
const milliseconds = (start: string, end: string) => Number(BigInt(end) - BigInt(start)) / 1e6;
const errors = (trace: QylTrace) => trace.spans.filter((span) => span.status.code === 2).length;
/** The get_trace shape of a fixture trace. */
export function toolTrace(trace: QylTrace): ToolTrace {
  return {
    trace_id: trace.trace_id, start_time: trace.start_time, end_time: trace.end_time, duration_ms: Number(trace.duration_ns) / 1e6,
    span_count: trace.span_count, error_count: errors(trace), services: trace.services,
    spans: trace.spans.map((span) => ({
      span_id: span.span_id, ...(span.parent_span_id ? { parent_span_id: span.parent_span_id } : {}),
      service: span.resource?.service_name ?? "unknown_service", name: span.name,
      start_time: instant(span.start_time_unix_nano), duration_ms: milliseconds(span.start_time_unix_nano, span.end_time_unix_nano),
      status: span.status.code === 2 ? "error" as const : span.status.code === 1 ? "ok" as const : "unset" as const,
      ...(span.status.message ? { message: span.status.message } : {}),
      ...(span.attributes ? { attributes: span.attributes } : {}),
    })),
  };
}
const SEVERITIES = ["TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL"] as const;
/** The search_logs row of a fixture log record. */
export function toolLog(log: QylLogRecord): ToolLogRow {
  const severity = log.severity_text ?? (log.severity_number >= 1 && log.severity_number <= 24 ? SEVERITIES[Math.floor((log.severity_number - 1) / 4)] : undefined);
  return {
    time: instant(log.time_unix_nano ?? log.observed_time_unix_nano ?? "0"),
    ...(severity ? { severity } : {}),
    severity_number: log.severity_number,
    service: log.resource.service_name ?? "unknown_service",
    body: typeof log.body === "string" ? log.body : JSON.stringify(log.body),
    ...(log.trace_id ? { trace_id: log.trace_id } : {}),
    ...(log.span_id ? { span_id: log.span_id } : {}),
    attributes: log.attributes ?? [],
  };
}
/** The list_traces row of a fixture trace. */
export function toolSummary(trace: QylTrace): ToolTraceRow {
  const root = trace.spans.find((span) => !span.parent_span_id) ?? trace.spans[0]!;
  return {
    trace_id: trace.trace_id, root: { service: root.resource?.service_name ?? "unknown_service", name: root.name },
    start_time: trace.start_time, duration_ms: Number(trace.duration_ns) / 1e6, span_count: trace.span_count,
    error_count: errors(trace), services: trace.services, failure_ids: [],
  };
}
