import { useEffect, useState } from "react";

// MCP tools for a selected resource, via the runner's /runner/mcp passthrough (Qyl.Host.Mcp).
// Converged from qyl.mcp/dashboard's useTools; the ext-apps App rendering stays with that
// dashboard until the C# runner grows a sandbox origin.

export interface McpTool {
  name: string;
  description?: string;
}

export type ToolsState =
  | { phase: "idle" }
  | { phase: "loading" }
  | { phase: "ready"; tools: McpTool[] }
  | { phase: "error"; message: string };

export function useTools(resource: string | null, isMcp: boolean): ToolsState {
  const [state, setState] = useState<ToolsState>({ phase: "idle" });

  useEffect(() => {
    if (!resource || !isMcp) {
      setState({ phase: "idle" });
      return;
    }

    let cancelled = false;
    setState({ phase: "loading" });

    fetch(`/runner/mcp/${encodeURIComponent(resource)}/tools`)
      .then(async (res) => {
        if (cancelled) return;
        if (!res.ok) {
          setState({ phase: "error", message: `HTTP ${res.status}` });
          return;
        }
        const body = (await res.json()) as { tools?: McpTool[] };
        if (!cancelled) setState({ phase: "ready", tools: body.tools ?? [] });
      })
      .catch((err: unknown) => {
        if (!cancelled) setState({ phase: "error", message: String(err) });
      });

    return () => {
      cancelled = true;
    };
  }, [resource, isMcp]);

  return state;
}

/** Calls a tool through the passthrough; returns the raw result JSON (pretty-printed). */
export async function callTool(resource: string, tool: string, argsJson: string): Promise<string> {
  const args = argsJson.trim() ? (JSON.parse(argsJson) as Record<string, unknown>) : {};
  const res = await fetch(`/runner/mcp/${encodeURIComponent(resource)}/tools/call`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ name: tool, arguments: args }),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${text}`);
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}
