import {
  RunnerResourceKindValues,
  type RunnerResourceKind,
} from "@ancplua/qyl-api-schema/types";

export type {
  RunnerLogLine as LogLine,
  RunnerResourceLifecycle as ResourceLifecycle,
  RunnerResourceState as ResourceState,
} from "@ancplua/qyl-api-schema/types";

/** Runner kinds whose selected resource exposes the MCP tools panel. */
export const MCP_KINDS: ReadonlySet<RunnerResourceKind> = new Set([
  RunnerResourceKindValues.stdio,
  RunnerResourceKindValues.http,
  RunnerResourceKindValues.inproc,
]);
