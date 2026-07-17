import { useEffect, useState } from "react";
import type { ResourceState } from "./types";

const STREAM_URL = "/runner/resources/stream";

export type ConnectionState = "connecting" | "open" | "closed" | "failed";

export interface ResourcesState {
  resources: ResourceState[];
  connection: ConnectionState;
  error: string | null;
}

function failureMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

// Subscribes to the runner's SSE state stream. The server replays a snapshot on connect and then pushes
// each change; we key resources by name (last-write-wins), matching the server's idempotent contract.
export function useResources(): ResourcesState {
  const [byName, setByName] = useState<Map<string, ResourceState>>(() => new Map());
  const [connection, setConnection] = useState<ConnectionState>("connecting");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    let source: EventSource | null = null;
    let retry: ReturnType<typeof setTimeout> | undefined;

    const start = async () => {
      try {
        const { parseResourceState } = await import("./contract-validation");
        if (cancelled) return;

        const connect = () => {
          if (cancelled) return;
          setConnection("connecting");
          const currentSource = new EventSource(STREAM_URL);
          source = currentSource;
          let transportFailed = false;

          currentSource.onopen = () => {
            if (!cancelled) setConnection("open");
          };

          currentSource.onmessage = (event) => {
            if (cancelled) return;
            try {
              const state = parseResourceState(JSON.parse(event.data));
              setByName((prev) => new Map(prev).set(state.name, state));
            } catch (frameError) {
              currentSource.close();
              if (source === currentSource) source = null;
              if (cancelled) return;
              setConnection("failed");
              setError(`Resource stream contract failure: ${failureMessage(frameError)}`);
            }
          };

          currentSource.onerror = () => {
            if (transportFailed) return;
            transportFailed = true;
            currentSource.close();
            if (source === currentSource) source = null;
            if (cancelled) return;
            setConnection("closed");
            retry = setTimeout(connect, 1500);
          };
        };

        connect();
      } catch (loadError) {
        if (cancelled) return;
        setConnection("failed");
        setError(`Unable to load resource stream validation: ${failureMessage(loadError)}`);
      }
    };

    void start();
    return () => {
      cancelled = true;
      if (retry) clearTimeout(retry);
      source?.close();
    };
  }, []);

  const resources = [...byName.values()].sort((a, b) => a.name.localeCompare(b.name));
  return { resources, connection, error };
}
