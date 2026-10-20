/**
 * Collector REST client.
 *
 * All operations query the configured Collector;
 * connection, HTTP, JSON, and contract failures surface to the caller rather
 * than silently substituting generated telemetry or invented defaults.
 */

import type {
  NotFoundError,
  ProblemDetails,
  ToolErrorCode,
  ValidationErrorDetail,
} from "./contracts/generated/api.js";
import type { ContractDefinitions } from "./contracts/generated/definitions.js";
import { checkContract, type ContractSchemaName } from "./contracts/tool-schemas.js";
import { redactTelemetryText } from "./telemetry-redaction.js";
import { collectorHeaders, collectorUrl } from "./config.js";
import { CollectorAccessError, readCollectorProjects, type CollectorAccess } from "./collector-access.js";
import { collectorOutageSignal, collectorUnavailable } from "./contract-handshake.js";

/**
 * The stable failure codes a tool result carries in `structuredContent.error`:
 * the contract's `Mcp.Tools.ToolErrorCode`, so the server cannot invent a code
 * the published `ToolError` model does not know. A model branches on the code,
 * never on the message text, which stays free to describe the specific failure.
 */
export type { ToolErrorCode };

/** Whether the same call can succeed when repeated unchanged. */
export function isRetryableToolError(code: ToolErrorCode): boolean {
  return (
    code === "collector_unreachable" ||
    code === "collector_timeout" ||
    code === "collector_rate_limited" ||
    code === "collector_error" ||
    // Answered while the Collector serves another contract revision; the deploy that brings both
    // sides to one revision makes the same call succeed.
    code === "collector_contract_mismatch" ||
    // Answered while the Collector does not answer its health check, as while it restarts.
    code === "collector_unavailable"
  );
}

function codeForStatus(status: number): ToolErrorCode {
  if (status === 404) return "not_found";
  if (status === 401 || status === 403) return "collector_unauthorized";
  // The write routes' answers: a duplicate name or reused retry key, a stale
  // If-Match etag, and a missing If-Match.
  if (status === 409) return "conflict";
  if (status === 412) return "precondition_failed";
  if (status === 428) return "precondition_required";
  if (status === 429) return "collector_rate_limited";
  if (status >= 500) return "collector_error";
  return "collector_rejected";
}

/**
 * A body the contract does not allow. During a deploy window the edge answers
 * with an HTML 502/503, and a rate limiter with an empty 429; there the status
 * is the truer signal, so 429 and 5xx keep their retryable codes. Every other
 * unexpected body is a contract mismatch.
 */
function codeForUnexpectedBody(response: Response): ToolErrorCode {
  if (!response.ok && (response.status === 429 || response.status >= 500)) {
    return codeForStatus(response.status);
  }
  return "contract_mismatch";
}

/** What a Collector 404 names as missing: the kind of record and its identifier. */
interface MissingResource {
  readonly resource_type?: string;
  readonly resource_id?: string;
}

/**
 * Error with a message already suitable for showing to the model/user, and a
 * stable `code`. Without an explicit code, a response status decides it, a
 * connection failure is `collector_unreachable`, and anything else is the
 * caller's `invalid_argument`.
 */
export class CollectorError extends Error {
  readonly code: ToolErrorCode;

  constructor(
    message: string,
    readonly connectionError = false,
    readonly status?: number,
    code?: ToolErrorCode,
    readonly problemType?: ProblemDetails["type"],
    /** Every rule a rejected request broke; the first is the argument to change. */
    readonly fields?: ValidationErrorDetail[],
    /** With a 404: the record the Collector did not find. */
    readonly missing?: MissingResource,
  ) {
    super(message);
    this.name = "CollectorError";
    this.code =
      code ??
      (status !== undefined
        ? codeForStatus(status)
        : connectionError
          ? "collector_unreachable"
          : "invalid_argument");
  }
}

export interface CollectorRequestOptions {
  timeoutMs?: number;
  signal?: AbortSignal;
  /** Host-selected access for the verified caller; never a tool input. */
  access?: CollectorAccess;
  /**
   * Contract-named request headers one operation takes, such as `If-Match`.
   * They never replace the credential or project headers.
   */
  headers?: Readonly<Record<string, string>>;
}

/**
 * Validate one whole response body against its generated document, including
 * resources, pages and analysis evidence.
 */
export function parseCollectorBody<Name extends ContractSchemaName>(
  value: unknown,
  context: string,
  schema: Name,
): ContractDefinitions[Name] {
  const checked = checkContract(schema, value);
  if (!checked.valid) {
    throw new CollectorError(`collector contract mismatch for ${context}: ${checked.message}`, false, undefined, "contract_mismatch");
  }
  return checked.value;
}

function parseProblemDetails(value: unknown): ProblemDetails | undefined {
  const checked = checkContract("Common.Errors.ProblemDetails", value);
  return checked.valid ? checked.value : undefined;
}

// A Collector problem names its type by a stable `code`, so the tool error code comes from
// the body first and from the status only for a code this server does not map.
function codeForProblem(problem: ProblemDetails, status: number): ToolErrorCode {
  switch (problem.code) {
    case "not_found":
      return "not_found";
    case "unauthorized":
    case "forbidden":
      return "collector_unauthorized";
    case "conflict":
      return "conflict";
    case "precondition_failed":
      return "precondition_failed";
    case "precondition_required":
      return "precondition_required";
    case "internal_error":
    case "service_unavailable":
      return "collector_error";
    case "validation_failed":
      return "collector_rejected";
    default:
      return codeForStatus(status);
  }
}

/**
 * One rejected argument as the model reads it: its path, the rule and, unless the rule's text
 * already names them, the values it accepts.
 */
