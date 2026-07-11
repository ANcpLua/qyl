import { useState } from "react";
import { useResources } from "./useResources";
import { useLogs } from "./useLogs";
import { callTool, useTools } from "./useTools";
import { MCP_KINDS, type ResourceLifecycle } from "./types";

const DOT: Record<ResourceLifecycle, string> = {
  Pending: "#6b7280",
  Starting: "#d97706",
  Ready: "#16a34a",
  Stopping: "#d97706",
  Stopped: "#6b7280",
  Failed: "#dc2626",
};

export default function App() {
  const { resources, connection } = useResources();
  const [selected, setSelected] = useState<string | null>(null);
  const logs = useLogs(selected);
  const selectedResource = resources.find((r) => r.name === selected) ?? null;
  const isMcp = selectedResource?.kind != null && MCP_KINDS.has(selectedResource.kind);
  const tools = useTools(selected, isMcp);

  return (
    <main className="app">
      <header className="header">
        <h1>
          qyl<span className="accent">.host</span>
        </h1>
        <span className={`conn conn-${connection}`}>● {connection}</span>
      </header>

      {resources.length === 0 ? (
        <p className="empty">Waiting for resources…</p>
      ) : (
        <table className="grid">
          <thead>
            <tr>
              <th>Resource</th>
              <th>Kind</th>
              <th>Status</th>
              <th>Port</th>
              <th>Endpoint</th>
            </tr>
          </thead>
          <tbody>
            {resources.map((r) => (
              <tr
                key={r.name}
                className={r.name === selected ? "row selected" : "row"}
                onClick={() => setSelected(r.name === selected ? null : r.name)}
              >
                <td className="name">{r.name}</td>
                <td>{r.kind ?? "—"}</td>
                <td>
                  <span className="dot" style={{ background: DOT[r.lifecycle] }} />
                  {r.lifecycle}
                  {r.lastError ? (
                    <span className="err" title={r.lastError}>
                      {" — "}
                      {r.lastError}
                    </span>
                  ) : null}
                </td>
                <td>{r.allocatedPort ?? "—"}</td>
                <td>
                  {r.endpoint ? (
                    <a
                      href={r.endpoint}
                      target="_blank"
                      rel="noreferrer"
                      onClick={(e) => e.stopPropagation()}
                    >
                      {r.endpoint}
                    </a>
                  ) : (
                    "—"
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selected && isMcp ? <ToolsPanel resource={selected} tools={tools} /> : null}

      {selected ? (
        <section className="logs">
          <div className="logs-head">
            <span>
              logs · <strong>{selected}</strong>
            </span>
            <button className="logs-close" onClick={() => setSelected(null)}>
              ✕
            </button>
          </div>
          <div className="logs-body">
            {logs.length === 0
              ? "— no output yet —"
              : logs.map((l, i) => (
                  <div key={i} className={l.stream === "err" ? "logline err-line" : "logline"}>
                    {l.line}
                  </div>
                ))}
          </div>
        </section>
      ) : (
        resources.length > 0 && <p className="hint">Click a resource to stream its logs.</p>
      )}
    </main>
  );
}

function ToolsPanel({ resource, tools }: { resource: string; tools: ReturnType<typeof useTools> }) {
  const [tool, setTool] = useState<string | null>(null);
  const [argsJson, setArgsJson] = useState("{}");
  const [result, setResult] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const invoke = async () => {
    if (!tool) return;
    setBusy(true);
    setResult(null);
    try {
      setResult(await callTool(resource, tool, argsJson));
    } catch (err) {
      setResult(String(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="logs tools">
      <div className="logs-head">
        <span>
          tools · <strong>{resource}</strong>
        </span>
      </div>
      <div className="logs-body">
        {tools.phase === "loading" && <div className="logline">— loading tools —</div>}
        {tools.phase === "error" && <div className="logline err-line">tools/list failed: {tools.message}</div>}
        {tools.phase === "ready" && tools.tools.length === 0 && (
          <div className="logline">— server exposes no tools —</div>
        )}
        {tools.phase === "ready" &&
          tools.tools.map((t) => (
            <div key={t.name} className="logline">
              <button
                className={t.name === tool ? "tool selected" : "tool"}
                onClick={() => {
                  setTool(t.name === tool ? null : t.name);
                  setResult(null);
                }}
              >
                {t.name}
              </button>
              {t.description ? <span className="tool-desc"> {t.description}</span> : null}
            </div>
          ))}
        {tool ? (
          <div className="tool-call">
            <textarea
              className="tool-args"
              value={argsJson}
              onChange={(e) => setArgsJson(e.target.value)}
              rows={3}
              spellCheck={false}
            />
            <button className="tool-run" disabled={busy} onClick={() => void invoke()}>
              {busy ? "calling…" : `call ${tool}`}
            </button>
            {result ? <pre className="tool-result">{result}</pre> : null}
          </div>
        ) : null}
      </div>
    </section>
  );
}
