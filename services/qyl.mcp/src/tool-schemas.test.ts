import assert from "node:assert/strict";
import { isDeepStrictEqual } from "node:util";
import test from "node:test";
import documents from "./contracts/generated/contract-schemas.json" with { type: "json" };
import { connectModernTestClient } from "./modern-test-client.test-helper.js";
import { createServer } from "./server.js";

type Node = Record<string, unknown>;
const generated = documents as unknown as Record<string, Node & { $defs?: Record<string, Node> }>;

// JSON Schema format assertions; OpenAPI's numeric formats become bounds at generation.
const formats = new Set(["date-time", "date", "time", "duration", "uri", "uri-reference", "email", "uuid", "hostname", "ipv4", "ipv6"]);
const dataKeywords = new Set(["enum", "const", "default", "examples", "required"]);

function* schemaNodes(node: unknown, path: string): Generator<[string, Node]> {
  if (Array.isArray(node)) {
    for (const [index, item] of node.entries()) yield* schemaNodes(item, `${path}[${index}]`);
    return;
  }
  if (node === null || typeof node !== "object") return;
  yield [path, node as Node];
  for (const [key, value] of Object.entries(node)) {
    if (!dataKeywords.has(key)) yield* schemaNodes(value, `${path}.${key}`);
  }
}

function references(node: unknown): Set<string> {
  const found = new Set<string>();
  for (const [, schema] of schemaNodes(node, "")) {
    if (typeof schema.$ref === "string") found.add(schema.$ref.slice("#/$defs/".length));
  }
  return found;
}

test("generated documents are plain, inlined JSON Schema every host can read", () => {
  for (const [name, document] of Object.entries(generated)) {
    assert.equal(document.type, "object", `${name} has an object root`);
    assert.equal(document.$schema, undefined, `${name} declares no dialect; MCP reads 2020-12`);
    const definitions = document.$defs ?? {};
    for (const [path, schema] of schemaNodes(document, name)) {
      for (const keyword of ["unevaluatedProperties", "discriminator", "allOf", "nullable"]) {
        assert.equal(schema[keyword], undefined, `${path} carries ${keyword}`);
      }
      assert(!Array.isArray(schema.type), `${path} spells a type array`);
      if (schema.additionalProperties !== undefined) {
        assert(typeof schema.additionalProperties === "boolean" || Object.keys(schema.additionalProperties as Node).length > 0,
          `${path} spells unconstrained additional properties as {}`);
      }
      if (schema.format !== undefined) assert(formats.has(schema.format as string), `${path} format ${JSON.stringify(schema.format)}`);
      if (typeof schema.$ref === "string") {
        assert.match(schema.$ref, /^#\/\$defs\//u, `${path} references outside the document`);
        assert(Object.hasOwn(definitions, schema.$ref.slice("#/$defs/".length)), `${path} references a missing definition`);
      }
    }
    // Only recursion stays referenced: every kept definition reaches itself again.
    for (const definition of Object.keys(definitions)) {
      const seen = new Set<string>();
      const pending = [...references(definitions[definition])];
      while (pending.length > 0) {
        const next = pending.pop()!;
        if (seen.has(next)) continue;
        seen.add(next);
        pending.push(...references(definitions[next]));
      }
      assert(seen.has(definition), `${name} keeps non-recursive ${definition} behind a $ref`);
    }
  }
});

test("tools/list advertises the generated input documents unchanged, and every input document is a tool's", async () => {
  const connection = await connectModernTestClient(
    { name: "tool-schemas-test", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  try {
    const { tools } = await connection.client.listTools();
    assert(tools.length > 0);
    const published = Object.values(generated);
    for (const tool of tools) {
      assert(published.some((document) => isDeepStrictEqual(document, tool.inputSchema)), `${tool.name} input`);
    }
    // A group of arguments the tools spread is an alias in the contract, never a document of its own.
    for (const [name, document] of Object.entries(generated).filter(([key]) => /^Mcp\.Tools\.\w+Input$/u.test(key))) {
      assert(tools.some((tool) => isDeepStrictEqual(document, tool.inputSchema)), `${name} is no tool's input`);
    }
  } finally {
    await connection.close();
  }
});
