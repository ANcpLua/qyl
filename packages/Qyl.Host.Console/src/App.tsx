import { useState } from "react";
import { useResources } from "./useResources";
import { useLogs } from "./useLogs";
import type { ResourceLifecycle } from "./types";
import { RunnerResourceLifecycleValues } from "@ancplua/qyl-api-schema/types";

const DOT: Record<ResourceLifecycle, string> = {
  [RunnerResourceLifecycleValues.pending]: "#6b7280",
  [RunnerResourceLifecycleValues.starting]: "#d97706",
  [RunnerResourceLifecycleValues.ready]: "#16a34a",
  [RunnerResourceLifecycleValues.stopping]: "#d97706",
  [RunnerResourceLifecycleValues.stopped]: "#6b7280",
  [RunnerResourceLifecycleValues.failed]: "#dc2626",
};

export default function App() {
  const { resources, connection, error: resourcesError } = useResources();
  const [selected, setSelected] = useState<string | null>(null);
  const { lines: logs, error: logsError } = useLogs(selected);

  return (
    <main className="app">
      <header className="header">
        <h1>
          qyl<span className="accent">.host</span>
        </h1>
        <span className={`conn conn-${connection}`}>● {connection}</span>
      </header>

      {resourcesError ? (
        <p className="stream-error" role="alert">{resourcesError}</p>
      ) : null}

      {resources.length === 0 && !resourcesError ? (
        <p className="empty">Waiting for resources…</p>
      ) : resources.length > 0 ? (
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
      ) : null}

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
            {logsError ? (
              <div className="logline err-line" role="alert">{logsError}</div>
            ) : logs.length === 0
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
