import { useEffect, useState } from "react";
import type { Tool } from "@modelcontextprotocol/sdk/types.js";

const loadMcpTypes = () => import("@modelcontextprotocol/sdk/types.js");

export type ToolsState =
  | { phase: "idle" }
  | { phase: "loading" }
  | { phase: "ready"; tools: Tool[] }
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
        const { ListToolsResultSchema } = await loadMcpTypes();
        const body = ListToolsResultSchema.parse(await res.json());
        if (!cancelled) setState({ phase: "ready", tools: body.tools });
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

export async function callTool(resource: string, tool: string, argsJson: string): Promise<string> {
  const { CallToolRequestParamsSchema, CallToolResultSchema } = await loadMcpTypes();
  const args: unknown = argsJson.trim() ? JSON.parse(argsJson) : {};
  const request = CallToolRequestParamsSchema.parse({ name: tool, arguments: args });
  const res = await fetch(`/runner/mcp/${encodeURIComponent(resource)}/tools/call`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(request),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${text}`);
  return JSON.stringify(CallToolResultSchema.parse(JSON.parse(text)), null, 2);
}
