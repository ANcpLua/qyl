/**
 * Token resolver for template strings.
 *
 * Resolves `{{path.to.value}}` placeholders against a context object.
 * Supports dot-notation for nested property access and optional pipe
 * filters (uppercase, lowercase, iso, json, trim).
 *
 * @module resolve-tokens
 */

// ═══════════════════════════════════════════════════════════════════════
// Types
// ═══════════════════════════════════════════════════════════════════════

export type TokenContext = Record<string, unknown>;
export type FilterFn = (value: string) => string;

// ═══════════════════════════════════════════════════════════════════════
// Built-in filters (pipe syntax: {{value | filter}})
// ═══════════════════════════════════════════════════════════════════════

const builtinFilters: Record<string, FilterFn> = {
  uppercase: (v) => v.toUpperCase(),
  lowercase: (v) => v.toLowerCase(),
  trim: (v) => v.trim(),
  json: (v) => {
    try {
      return JSON.stringify(JSON.parse(v), null, 2);
    } catch {
      return v;
    }
  },
  iso: (v) => {
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? v : d.toISOString();
  },
};

// ═══════════════════════════════════════════════════════════════════════
// Token pattern:  {{  path.to.value  |  filter  }}
//   - path segments separated by dots
//   - optional pipe filter (built-in or custom)
//   - whitespace around path and filter is trimmed
// ═══════════════════════════════════════════════════════════════════════

const TOKEN_RE = /\{\{([^}]+)\}\}/g;

// ═══════════════════════════════════════════════════════════════════════
// Core API
// ═══════════════════════════════════════════════════════════════════════

export interface ResolveOptions {
  /** Additional filters merged with built-ins (custom wins on collision). */
  filters?: Record<string, FilterFn>;
  /** When true, unresolved tokens are replaced with empty string. Default: keep as-is. */
  stripUnresolved?: boolean;
}

/**
 * Resolve all `{{…}}` tokens in `template` using values from `context`.
 *
 * @example
 * ```ts
 * resolveTokens("Hello {{user.name}}!", { user: { name: "Ada" } });
 * // => "Hello Ada!"
 * ```
 */
export function resolveTokens(
  template: string,
  context: TokenContext,
  options: ResolveOptions = {},
): string {
  const filters = { ...builtinFilters, ...options.filters };

  return template.replace(TOKEN_RE, (match, raw: string) => {
    const [pathPart, ...filterParts] = raw.split("|").map((s) => s.trim());
    const segments = pathPart.split(".");

    let value: unknown = context;
    for (const seg of segments) {
      if (value == null || typeof value !== "object") {
        return options.stripUnresolved ? "" : match;
      }
      value = (value as Record<string, unknown>)[seg];
    }

    if (value === undefined) {
      return options.stripUnresolved ? "" : match;
    }

    let str =
      value === null
        ? ""
        : typeof value === "object"
          ? JSON.stringify(value)
          : String(value);

    for (const filterName of filterParts) {
      const fn = filters[filterName];
      if (fn) str = fn(str);
    }

    return str;
  });
}

/**
 * Extract all unique token paths from a template string.
 *
 * @example
 * ```ts
 * extractTokens("{{a.b}} and {{c | uppercase}}");
 * // => ["a.b", "c"]
 * ```
 */
export function extractTokens(template: string): string[] {
  const tokens = new Set<string>();
  for (const m of template.matchAll(TOKEN_RE)) {
    const path = m[1].split("|")[0].trim();
    tokens.add(path);
  }
  return [...tokens];
}

/**
 * Check whether all tokens in `template` can be resolved from `context`.
 */
export function allTokensResolvable(
  template: string,
  context: TokenContext,
): boolean {
  const paths = extractTokens(template);
  for (const path of paths) {
    let value: unknown = context;
    for (const seg of path.split(".")) {
      if (value == null || typeof value !== "object") return false;
      value = (value as Record<string, unknown>)[seg];
    }
    if (value === undefined) return false;
  }
  return true;
}

// ═══════════════════════════════════════════════════════════════════════
// Inline tests — `node --test src/lib/resolve-tokens.ts` (via tsx)
//                or `npx tsx --test src/lib/resolve-tokens.ts`
// ═══════════════════════════════════════════════════════════════════════

import { describe, it } from "node:test";
import assert from "node:assert/strict";

