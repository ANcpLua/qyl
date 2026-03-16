#!/usr/bin/env npx tsx
/**
 * CLI tool to export artifacts from the qyl API.
 *
 * Usage:
 *   npx tsx tools/export-artifact.ts <id>
 *   npx tsx tools/export-artifact.ts <id> --url https://qyl.example.com
 *   npx tsx tools/export-artifact.ts <id> --out artifact.md
 *   npx tsx tools/export-artifact.ts <id> --json
 *
 * Environment:
 *   QYL_URL            qyl API base URL
 *   QYL_COLLECTOR_URL  Backward-compatible alias for qyl API base URL
 *   QYL_TOKEN          Auth token (optional, sent as Bearer if set)
 */

import { writeFileSync } from "node:fs";
import { parseArgs } from "node:util";

// ═══════════════════════════════════════════════════════════════════════
// CLI argument parsing
// ═══════════════════════════════════════════════════════════════════════

const { values, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    url: { type: "string", short: "u" },
    out: { type: "string", short: "o" },
    json: { type: "boolean", short: "j", default: false },
    help: { type: "boolean", short: "h", default: false },
  },
});

if (values.help || positionals.length === 0) {
  console.log(`
export-artifact — fetch artifacts from qyl API

Usage:
  npx tsx tools/export-artifact.ts <id> [options]

Options:
  --url, -u <url>    qyl API base URL (QYL_URL or QYL_COLLECTOR_URL for defaulting)
  --out, -o <file>   Write content to file instead of stdout
  --json, -j         Output full JSON response (id, content_type, metadata, content)
  --help, -h         Show this help

Examples:
  npx tsx tools/export-artifact.ts abc123
  npx tsx tools/export-artifact.ts abc123 --out report.md
  npx tsx tools/export-artifact.ts abc123 --json
  QYL_URL=https://qyl.example.com npx tsx tools/export-artifact.ts abc123
`.trim());
  process.exit(values.help ? 0 : 1);
}

// ═══════════════════════════════════════════════════════════════════════
// Configuration
// ═══════════════════════════════════════════════════════════════════════

const artifactId = positionals[0];
const baseUrl =
  values.url ?? process.env.QYL_URL ?? process.env.QYL_COLLECTOR_URL;
const token = process.env.QYL_TOKEN;

if (!baseUrl) {
  console.error(
    "error: missing API base URL. Provide --url or set QYL_URL (or QYL_COLLECTOR_URL).",
  );
  process.exit(1);
}

// ═══════════════════════════════════════════════════════════════════════
// Fetch artifact
// ═══════════════════════════════════════════════════════════════════════

interface ArtifactResponse {
  id: string;
  content_type: string;
  content: string;
  title: string | null;
  source: string | null;
  metadata: Record<string, unknown> | null;
  created_at: string;
  expires_at: string | null;
}

async function fetchArtifact(id: string): Promise<ArtifactResponse> {
  const url = `${baseUrl.replace(/\/$/, "")}/api/v1/artifacts/${encodeURIComponent(id)}`;

  const headers: Record<string, string> = {
    Accept: "application/json",
  };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(url, { headers });

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error(`Artifact '${id}' not found`);
    }
    const body = await response.text().catch(() => "");
    throw new Error(
      `HTTP ${response.status}: ${response.statusText}${body ? ` — ${body}` : ""}`,
    );
  }

  return (await response.json()) as ArtifactResponse;
}

// ═══════════════════════════════════════════════════════════════════════
// Main
// ═══════════════════════════════════════════════════════════════════════

try {
  const artifact = await fetchArtifact(artifactId);

  if (values.json) {
    const output = JSON.stringify(artifact, null, 2);
    if (values.out) {
      writeFileSync(values.out, output + "\n", "utf-8");
      console.error(`Written to ${values.out}`);
    } else {
      console.log(output);
    }
  } else {
    if (values.out) {
      writeFileSync(values.out, artifact.content, "utf-8");
      console.error(`Written to ${values.out} (${artifact.content_type})`);
    } else {
      process.stdout.write(artifact.content);
    }
  }
} catch (err) {
  console.error(`error: ${err instanceof Error ? err.message : err}`);
  process.exit(1);
}
