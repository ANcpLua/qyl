import { useEffect, useState } from "react";
import type { LogLine } from "./types";

const MAX_LINES = 500;

export interface LogsState {
  lines: LogLine[];
  error: string | null;
}

function failureMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

// Subscribes to a resource's log stream. The server replays a snapshot on connect, then pushes new lines.
export function useLogs(resource: string | null): LogsState {
  const [lines, setLines] = useState<LogLine[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLines([]);
    setError(null);
    if (!resource) return;

    let cancelled = false;
    let source: EventSource | null = null;

    const start = async () => {
      try {
        const { parseLogLine } = await import("./contract-validation");
        if (cancelled) return;
        const currentSource = new EventSource(
          `/runner/resources/${encodeURIComponent(resource)}/logs/stream`,
        );
        source = currentSource;

        currentSource.onmessage = (event) => {
          if (cancelled) return;
          try {
            const line = parseLogLine(JSON.parse(event.data));
            setLines((prev) => {
              const next = [...prev, line];
              return next.length > MAX_LINES ? next.slice(next.length - MAX_LINES) : next;
            });
          } catch (frameError) {
            currentSource.close();
            if (source === currentSource) source = null;
            if (cancelled) return;
            setError(`Log stream contract failure: ${failureMessage(frameError)}`);
          }
        };
      } catch (loadError) {
        if (cancelled) return;
        setError(`Unable to load log stream validation: ${failureMessage(loadError)}`);
      }
    };

    void start();
    return () => {
      cancelled = true;
      source?.close();
    };
  }, [resource]);

  return { lines, error };
}