describe("resolveTokens", () => {
  it("resolves simple tokens", () => {
    assert.equal(
      resolveTokens("Hello {{name}}!", { name: "World" }),
      "Hello World!",
    );
  });

  it("resolves nested dot-notation", () => {
    const ctx = { user: { profile: { name: "Ada" } } };
    assert.equal(resolveTokens("Hi {{user.profile.name}}", ctx), "Hi Ada");
  });

  it("keeps unresolved tokens by default", () => {
    assert.equal(resolveTokens("{{missing}}", {}), "{{missing}}");
  });

  it("strips unresolved tokens when configured", () => {
    assert.equal(
      resolveTokens("a{{missing}}b", {}, { stripUnresolved: true }),
      "ab",
    );
  });

  it("handles null values as empty string", () => {
    assert.equal(resolveTokens("v={{val}}", { val: null }), "v=");
  });

  it("serializes object values as JSON", () => {
    assert.equal(
      resolveTokens("{{data}}", { data: { a: 1 } }),
      '{"a":1}',
    );
  });

  it("handles numeric values", () => {
    assert.equal(resolveTokens("count={{n}}", { n: 42 }), "count=42");
  });

  it("handles boolean values", () => {
    assert.equal(resolveTokens("ok={{ok}}", { ok: true }), "ok=true");
  });

  it("resolves multiple tokens in one string", () => {
    assert.equal(
      resolveTokens("{{a}}-{{b}}", { a: "x", b: "y" }),
      "x-y",
    );
  });

  it("handles empty template", () => {
    assert.equal(resolveTokens("", { a: 1 }), "");
  });

  it("handles template with no tokens", () => {
    assert.equal(resolveTokens("plain text", {}), "plain text");
  });

  it("stops at null in path", () => {
    assert.equal(
      resolveTokens("{{a.b.c}}", { a: null }),
      "{{a.b.c}}",
    );
  });

  it("stops at primitive in path", () => {
    assert.equal(
      resolveTokens("{{a.b}}", { a: "string" }),
      "{{a.b}}",
    );
  });
});

describe("filters", () => {
  it("applies uppercase filter", () => {
    assert.equal(
      resolveTokens("{{name | uppercase}}", { name: "ada" }),
      "ADA",
    );
  });

  it("applies lowercase filter", () => {
    assert.equal(
      resolveTokens("{{name | lowercase}}", { name: "ADA" }),
      "ada",
    );
  });

  it("applies trim filter", () => {
    assert.equal(
      resolveTokens("{{s | trim}}", { s: "  hi  " }),
      "hi",
    );
  });

  it("chains multiple filters", () => {
    assert.equal(
      resolveTokens("{{s | trim | uppercase}}", { s: "  hi  " }),
      "HI",
    );
  });

  it("applies custom filter", () => {
    const filters = { reverse: (v: string) => [...v].reverse().join("") };
    assert.equal(
      resolveTokens("{{w | reverse}}", { w: "abc" }, { filters }),
      "cba",
    );
  });

  it("ignores unknown filters", () => {
    assert.equal(
      resolveTokens("{{v | nope}}", { v: "ok" }),
      "ok",
    );
  });

  it("applies iso filter to date string", () => {
    const result = resolveTokens("{{d | iso}}", {
      d: "2026-03-15T10:00:00Z",
    });
    assert.equal(result, "2026-03-15T10:00:00.000Z");
  });

  it("iso filter preserves non-date strings", () => {
    assert.equal(resolveTokens("{{d | iso}}", { d: "not-a-date" }), "not-a-date");
  });
});

describe("extractTokens", () => {
  it("extracts unique paths", () => {
    const tokens = extractTokens("{{a.b}} {{c}} {{a.b}}");
    assert.deepEqual(tokens, ["a.b", "c"]);
  });

  it("strips filter from extracted path", () => {
    assert.deepEqual(
      extractTokens("{{name | uppercase}}"),
      ["name"],
    );
  });

  it("returns empty array for no tokens", () => {
    assert.deepEqual(extractTokens("no tokens"), []);
  });
});

describe("allTokensResolvable", () => {
  it("returns true when all tokens exist", () => {
    assert.equal(
      allTokensResolvable("{{a}} {{b.c}}", { a: 1, b: { c: 2 } }),
      true,
    );
  });

  it("returns false when a token is missing", () => {
    assert.equal(allTokensResolvable("{{a}} {{b}}", { a: 1 }), false);
  });

  it("returns false when nested path is missing", () => {
    assert.equal(allTokensResolvable("{{a.b.c}}", { a: { b: {} } }), false);
  });

  it("returns true for empty template", () => {
    assert.equal(allTokensResolvable("", {}), true);
  });
});
