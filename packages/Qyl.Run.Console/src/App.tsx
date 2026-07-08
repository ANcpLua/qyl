import { useState } from "react";
import { useResources } from "./useResources";
import { useLogs } from "./useLogs";
import type { ResourceLifecycle } from "./types";

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

  return (
    <main className="app">
      <header className="header">
        <h1>
          qyl<span className="accent">.run</span>
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
