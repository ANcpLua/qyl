/**
 * Collector calls for the objects users create in qyl: annotations (notes on
 * a trace, span, session or failure) and saved queries (named filters).
 *
 * They live in the caller's Collector project like telemetry does, and every
 * call reaches it with the options `runTool` resolved from the verified
 * subject; no argument names a project or a credential. Nothing here changes
 * telemetry: an annotation points at a trace, span, session or failure and
 * leaves it as recorded, and the one delete operation removes only these
 * objects.
 */

import type {
  Annotation,
  AnnotateInput,
  DeletableKind,
  SaveQueryInput,
  SavedQuery,
  ValidationErrorDetail,
} from "./contracts/generated/api.js";
import {
  CollectorError,
  type CollectorRequestOptions,
  collectorDelete,
  collectorPatch,
  collectorPost,
  parseCollectorBody,
} from "./collector.js";
import { createHash } from "node:crypto";
import { IDEMPOTENCY_KEY_HEADER, IF_MATCH_HEADER } from "./contracts/generated/headers.js";
import { redactTelemetry } from "./telemetry-redaction.js";

const ANNOTATIONS_PATH = "/api/v1/annotations";
const SAVED_QUERIES_PATH = "/api/v1/saved-queries";

/** Where each deletable kind lives on the Collector API. */
const DELETE_PATHS: Readonly<Record<DeletableKind, string>> = {
  annotation: ANNOTATIONS_PATH,
  saved_query: SAVED_QUERIES_PATH,
};

/**
 * The If-Match value for a change. An etag is opaque, but it travels as an
 * HTTP field value, so one that could not be sent is refused here, naming the
 * argument like every other rejected one, instead of failing inside fetch as
 * if the Collector were unreachable.
 */
export function ifMatch(etag: string): Readonly<Record<string, string>> {
  if (!/^[\x21-\x7e](?:[\x20-\x7e]*[\x21-\x7e])?$/u.test(etag)) {
    throw invalid([{ field: "etag", message: "is not a valid HTTP field value; pass the etag a qyl tool returned", code: "etag.invalid" }]);
  }
  return { [IF_MATCH_HEADER]: etag };
}

/** Retry keys deduplicate for 24 hours; identical bodies get a deterministic default key. */
function idempotencyKey(options: CollectorRequestOptions, operation: string, body: object, suppliedKey?: string): Readonly<Record<string, string>> {
  const canonical = (value: unknown): unknown => Array.isArray(value) ? value.map(canonical) : value !== null && typeof value === "object" ? Object.fromEntries(Object.entries(value).sort(([a], [b]) => a.localeCompare(b)).map(([k, v]) => [k, canonical(v)])) : value;
  const key = suppliedKey ?? JSON.stringify(canonical(body));
  const digest = createHash("sha256")
    .update(`${options.access?.project ?? ""}\0${operation}\0${key}`)
    .digest("hex");
  return { [IDEMPOTENCY_KEY_HEADER]: digest };
}

function resourcePath(base: string, id: string, suffix = ""): string {
  return `${base}/${encodeURIComponent(id)}${suffix}`;
}

/**
 * Runs one Collector call and names its refusals for what the caller tried:
 * a status listed in `messages` becomes that message, keeping the code the
 * status decides (`not_found`, `conflict`, `precondition_failed`,
 * `collector_unauthorized`) and the record a 404 names as missing. A body
 * that breaks the contract stays a contract mismatch, and every other
 * failure passes through unchanged.
 */
export async function refusals<T>(
  messages: Readonly<Partial<Record<number, string>>>,
  request: () => Promise<T>,
): Promise<T> {
  try {
    return await request();
  } catch (error) {
    if (!(error instanceof CollectorError) || error.status === undefined || error.code === "contract_mismatch") {
      throw error;
    }
    const message = messages[error.status];
    if (message === undefined) throw error;
    throw new CollectorError(message, false, error.status, undefined, undefined, undefined, error.missing);
  }
}

/** The refusals every change to an existing object shares. */
export function changeRefusals(kind: string, id: string, role: "write" | "delete"): Partial<Record<number, string>> {
  return {
    403: `the Collector refused the change: the server's Collector key needs the ${role} role`,
    404: `${kind} not found: ${id}`,
    412: `${kind} ${id} changed since that etag was read; read it again for its current etag`,
  };
}

/** An argument the record's create or change does not take, refused before any Collector request. */
function invalid(details: readonly ValidationErrorDetail[]): CollectorError {
  return new CollectorError(details.map((detail) => `${detail.field}: ${detail.message}`).join("; "), false, 400, "invalid_argument",
    undefined, [...details]);
}

/** Without an id a create takes these; with one a change refuses them. */
function checkUpsert(record: string, input: { id?: string; etag?: string; idempotency_key?: string }, required: Readonly<Record<string, unknown>>): void {
  if (input.id === undefined) {
    const missing = Object.entries(required).filter(([, value]) => value === undefined).map(([field]) => ({
      field, message: `is required to create ${record}; pass id and etag to change one`, code: `${field}.required`,
    }));
    if (input.etag !== undefined) missing.push({ field: "etag", message: "applies only with id, to change an existing record", code: "etag.requires_id" });
    if (missing.length > 0) throw invalid(missing);
  } else {
    const refused = [
      ...(input.etag === undefined ? [{ field: "etag", message: `is required with id; read ${record} again for its current etag`, code: "etag.required" }] : []),
      ...(input.idempotency_key === undefined ? [] : [{ field: "idempotency_key", message: "applies only to a create, without id", code: "idempotency_key.requires_create" }]),
    ];
    if (refused.length > 0) throw invalid(refused);
  }
}

