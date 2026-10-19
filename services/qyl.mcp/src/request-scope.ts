/**
 * The request-scoped helpers every tool handler runs inside.
 *
 * The SDK hands each handler a `ServerContext` whose `mcpReq` carries the
 * cancellation signal, the request's `_meta`, and a request-bound `notify`.
 * `runTool` resolves those once and
 * hands the work a plain `ToolScope`: the data layer (`data.ts`, `metrics.ts`)
 * takes `CollectorRequestOptions` with an `AbortSignal`, never the SDK context.
 *
 * Two channels, one frame:
 *   - cancellation: `scope.collector.signal` is `ctx.mcpReq.signal`, aborted on
 *     `notifications/cancelled` for this request and when the connection
 *     closes; `collectorRequest` races it against its own timeout.
 *   - progress: `scope.step(message)` sends `notifications/progress` when the
 *     request carried a `progressToken`, and nothing when it did not. The
 *     reporter counts its own steps, so `progress` increases per token by
 *     construction; `runTool` reports the final step itself once the result is
 *     built, so a handler declares only the steps it reports.
 *
 * Every result, success or failure, has one shape: `content[0]` is a one-line
 * headline, `content[1]` the compact JSON of the whole result, and
 * `structuredContent` the same object (telemetry-redaction.ts).
 */
import type { ToolError } from "./contracts/generated/api.js";
import type { CallToolResult, ServerContext } from "@modelcontextprotocol/server";
import {
  CollectorError,
  type CollectorRequestOptions,
  isRetryableToolError,
} from "./collector.js";
import { CollectorAccessError, collectorAccessForSubject } from "./collector-access.js";
import { assertCollectorContract, CollectorContractMismatchError } from "./contract-handshake.js";
import { formatArgumentIssue, type ArgumentIssue } from "./contracts/tool-schemas.js";
import { headlineText, redactTelemetry, redactTelemetryText } from "./telemetry-redaction.js";

/** What a tool's work receives: collector options and a progress step. */
export interface ToolScope {
  /** Collector options carrying this request's cancellation signal. */
  collector: CollectorRequestOptions;
  /** Report one more completed step; the message says what just finished. */
  step(message: string): Promise<void>;
}

/** Arguments the tool's input schema rejected before the tool ran. */
export class InvalidArgumentsError extends Error {
  constructor(readonly issues: readonly ArgumentIssue[]) {
    super(issues.length === 0 ? "The arguments do not match the tool's input schema." : issues.map(formatArgumentIssue).join("; "));
    this.name = "InvalidArgumentsError";
  }
}

/** Where a call that named an unknown record gets a real one. */
const ID_SOURCES: Readonly<Record<string, string>> = {
  get_trace: "list_traces returns the trace ids of this project and window",
  compare_traces: "list_traces returns the trace ids of this project and window",
  get_failure_context: "triage returns failure ids, list_traces trace ids and list_sessions session ids",
  verify_fix: "triage returns failure ids; a verify_id comes from an earlier verify_fix",
  update_failure_status: "triage with status all returns each failure's id and current etag",
  get_agent_run: "list_sessions returns session ids",
  query_metric: "list_metrics returns metric names; a raw query_metric that matches several series names their series ids",
  run_saved_query: "run_saved_query without id lists the saved query ids",
  save_query: "run_saved_query without id lists each saved query's id and current etag",
  annotate: "list_traces, list_sessions or triage return the ids a target names, and get_trace, list_sessions or get_failure_context each annotation's id and current etag",
  delete_record: "run_saved_query without id, get_trace, list_sessions or get_failure_context return ids and current etags",
  ci_log: "ci_log without run_id lists the recent runs",
};

function hintFor(tool: string, detail: ToolError): string | undefined {
  switch (detail.code) {
    case "invalid_argument":
    case "collector_rejected":
      return detail.field === undefined
        ? `Correct the arguments as the message says and call ${tool} again.`
        : `Correct ${detail.field}${detail.allowed === undefined ? "" : " to one of the accepted values"} and call ${tool} again.`;
    case "not_found":
      return `Take ids only from earlier results: ${ID_SOURCES[tool] ?? "an earlier result of this project and window names them"}.`;
    case "conflict":
      return "Choose another name, or a new idempotency_key only when a second object is intended.";
    case "precondition_failed":
      return "Read the object again and retry with its current etag.";
    case "precondition_required":
      return "Pass the object's current etag.";
    case "collector_timeout":
      return "Narrow the time window or the filters, or retry later.";
    case "collector_unreachable":
    case "collector_rate_limited":
    case "collector_error":
      return "Retry the same call later.";
    case "collector_contract_mismatch":
      return "Retry once the Collector and this server serve one contract revision.";
    case "collector_unauthorized":
      return "The Collector refused this server's credential; tell the qyl operator.";
    case "no_project_access":
      return "Ask the qyl operator to assign this account to a project.";
    case "insufficient_scope":
      return "Sign in again and grant the scope the tool needs.";
    case "contract_mismatch":
    case "internal":
      return "Tell the qyl operator; repeating the call will not help.";
    default:
      return undefined;
  }
}