function describeRejection(detail: ValidationErrorDetail): string {
  const { allowed, message } = detail;
  if (allowed === undefined || allowed.every((value) => message.includes(value))) {
    return `${detail.field}: ${message}`;
  }
  return `${detail.field}: ${/[.!?]$/u.test(message) ? message : `${message}.`} Accepted: ${allowed.join(", ")}`;
}

/** A 404 names what is missing; the Collector's detail also names the tool that lists what exists. */
function notFoundMessage(problem: NotFoundError): string | undefined {
  if (problem.detail !== undefined && problem.detail.trim() !== "") return problem.detail;
  if (problem.resource_type === undefined && problem.resource_id === undefined) return undefined;
  return `${[problem.resource_type ?? "The record", problem.resource_id].filter(Boolean).join(" ")} was not found in this project.`;
}

function missingResource(problem: NotFoundError): MissingResource | undefined {
  if (problem.resource_type === undefined && problem.resource_id === undefined) return undefined;
  return {
    ...(problem.resource_type === undefined ? {} : { resource_type: problem.resource_type }),
    ...(problem.resource_id === undefined ? {} : { resource_id: problem.resource_id }),
  };
}

/**
 * Call a collector endpoint. All response data remains unknown until the
 * endpoint-specific generated contract parser accepts it.
 */
async function collectorRequest(
  pathname: string,
  method: "POST" | "PATCH" | "DELETE",
  body: unknown,
  options: CollectorRequestOptions,
): Promise<unknown> {
  if (readCollectorProjects() !== undefined && options.access === undefined) {
    throw new CollectorAccessError();
  }
  const outage = collectorOutageSignal();
  if (outage.aborted) throw collectorUnavailable(false);
  const url = new URL(pathname, collectorUrl());
  const timeout = AbortSignal.timeout(options.timeoutMs ?? 10_000);
  const signal = AbortSignal.any(options.signal ? [options.signal, outage, timeout] : [outage, timeout]);
  // Whichever signal ended the request names the failure, whether the response had not begun or
  // its body was still arriving: the caller's cancellation, the Collector's outage, the timeout.
  const aborted = (): Error | undefined => {
    if (options.signal?.aborted) {
      return new CollectorError(`collector request cancelled for ${pathname}`, true, undefined, "cancelled");
    }
    if (outage.aborted) return collectorUnavailable(true);
    if (timeout.aborted) {
      return new CollectorError(`collector timed out for ${pathname}`, true, undefined, "collector_timeout");
    }
    return undefined;
  };
  const headers = {
    accept: "application/json",
    ...options.headers,
    ...collectorHeaders(options.access),
    ...(body === undefined ? {} : { "content-type": "application/json" }),
  };

  let response: Response;
  try {
    response = await fetch(url, {
      method,
      headers,
      signal,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
  } catch {
    throw aborted() ?? new CollectorError(
      `collector unreachable at ${collectorUrl()} — start it with ` +
        "`qyl` or configure QYL_COLLECTOR_URL",
      true,
    );
  }

  // A DELETE answers 204 with no body; the caller's operation decides
  // whether an empty answer is one its contract allows.
  if (response.status === 204) return undefined;

  let responseBody: unknown;
  try {
    responseBody = await response.json();
  } catch {
    throw aborted() ?? new CollectorError(
      `collector returned invalid JSON (${response.status} ${response.statusText}) for ${pathname}`,
      false,
      response.status,
      codeForUnexpectedBody(response),
    );
  }

  if (!response.ok) {
    const mediaType = response.headers.get("content-type")?.split(";", 1)[0].trim().toLowerCase();
    if (mediaType !== "application/problem+json") {
      throw new CollectorError(
        `collector contract mismatch for ${pathname}: expected application/problem+json, got ${mediaType ?? "no content type"}`,
        false,
        response.status,
        codeForUnexpectedBody(response),
      );
    }
    const problem = parseProblemDetails(responseBody);
    if (!problem) {
      throw new CollectorError(
        `collector contract mismatch for ${pathname}: invalid Problem Details body`,
        false,
        response.status,
        codeForUnexpectedBody(response),
      );
    }
    const validation = problem.code === "validation_failed"
      ? checkContract("Common.Errors.ValidationError", responseBody)
      : undefined;
    const fields = validation?.valid && validation.value.errors.length > 0 ? validation.value.errors : undefined;
    const notFound = problem.code === "not_found" ? checkContract("Common.Errors.NotFoundError", responseBody) : undefined;
    const missing = notFound?.valid ? missingResource(notFound.value) : undefined;
    throw new CollectorError(
      redactTelemetryText(
        (fields === undefined ? undefined : fields.map(describeRejection).join("; "))
        ?? (notFound?.valid ? notFoundMessage(notFound.value) : undefined)
        ?? `Collector request failed (HTTP ${response.status}); retry reads after correcting the request or Collector access.`,
      ),
      false,
      response.status,
      codeForProblem(problem, response.status),
      problem.type,
      fields,
      missing,
    );
  }

  return responseBody;
}

export function collectorPost(
  pathname: string,
  body: unknown,
  options: CollectorRequestOptions = {},
): Promise<unknown> {
  return collectorRequest(pathname, "POST", body, options);
}

export function collectorPatch(
  pathname: string,
  body: unknown,
  options: CollectorRequestOptions = {},
): Promise<unknown> {
  return collectorRequest(pathname, "PATCH", body, options);
}

export function collectorDelete(
  pathname: string,
  options: CollectorRequestOptions = {},
): Promise<unknown> {
  return collectorRequest(pathname, "DELETE", undefined, options);
}
