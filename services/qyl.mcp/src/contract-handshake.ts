/**
 * Contract-revision skew between this server and its Collector.
 *
 * The Collector advertises the revision of the API contract it serves in its /health body; this
 * server was generated from CONTRACT_REVISION. The two deploy as separate Railway services,
 * minutes apart, so a difference is a state to report, not a reason to refuse to start: a monitor
 * reads the Collector's revision in the background, and while the two differ every tool call
 * answers `collector_contract_mismatch` naming both revisions and /healthz reports `degraded`. The
 * first read that matches again clears it, with no restart.
 *
 * Only evidence counts: a Collector that does not answer, or answers without JSON, leaves the
 * state unknown and tool calls proceed to report their own collector error. A JSON health body
 * without a revision comes from a Collector older than the revision field, which is skew.
 */

import { collectorHeaders, collectorUrl } from "./config.js";
import { CONTRACT_REVISION } from "./contracts/generated/api.js";
import { logInfo, logWarning } from "./stderr-log.js";

/** What the last health read proved about the Collector's contract. */
export type CollectorContract =
  | { kind: "unknown" }
  | { kind: "match"; revision: string }
  /** `revision` is null when the Collector's health body names no revision. */
  | { kind: "mismatch"; revision: string | null };

let observed: CollectorContract = { kind: "unknown" };

export function collectorContract(): CollectorContract {
  return observed;
}

/**
 * The revision the Collector's /health body advertises: a string, `null` for a JSON body without
 * one, or `undefined` when the read proved nothing. Deliberately lenient where the rest of this
 * server validates with the generated contract: it must read health bodies of any revision,
 * older and newer, because telling them apart is its purpose. An unhealthy Collector still
 * advertises its revision, so a 503 body is read like a 200.
 */
export async function readCollectorRevision(): Promise<string | null | undefined> {
  let response: Response;
  try {
    response = await fetch(new URL("/health", collectorUrl()), {
      headers: { accept: "application/json", ...collectorHeaders() },
      signal: AbortSignal.timeout(5_000),
    });
  } catch {
    return undefined;
  }
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

/** Record one health read; a change of state is logged once, with both revisions. */
export function observeCollectorRevision(revision: string | null | undefined, expected = CONTRACT_REVISION): CollectorContract {
  const next: CollectorContract = revision === undefined
    ? { kind: "unknown" }
    : revision === expected ? { kind: "match", revision } : { kind: "mismatch", revision };
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

export interface CollectorContractMonitorOptions {
  /** Defaults to reading the configured Collector's /health. */
  read?: () => Promise<string | null | undefined>;
  intervalMs?: number;
}

/**
 * Read the Collector's revision now and every `intervalMs` after. The timer does not hold the
 * process open; the returned function stops it and forgets the state.
 */
export function startCollectorContractMonitor(options: CollectorContractMonitorOptions = {}): () => void {
  const read = options.read ?? readCollectorRevision;
  let reading = false;
  const tick = async (): Promise<void> => {
    if (reading) return;
    reading = true;
    try {
      observeCollectorRevision(await read());
    } finally {
      reading = false;
    }
  };
  void tick();
  const timer = setInterval(() => void tick(), options.intervalMs ?? 10_000);
  timer.unref();
  return () => {
    clearInterval(timer);
    observed = { kind: "unknown" };
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
