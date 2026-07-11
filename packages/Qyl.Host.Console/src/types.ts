// Mirrors QylResourceState served by the runner's read-only /runner API (camelCase, string enums).
// This is a runner-internal dev-only projection, deliberately NOT sourced from qyl-api-schema.
export type ResourceLifecycle =
  | "Pending" | "Starting" | "Ready" | "Stopping" | "Stopped" | "Failed";

export interface ResourceState {
  name: string;
  lifecycle: ResourceLifecycle;
  timestamp: string;
  kind: string | null;
  allocatedPort: number | null;
  endpoint: string | null;
  lastError: string | null;
}

/** Kinds served by Qyl.Host.Mcp — the console shows a tools panel for these. */
export const MCP_KINDS = new Set(["stdio", "http", "inproc"]);

export interface LogLine {
  resource: string;
  stream: "out" | "err";
  line: string;
}
