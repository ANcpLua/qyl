import { useEffect, useState } from "react";
import { parseLogLine } from "./contract-validation";
import type { LogLine } from "./types";

const MAX_LINES = 500;

// Subscribes to a resource's log stream. The server replays a snapshot on connect, then pushes new lines.
export function useLogs(resource: string | null): LogLine[] {
  const [lines, setLines] = useState<LogLine[]>([]);

  useEffect(() => {
    setLines([]);
    if (!resource) return;

    let cancelled = false;
    const source = new EventSource(
      `/runner/resources/${encodeURIComponent(resource)}/logs/stream`,
    );

    source.onmessage = (event) => {
      if (cancelled) return;
      try {
        const line = parseLogLine(JSON.parse(event.data));
        setLines((prev) => {
          const next = [...prev, line];
          return next.length > MAX_LINES ? next.slice(next.length - MAX_LINES) : next;
        });
      } catch {
        // ignore a malformed frame
      }
    };

    return () => {
      cancelled = true;
      source.close();
    };
  }, [resource]);

  return lines;
}
