# qyl.run.console

Dev-only **runner console** for the [`Qyl.Run`](../Qyl.Run) distributed-app runner —
a qyl-native (React + Vite + TypeScript) equivalent of Aspire's dashboard *resource view*.

It reads the runner's **read-only** state API and renders live resource status:

- `GET /runner/resources` — snapshot
- `GET /runner/resources/stream` — Server-Sent Events (snapshot replay + live changes)

## Why standalone (not a tab in the product dashboard)

`QylResourceState` is a runner-internal dev-time projection, deliberately **not** sourced from
`qyl-api-schema`. Keeping this a separate surface (never wired through the product dashboard's generated
`api.ts`) is what keeps it out of the single-sourced product API contract.

There are **no control verbs** here — the dashboard is read-only. Start/stop/restart stay on the runner's
terminal (TUI) keyboard.

## Run

```bash
# 1) start the runner (serves the /runner state API on 127.0.0.1:18888)
dotnet run --project packages/Qyl.Run.Host

# 2) start this dashboard (Vite dev server on :5051, proxies /runner -> :18888)
cd packages/Qyl.Run.Console && npm install && npm run dev
```
