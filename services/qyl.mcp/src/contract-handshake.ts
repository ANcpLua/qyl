/**
 * What this server knows of its Collector from the Collector's /health: whether it answers, and
 * which contract revision it serves.
 *
 * The Collector advertises the revision of the API contract it serves in its /health body; this
 * server was generated from CONTRACT_REVISION. The two deploy as separate Railway services,
 * minutes apart, so a difference is a state to report, not a reason to refuse to start: a monitor
 * reads the Collector's revision in the background, and while the two differ every tool call
 * answers `collector_contract_mismatch` naming both revisions and /healthz reports `degraded`. The
 * first read that matches again clears it, with no restart.
 *
 * A read the Collector gives no answer at all, refused or timed out, as while Railway restarts the
 * volume-backed Collector during a deploy, reports it unreachable: a Collector call then ends at
 * once with `collector_unavailable` and the seconds after which to repeat it, a call already
 * waiting is abandoned instead of waiting out its own timeout, and the monitor reads again every
 * second until the first answer clears the state. An answer without JSON leaves the state unknown
 * and tool calls proceed to report their own collector error. A JSON health body without a
 * revision comes from a Collector older than the revision field, which is skew.
 */

import { collectorHeaders, collectorUrl } from "./config.js";
import { CONTRACT_REVISION } from "./contracts/generated/api.js";
import { logInfo, logWarning } from "./stderr-log.js";

/** What the last health read proved about the Collector. */
export type CollectorContract =
  | { kind: "unknown" }
  /** `since` is when the first read without an answer ended. */
  | { kind: "unreachable"; since: number }
  | { kind: "match"; revision: string }
  /** `revision` is null when the Collector's health body names no revision. */
  | { kind: "mismatch"; revision: string | null };

let observed: CollectorContract = { kind: "unknown" };
/** Aborted when the Collector stops answering; replaced once it answers again. */
let outage = new AbortController();

export function collectorContract(): CollectorContract {
  return observed;
}

/** A Collector call races this signal: it aborts the moment the monitor finds the Collector unreachable. */
export function collectorOutageSignal(): AbortSignal {
  return outage.signal;
}

/**
 * The revision the Collector's /health body advertises: a string, or `null` for a JSON body
 * without one, or `undefined` for an answer that proves nothing. A read without any answer
 * rejects. Deliberately lenient where the rest of this server validates with the generated
 * contract: it must read health bodies of any revision, older and newer, because telling them
 * apart is its purpose. An unhealthy Collector still advertises its revision, so a 503 body is
 * read like a 200.
 */
export async function readCollectorRevision(timeoutMs = 5_000): Promise<string | null | undefined> {
  const response = await fetch(new URL("/health", collectorUrl()), {
    headers: { accept: "application/json", ...collectorHeaders() },
    signal: AbortSignal.timeout(timeoutMs),
  });
  let body: unknown;
  try {
    body = await response.json();
  } catch {
    return undefined;
  }
  if (typeof body !== "object" || body === null || Array.isArray(body)) return undefined;
  const revision = (body as Record<string, unknown>).contract_revision;
  if (typeof revision === "string" && revision.length > 0) return revision;
  return response.ok ? null : undefined;
}

/** Record one answered health read; a change of state is logged once, with both revisions. */
export function observeCollectorRevision(revision: string | null | undefined, expected = CONTRACT_REVISION): CollectorContract {
  const next: CollectorContract = revision === undefined
    ? { kind: "unknown" }
    : revision === expected ? { kind: "match", revision } : { kind: "mismatch", revision };
  if (observed.kind === "unreachable") {
    outage = new AbortController();
    logInfo(`collector at ${collectorUrl()} answers /health again after ${Math.round((Date.now() - observed.since) / 1_000)} s`);
  }
  if (next.kind === "mismatch" && (observed.kind !== "mismatch" || observed.revision !== next.revision)) {
    logWarning(
      `collector at ${collectorUrl()} serves contract revision ${next.revision ?? "(none)"}, this server ${expected}: ` +
        "tools answer collector_contract_mismatch until both serve one revision",
    );
  } else if (next.kind === "match" && observed.kind !== "match") {
    logInfo(`contract revision ${next.revision} matched at ${collectorUrl()}`);
  }
  observed = next;
  return next;
}