/** `structuredContent.error` of a failed call; `describeToolError` adds a hint where the Collector sent none. */
function detailOf(err: unknown): ToolError {
  if (err instanceof InvalidArgumentsError) {
    const [first] = err.issues;
    return {
      code: "invalid_argument", message: err.message, retryable: false,
      ...(first === undefined || first.field === "" ? {} : { field: first.field }),
      ...(first?.allowed === undefined ? {} : { allowed: [...first.allowed] }),
      ...(err.issues.length === 0 ? {} : {
        fields: err.issues.map((issue) => ({
          field: issue.field || "arguments", message: issue.message, code: issue.keyword,
          ...(issue.allowed === undefined ? {} : { allowed: issue.allowed.map(String) }),
        })),
      }),
    };
  }
  if (err instanceof CollectorError) {
    // The Collector names the rejected argument, the values it accepts and what to send instead,
    // or for a 404 the record it did not find; the first rejected argument is the one to change.
    const [first] = err.fields ?? [];
    return {
      code: err.code, message: err.message, retryable: isRetryableToolError(err.code),
      ...(first === undefined ? {} : { field: first.field }),
      ...(first?.allowed === undefined ? {} : { allowed: first.allowed }),
      ...(first?.hint === undefined ? {} : { hint: first.hint }),
      ...(err.missing === undefined ? {} : err.missing),
      ...(err.problemType === undefined ? {} : { problem_type: err.problemType }),
      ...(err.fields === undefined ? {} : { fields: err.fields }),
    };
  }
  if (err instanceof CollectorAccessError) {
    return { code: "no_project_access", message: err.message, retryable: false };
  }
  if (err instanceof CollectorContractMismatchError) {
    return {
      code: err.code, message: err.message, retryable: isRetryableToolError(err.code),
      collector_contract_revision: err.collectorRevision, server_contract_revision: err.serverRevision,
    };
  }
  return { code: "internal", message: "Telemetry request failed.", retryable: false };
}

/**
 * The contract's `ToolError` of a failed call, with a hint for the next step: the
 * Collector's own when it sent one, else this server's for the code. Anything that
 * is not a known failure stays "Telemetry request failed." with the code
 * `internal`: an unexpected exception's message is not for the model.
 */
export function describeToolError(tool: string, err: unknown): ToolError {
  const detail = detailOf(err);
  const hint = detail.hint ?? hintFor(tool, detail);
  return hint === undefined ? detail : { ...detail, hint };
}

/**
 * Uniform failure result: a headline that names the code, the message and the
 * next step, then the error as JSON in `content[1]` and `structuredContent`,
 * and `isError`. Never a thrown exception: the model reads and recovers.
 */
export function toolError(tool: string, err: unknown): CallToolResult {
  const detail = describeToolError(tool, err);
  const error = redactTelemetry({ ...detail, message: redactTelemetryText(detail.message) });
  const structured = { error };
  return {
    content: [
      { type: "text", text: headlineText(`${tool} failed: ${error.code} — ${error.message}${error.hint === undefined ? "" : ` → ${error.hint}`}`) },
      { type: "text", text: JSON.stringify(structured) },
    ],
    structuredContent: structured,
    isError: true,
  };
}

/**
 * Run one tool call inside its request scope.
 *
 * `steps` is how many `scope.step` calls the work makes on its success path;
 * the reported `total` is one more, for the final "Result ready" step that
 * `runTool` sends once the work has returned. A thrown error becomes the
 * uniform `toolError` result. Nothing is sent once the request is cancelled:
 * the SDK discards the result of a cancelled request, and a notification
 * about it would describe work the client already gave up on.
 */
export async function runTool(
  ctx: ServerContext,
  tool: string,
  steps: number,
  work: (scope: ToolScope) => Promise<CallToolResult>,
): Promise<CallToolResult> {
  const { signal } = ctx.mcpReq;
  const progressToken = ctx.mcpReq._meta?.progressToken;
  const total = steps + 1;
  let progress = 0;

  const step = async (message: string): Promise<void> => {
    if (progressToken === undefined || signal.aborted) return;
    progress += 1;
    await ctx.mcpReq.notify({
      method: "notifications/progress",
      params: { progressToken, progress, total, message },
    });
  };

  try {
    assertCollectorContract();
    const access = collectorAccessForSubject(ctx.http?.authInfo?.extra?.["subject"]);
    const result = await work({
      collector: { signal, ...(access === undefined ? {} : { access }) },
      step,
    });
    await step("Result ready");
    return result;
  } catch (err) {
    return toolError(tool, err);
  }
}
