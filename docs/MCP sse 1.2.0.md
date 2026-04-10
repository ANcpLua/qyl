````markdown
# v1.2.0

This release improves stateless HTTP transport defaults and documentation, but it also includes a **breaking behavioral
change** that is being treated as a server reliability fix rather than a major-version bump.

The two most important changes are:

1. Legacy SSE endpoints are now disabled by default.
2. The 2-argument `RequestContext` constructor is now obsolete.

## Breaking Changes

Refer to the [C# SDK Versioning](https://csharp.sdk.modelcontextprotocol.io/versioning.html) documentation for details
on versioning and breaking change policies.

### 1. Disable legacy SSE by default

`MapMcp()` no longer maps `/sse` and `/message` endpoints by default. Servers whose clients connect via SSE will find
those endpoints removed unless legacy SSE is explicitly re-enabled.

#### What changed

If your clients connect to a `/sse` endpoint such as:

```text
https://my-server.example.com/sse
```

then they were using the legacy SSE transport, assuming the server was not running in `Stateless` mode.

The `/sse` and `/message` endpoints are now disabled by default:

- `EnableLegacySse` defaults to `false`
- `EnableLegacySse` is marked `[Obsolete]`
- the diagnostic is `MCP9004`

That means upgrading the server SDK without updating clients can break existing SSE connections.

#### Client-side migration

Change the client `Endpoint` from the `/sse` path to the root MCP endpoint, which is the same URL your server passes to
`MapMcp()`.

```csharp
// Before (legacy SSE):
Endpoint = new Uri("https://my-server.example.com/sse")

// After (Streamable HTTP):
Endpoint = new Uri("https://my-server.example.com/")
```

With the default `HttpTransportMode.AutoDetect`, the client automatically tries Streamable HTTP first. If you already
know the server supports it, you can explicitly set:

```csharp
TransportMode = HttpTransportMode.StreamableHttp
```

#### Server-side migration

If you previously relied on `/sse` being mapped automatically, you now need:

```csharp
EnableLegacySse = true
```

That suppresses the `MCP9004` warning and keeps the SSE endpoints available.

The recommended path is:

1. migrate all clients to Streamable HTTP
2. remove `EnableLegacySse`

#### Transition period

If some clients still require SSE while others have already moved to Streamable HTTP, you can temporarily support both
transports by using:

```csharp
EnableLegacySse = true
Stateless = false
```

In that configuration, `MapMcp()` serves both transports at the same time:

- Streamable HTTP on the root MCP endpoint
- SSE on `/sse`
- POST messages on `/message`

Once all clients have migrated, remove `EnableLegacySse` and optionally switch to `Stateless = true`.

#### Why SSE is disabled by default

Legacy SSE is opt-in only because it does not provide built-in HTTP-level backpressure.

The legacy SSE transport uses two separate channels:

- clients POST JSON-RPC messages to `/message`
- clients receive responses through a long-lived GET SSE stream on `/sse`

The POST endpoint returns `202 Accepted` immediately after queuing the message. It does **not** wait for the handler to
complete.

That means:

- there is no HTTP-level backpressure on handler concurrency
- a client can send unlimited POST requests to `/message`
- each request can spawn a concurrent handler
- there is no built-in per-client concurrency limit

The GET stream does provide session lifetime bounds. Handler cancellation tokens are linked to the GET request’s
`HttpContext.RequestAborted`, so when the client disconnects the SSE stream, all in-flight handlers are cancelled.

This is similar to a connection-bound lifetime model, but unlike systems such as SignalR, it does not provide a
per-client concurrency limit. It only ensures cleanup on disconnect.

### 2. Obsolete 2-arg `RequestContext` constructor

The `RequestContext(McpServer, JsonRpcRequest)` constructor is now `[Obsolete]` and produces build warnings with
diagnostic `MCP9003`.

The `Params` property also changes from:

```csharp
TParams?
```

to:

```csharp
TParams
```

#### Migration

Use the new 3-argument constructor instead:

```csharp
new RequestContext(server, request, parameters)
```

## Notable changes

- Support specifying `OutputSchema` independently of return type for tools returning `CallToolResult`
- Fix `WithMeta` + `WithProgress` causing tool invocation failure
- Fix per-task DI scope creation in `ExecuteToolAsTaskAsync` to prevent `ObjectDisposedException`
- Route `SendRequestAsync` logic through outgoing message filters
- Add stateless documentation and disable legacy SSE by default
- Update documentation URLs to the new vanity domain
- Align roots terminology with the MCP spec clarification

## Full changelog

https://github.com/modelcontextprotocol/csharp-sdk/compare/v1.1.0...v1.2.0
````