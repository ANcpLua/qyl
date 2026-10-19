import type { CallToolResult } from "@modelcontextprotocol/server";
import { readCollectorProjects } from "./collector-access.js";
import { isCredentialKey, SecretRedactor } from "./secret-redactor.js";

// The standalone server and the runner share this implementation through the
// server package export. Keep one process-local instance so configured secret
// values remain available for every result path without ever being serialized.
const redactor = new SecretRedactor({ environment: process.env });

function registerCurrentEnvironmentSecrets(): void {
  redactor.registerSecretValues(
    Object.entries(process.env)
      .filter(([name, value]) => value !== undefined && isCredentialKey(name))
      .map(([, value]) => value as string),
  );
  // Project credentials live inside JSON rather than individual *_KEY variables.
  // Invalid configuration is rejected at startup/request authorization; reporting
  // that failure must not itself throw from the final redaction guard.
  try {
    const projects = readCollectorProjects();
    if (projects !== undefined) {
      redactor.registerSecretValues([...projects.values()].map((access) => access.apiKey));
    }
  } catch {
    // Keep existing secret registrations when the host configuration is invalid.
  }
}

/** Redact already-validated telemetry while preserving its published shape. */
export function redactTelemetry<T>(value: T): T {
  registerCurrentEnvironmentSecrets();
  return redactor.redact(value) as T;
}

export function redactTelemetryText(value: string): string {
  registerCurrentEnvironmentSecrets();
  return redactor.redactText(value);
}

/** Longest headline; the JSON beside it carries everything the headline leaves out. */
const HEADLINE_CHARACTERS = 1_000;

/** One redacted line: hosts that show only the first text block still see the whole headline. */
export function headlineText(text: string): string {
  const line = redactTelemetryText(text).replace(/\s+/gu, " ").trim();
  return line.length <= HEADLINE_CHARACTERS ? line : `${line.slice(0, HEADLINE_CHARACTERS - 1)}…`;
}

/**
 * Final MCP result guard for both model text and structured telemetry. `content[0]`
 * is the headline, `content[1]` the compact JSON of the whole result for hosts that
 * read text only, and `structuredContent` the same object.
 */
export function telemetryToolResult<T extends object>(
  headline: string,
  output: T,
): CallToolResult {
  const safeOutput = redactTelemetry(output) as unknown as Record<string, unknown>;
  return {
    content: [
      { type: "text", text: headlineText(headline) },
      { type: "text", text: JSON.stringify(safeOutput) },
    ],
    structuredContent: safeOutput,
  };
}
