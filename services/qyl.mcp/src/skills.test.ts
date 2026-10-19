import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import test from "node:test";
import { Client, StreamableHTTPClientTransport } from "@modelcontextprotocol/client";
import { createMcpHandler } from "@modelcontextprotocol/server";
import * as z from "zod/v4";
import { connectModernTestClient } from "./modern-test-client.test-helper.js";
import { createServer } from "./server.js";
import { parseFrontmatter } from "./skills.js";

const SKILL_URI = "skill://qyl/qyl-investigate/SKILL.md";

const CatalogEntrySchema = z.object({
  uri: z.string(),
  frontmatter: z.looseObject({ name: z.string(), description: z.string() }),
  resources: z.array(z.object({ uri: z.string(), digest: z.string(), size: z.number().int().nonnegative() })),
});
const CatalogSchema = z.looseObject({
  skills: z.array(CatalogEntrySchema),
  nextCursor: z.string().optional(),
  ttlMs: z.number().optional(),
  cacheScope: z.string().optional(),
});

test("skills/list, skills/get and resources/read publish the bundled skill with matching digests", async () => {
  const connection = await connectModernTestClient(
    { name: "skills-test", version: "1.0.0" },
    () => createServer({ nativeExecution: false }),
  );
  try {
    const { client } = connection;
    const listed = await client.request({ method: "skills/list", params: {} }, CatalogSchema);
    assert.equal(listed.nextCursor, undefined);
    // SEP-2549 on 2026-07-28: the list result carries the base protocol's cache fields.
    assert.equal(listed.ttlMs, 60_000);
    assert.equal(listed.cacheScope, "public");
    assert.deepEqual(listed.skills.map((skill) => skill.uri), [SKILL_URI]);
    const [skill] = listed.skills;
    assert.equal(skill.frontmatter.name, "qyl-investigate");
    assert.ok(skill.frontmatter.description.includes("qyl"));
    assert.ok(skill.resources.some((resource) => resource.uri === SKILL_URI));

    for (const resource of skill.resources) {
      const read = await client.readResource({ uri: resource.uri });
      assert.equal(read.contents.length, 1, resource.uri);
      const content = read.contents[0];
      assert.equal(content?.uri, resource.uri);
      const text = content !== undefined && "text" in content ? content.text : undefined;
      assert.ok(typeof text === "string", resource.uri);
      const digest = `sha256:${createHash("sha256").update(text, "utf8").digest("hex")}`;
      assert.equal(digest, resource.digest, resource.uri);
      assert.equal(resource.size, Buffer.byteLength(text, "utf8"), resource.uri);
      assert.match(resource.digest, /^sha256:[a-f0-9]{64}$/u);
    }

    const fetched = await client.request(
      { method: "skills/get", params: { uri: SKILL_URI } },
      z.looseObject({ skill: CatalogEntrySchema }),
    );
    assert.deepEqual(fetched.skill, skill);

    await assert.rejects(
      client.request({ method: "skills/get", params: { uri: "skill://qyl/missing/SKILL.md" } }, z.looseObject({})),
      /unknown skill/u,
    );
  } finally {
    await connection.close();
  }
});

test("front matter parses quoted and bare values and rejects a missing name", () => {
  assert.deepEqual(
    parseFrontmatter("---\nname: demo\ndescription: \"Does a thing.\"\n---\n# Body\n", "SKILL.md"),
    { name: "demo", description: "Does a thing." },
  );
  assert.throws(() => parseFrontmatter("---\ndescription: x\n---\n", "SKILL.md"), /needs name and description/u);
  assert.throws(() => parseFrontmatter("# no front matter\n", "SKILL.md"), /no YAML front matter/u);
});

test("a 2025-era skills/list carries no cache fields; they are defined from 2026-07-28 on", async (context) => {
  const handler = createMcpHandler(() => createServer({ nativeExecution: false }));
  context.after(() => handler.close());
  const client = new Client({ name: "skills-legacy", version: "1.0.0" });
  context.after(() => client.close().catch(() => undefined));
  await client.connect(new StreamableHTTPClientTransport(new URL("http://qyl-legacy-test.invalid/mcp"), {
    fetch: (url, init) => handler.fetch(new Request(url, init)),
  }));
  assert.equal(client.getProtocolEra(), "legacy");
  const listed = await client.request({ method: "skills/list", params: {} }, CatalogSchema);
  assert.deepEqual(listed.skills.map((skill) => skill.uri), [SKILL_URI]);
  assert.equal(listed.ttlMs, undefined);
  assert.equal(listed.cacheScope, undefined);
});
