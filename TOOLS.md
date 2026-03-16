# qyl Tools

## Token Resolution

**`src/lib/resolve-tokens.ts`** — Template token resolver for `{{path.to.value}}` placeholders.

### API

```ts
import { resolveTokens, extractTokens, allTokensResolvable } from "./src/lib/resolve-tokens.ts";

// Resolve tokens against a context object
resolveTokens("Service {{service.name}} has {{count}} errors", {
  service: { name: "api-gateway" },
  count: 42,
});
// => "Service api-gateway has 42 errors"

// Built-in filters: uppercase, lowercase, trim, json, iso
resolveTokens("{{name | uppercase}}", { name: "ada" });
// => "ADA"

// Custom filters
resolveTokens("{{w | reverse}}", { w: "abc" }, {
  filters: { reverse: (v) => [...v].reverse().join("") },
});
// => "cba"

// Extract all token paths from a template
extractTokens("{{a.b}} and {{c | uppercase}}");
// => ["a.b", "c"]

// Check if all tokens are resolvable
allTokensResolvable("{{a}} {{b.c}}", { a: 1, b: { c: 2 } });
// => true
```

### Test

```bash
npm run test:tokens
```

---

## Artifact Storage

Store and retrieve shareable content (code patches, analysis reports, investigation notes) produced by AI agent operations.

### MCP Tools

| Tool | Description |
|------|-------------|
| `qyl.store_artifact` | Store content with optional title, source, content type, and TTL |
| `qyl.get_artifact` | Retrieve a stored artifact by ID |

### REST API

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/artifacts` | Create an artifact |
| GET | `/api/v1/artifacts/{id}` | Retrieve an artifact (JSON) |
| GET | `/a/{id}` | Short URL — returns raw content or JSON based on Accept header |

### Request body (POST)

```json
{
  "content": "## Analysis Report\n...",
  "content_type": "text/markdown",
  "title": "RCA for trace abc123",
  "source": "autofix",
  "metadata": { "trace_id": "abc123" },
  "ttl_seconds": 86400
}
```

### Response

```json
{
  "id": "a1b2c3d4e5f6",
  "content_type": "text/markdown",
  "content": "## Analysis Report\n...",
  "title": "RCA for trace abc123",
  "source": "autofix",
  "metadata": { "trace_id": "abc123" },
  "created_at": "2026-03-15T10:00:00Z",
  "expires_at": "2026-03-16T10:00:00Z"
}
```

### Export CLI

```bash
# Fetch artifact content to stdout
npx tsx tools/export-artifact.ts <id>

# Write to file
npx tsx tools/export-artifact.ts <id> --out report.md

# Full JSON response
npx tsx tools/export-artifact.ts <id> --json

# Custom collector URL
npx tsx tools/export-artifact.ts <id> --url https://qyl.example.com
```

Environment variables: `QYL_URL` (primary), `QYL_COLLECTOR_URL` (backward-compatible alias), `QYL_TOKEN` (optional auth). No default URL — provide one via `--url` or an environment variable.

---

## Build

```bash
npm run build:export    # Type-check TypeScript tooling
npm run test:tokens     # Run resolve-tokens tests
```
