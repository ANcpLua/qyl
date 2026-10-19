/**
 * Tool-manifest snapshot (architecture gate G10b).
 *
 * The manifest is what an agent actually sees: names, titles, descriptions,
 * annotations, tool metadata, the input JSON Schemas the SDK derives from the
 * generated contract schemas, and the skill:// resources the server
 * serves. Pinning it means a contract change that is not regenerated here
 * fails as a diff instead of reaching clients unnoticed, and the recorded
 * contract revision ties the snapshot to the contract it came from rather
 * than to the day someone last looked at it.
 *
 * This test only compares. Regenerate deliberately with
 * `bun run snapshot:tools` — a plain script run, so no ambient environment
 * variable can turn the assertion into a self-approving rewrite.
 */

import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { connectModernTestClient } from "./modern-test-client.test-helper.js";
import { createServer } from "./server.js";
import { buildToolManifest, snapshotUrl } from "./tool-manifest.test-helper.js";

test("the published tool manifest matches its committed snapshot", async () => {
  const manifest = await buildToolManifest();

  const committed = await readFile(snapshotUrl, "utf8").catch(() => undefined);
  assert.ok(
    committed !== undefined,
    "tool-manifest.snapshot.json is missing — regenerate it with `bun run snapshot:tools`",
  );
  assert.match(
    committed,
    /"contract_revision": "sha256:[a-f0-9]{16}"/u,
    "the committed snapshot pins no real contract revision — G10b would no longer tie " +
      "the tool surface to a contract",
  );
  assert.equal(
    manifest,
    committed,
    "the tool manifest changed: review the diff, then regenerate with `bun run snapshot:tools`",
  );
});

test("no tool advertises an outputSchema, so no client validates an error result against one", async () => {
  // A strict client checks structuredContent against a tool's outputSchema even on an isError
  // result and shows its own validation error instead of the tool's. The results carry the
  // same object as JSON text, so dropping the schema loses no information.
  const connection = await connectModernTestClient(
    { name: "tool-manifest-output-schema", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  try {
    const { tools } = await connection.client.listTools();
    assert(tools.length > 0);
    for (const tool of tools) assert.equal(tool.outputSchema, undefined, tool.name);
  } finally {
    await connection.close();
  }
});

test("every tool input schema is an object at its root", async () => {
  const { tools } = JSON.parse(await buildToolManifest()) as {
    tools: { name: string; inputSchema: { type?: unknown } }[];
  };
  assert.ok(tools.length > 0);
  for (const tool of tools) {
    // MCP requires `inputSchema.type: "object"`; hosts validate it on tools/list.
    assert.equal(tool.inputSchema.type, "object", tool.name);
  }
});
