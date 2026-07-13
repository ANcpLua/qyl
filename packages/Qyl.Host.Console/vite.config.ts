import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const runnerOrigin = process.env.QYL_RUNNER_ORIGIN ?? "http://127.0.0.1:18888";

function isTrustedDevOrigin(origin: string, host: string | undefined): boolean {
  try {
    const parsed = new URL(origin);
    return (
      parsed.protocol === "http:" &&
      parsed.host === host &&
      (parsed.hostname === "localhost" || parsed.hostname === "127.0.0.1" || parsed.hostname === "[::1]")
    );
  } catch {
    return false;
  }
}

// Dev-only dashboard for the Qyl.Run runner. Proxies /runner to the loopback runner API so the
// browser talks same-origin in dev. The runner intentionally does not enable cross-origin access.
// QYL_RUNNER_ORIGIN overrides the proxy target when the runner API runs on a non-default port
// (Qyl__Run__RunnerPort on the runner side).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5051,
    headers: {
      "Content-Security-Policy": "frame-ancestors 'none'",
      "X-Frame-Options": "DENY",
    },
    proxy: {
      "/runner": {
        target: runnerOrigin,
        changeOrigin: true,
        configure(proxy) {
          // The runner validates browser mutation origins. The browser sees the Vite dev origin,
          // so make the proxied Origin match the loopback target just as the embedded console does.
          proxy.on("proxyReq", (proxyRequest, request) => {
            const origin = request.headers.origin;
            if (typeof origin === "string" && isTrustedDevOrigin(origin, request.headers.host)) {
              proxyRequest.setHeader("Origin", runnerOrigin);
            }
          });
        },
      },
    },
  },
});
