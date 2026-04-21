# @qyl/client

Generated JavaScript/TypeScript client for the [qyl](https://github.com/ancplua/qyl)
observability REST API.

Emitted from the TypeSpec sources in `core/specs/` by:

- `@qyl/typespec-emit-ts-types` — branded ID types (`TraceId`, `SpanId`, …), enums, interfaces
- `@typespec/http-client-js` — `ApiClient` + sub-clients (traces, metrics, sessions, …)
- `@typespec/json-schema` — JSON Schema bundle in `./schemas/qyl-api` for form generation

## Install

```sh
npm install @qyl/client
```

## Usage

```ts
import { ApiClient } from "@qyl/client";

const client = new ApiClient("https://collector.qyl.dev", { apiKey: "…" });
const page = await client.traces.list({ limit: 100 });
for (const span of page.data) {
    console.log(span.name, span.durationNs);
}
```

Types-only import (no runtime cost):

```ts
import type { TraceId, SpanId, Span } from "@qyl/client/types";
```

## License

MIT © 2025-2026 ancplua