/** annotate: a new note without `id`, a change of the note `id` names with its current `etag`. */
export async function annotate(
  input: AnnotateInput,
  options: CollectorRequestOptions,
): Promise<{ annotation: Annotation; created: boolean }> {
  checkUpsert("an annotation", input, { target: input.target, text: input.text });
  const labels = input.labels === undefined ? {} : { labels: input.labels };
  const classification = input.classification === undefined ? {} : { classification: input.classification };
  if (input.id === undefined) {
    // The create's 404 is its target: nothing by that id is recorded in this
    // project, so there is nothing to attach the note to.
    const target = input.target!;
    const create = { target, text: input.text!, ...labels, ...classification };
    const body = await refusals(
      {
        403: "the Collector refused the change: the server's Collector key needs the write role",
        404: `${describeTarget(target)} is not recorded in this project`,
      },
      () => collectorPost(ANNOTATIONS_PATH, create, {
        ...options,
        headers: { ...options.headers, ...idempotencyKey(options, ANNOTATIONS_PATH, create, input.idempotency_key) },
      }),
    );
    return { annotation: redactTelemetry(parseCollectorBody(body, `POST ${ANNOTATIONS_PATH}`, "Annotations.Annotation")), created: true };
  }
  if (input.target !== undefined) throw invalid([{ field: "target", message: "is fixed at creation; create another annotation for another target", code: "target.immutable" }]);
  const changes = { ...(input.text === undefined ? {} : { text: input.text }), ...labels, ...classification };
  if (Object.keys(changes).length === 0) {
    throw invalid([{ field: "text", message: "the change names nothing to change: pass text, labels or classification", code: "annotation.unchanged" }]);
  }
  const path = resourcePath(ANNOTATIONS_PATH, input.id);
  const body = await refusals(changeRefusals("annotation", input.id, "write"), () =>
    collectorPatch(path, changes, { ...options, headers: ifMatch(input.etag!) }));
  return { annotation: redactTelemetry(parseCollectorBody(body, `PATCH ${path}`, "Annotations.Annotation")), created: false };
}

/** save_query: a new saved query without `id`, a change of the one `id` names with its current `etag`. */
export async function saveQuery(
  input: SaveQueryInput,
  options: CollectorRequestOptions,
): Promise<{ savedQuery: SavedQuery; created: boolean }> {
  checkUpsert("a saved query", input, { name: input.name, filter: input.filter });
  const description = input.description === undefined ? {} : { description: input.description };
  if (input.id === undefined) {
    const create = { name: input.name!, ...description, filter: input.filter! };
    const body = await refusals(
      {
        403: "the Collector refused the change: the server's Collector key needs the write role",
        409: `a saved query named "${create.name}" already exists in this project`,
      },
      () => collectorPost(SAVED_QUERIES_PATH, create, {
        ...options,
        headers: { ...options.headers, ...idempotencyKey(options, SAVED_QUERIES_PATH, create, input.idempotency_key) },
      }),
    );
    return { savedQuery: redactTelemetry(parseCollectorBody(body, `POST ${SAVED_QUERIES_PATH}`, "SavedQueries.SavedQuery")), created: true };
  }
  const changes = { ...(input.name === undefined ? {} : { name: input.name }), ...description, ...(input.filter === undefined ? {} : { filter: input.filter }) };
  if (Object.keys(changes).length === 0) {
    throw invalid([{ field: "name", message: "the change names nothing to change: pass a name, description or filter", code: "saved_query.unchanged" }]);
  }
  const path = resourcePath(SAVED_QUERIES_PATH, input.id);
  const body = await refusals(
    {
      ...changeRefusals("saved query", input.id, "write"),
      409: `a saved query named "${changes.name ?? ""}" already exists in this project`,
    },
    () => collectorPatch(path, changes, { ...options, headers: ifMatch(input.etag!) }),
  );
  return { savedQuery: redactTelemetry(parseCollectorBody(body, `PATCH ${path}`, "SavedQueries.SavedQuery")), created: false };
}

export async function deleteResource(
  kind: DeletableKind,
  id: string,
  etag: string,
  options: CollectorRequestOptions,
): Promise<void> {
  const path = resourcePath(DELETE_PATHS[kind], id);
  const body = await refusals(
    changeRefusals(kind === "annotation" ? "annotation" : "saved query", id, "delete"),
    () => collectorDelete(path, { ...options, headers: ifMatch(etag) }),
  );
  if (body !== undefined) {
    throw new CollectorError(
      `collector contract mismatch for DELETE ${path}: expected 204 No Content`,
      false,
      undefined,
      "contract_mismatch",
    );
  }
}

// -----------------------------------------------------------------------------
// Read-back: get_trace, list_sessions and get_failure_context return the
// annotations on what they read, so there is no annotation listing tool.
// -----------------------------------------------------------------------------

/** The telemetry object an annotation names, in full. */
export function describeTarget(target: Annotation["target"]): string {
  switch (target.kind) {
    case "trace":
      return `trace ${target.trace_id}`;
    case "span":
      return `span ${target.span_id} of trace ${target.trace_id}`;
    case "session":
      return `session ${target.session_id}`;
    case "failure":
      return `failure ${target.failure_id}`;
  }
}

/** The read that shows an annotation on `target` again, as the next call a headline names. */
export function annotationReadBack(target: Annotation["target"]): string {
  switch (target.kind) {
    case "trace":
    case "span":
      return `get_trace trace_id=${target.trace_id}`;
    case "session":
      return "list_sessions";
    case "failure":
      return `get_failure_context failure_id=${target.failure_id}`;
  }
}