/** Record a health read without any answer: Collector calls end at once, and waiting ones are abandoned. */
export function observeCollectorUnreachable(now = Date.now()): CollectorContract {
  if (observed.kind !== "unreachable") {
    logWarning(`collector at ${collectorUrl()} does not answer /health: Collector calls answer collector_unavailable until it does`);
    observed = { kind: "unreachable", since: now };
    outage.abort();
  }
  return observed;
}

export interface CollectorContractMonitorOptions {
  /** Defaults to reading the configured Collector's /health; a rejection is a read without an answer. */
  read?: () => Promise<string | null | undefined>;
  intervalMs?: number;
  /** The wait before the next read while the Collector does not answer. */
  unreachableIntervalMs?: number;
}

/**
 * Read the Collector's /health now and again after each read, `intervalMs` later or, while it
 * does not answer, `unreachableIntervalMs` later. Reads never overlap, the timer does not hold
 * the process open, and the returned function stops it and forgets the state.
 */
export function startCollectorContractMonitor(options: CollectorContractMonitorOptions = {}): () => void {
  const read = options.read ?? (() => readCollectorRevision());
  let stopped = false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const tick = async (): Promise<void> => {
    let revision: string | null | undefined;
    let answered = true;
    try {
      revision = await read();
    } catch {
      answered = false;
    }
    if (stopped) return;
    if (answered) observeCollectorRevision(revision);
    else observeCollectorUnreachable();
    timer = setTimeout(() => void tick(),
      observed.kind === "unreachable" ? options.unreachableIntervalMs ?? 1_000 : options.intervalMs ?? 10_000);
    timer.unref();
  };
  void tick();
  return () => {
    stopped = true;
    clearTimeout(timer);
    observed = { kind: "unknown" };
    outage = new AbortController();
  };
}

/** A tool call refused while the Collector serves another contract revision. */
export class CollectorContractMismatchError extends Error {
  readonly code = "collector_contract_mismatch";

  constructor(readonly collectorRevision: string | null, readonly serverRevision = CONTRACT_REVISION) {
    super(
      `the Collector serves contract revision ${collectorRevision ?? "(none)"} and this MCP server ${serverRevision}; ` +
        "the call succeeds once both are deployed from one revision",
    );
    this.name = "CollectorContractMismatchError";
  }
}

/** Throws while the last health read proved skew; a match or an unknown state passes. */
export function assertCollectorContract(): void {
  if (observed.kind === "mismatch") throw new CollectorContractMismatchError(observed.revision);
}

/** A Collector call not sent, or abandoned unanswered, while the Collector does not answer its health check. */
export class CollectorUnavailableError extends Error {
  readonly code = "collector_unavailable";

  constructor(
    /** The longer the Collector has not answered, the longer the wait: from 5 to 60 seconds. */
    readonly retryAfterSeconds: number,
    abandoned: boolean,
  ) {
    super(
      `the Collector at ${collectorUrl()} does not answer its health check, as while it restarts or before \`qyl up\` ` +
        `starts it; ${abandoned ? "this call was abandoned unanswered" : "this call was not sent"}`,
    );
    this.name = "CollectorUnavailableError";
  }
}

/** The failure of a Collector call the outage refused (`abandoned` false) or cut short. */
export function collectorUnavailable(abandoned: boolean, now = Date.now()): CollectorUnavailableError {
  const silentSeconds = observed.kind === "unreachable" ? Math.ceil((now - observed.since) / 1_000) : 0;
  return new CollectorUnavailableError(Math.min(60, Math.max(5, silentSeconds)), abandoned);
}
