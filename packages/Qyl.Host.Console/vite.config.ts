import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Dev-only dashboard for the Qyl.Run runner. Proxies /runner to the runner's read-only state API so the
// browser talks same-origin in dev (the API also sends Access-Control-Allow-Origin, so direct use works too).
// QYL_RUNNER_ORIGIN overrides the proxy target when the runner API runs on a non-default port
// (Qyl__Run__RunnerPort on the runner side).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5051,
    proxy: {
      "/runner": {
        target: process.env.QYL_RUNNER_ORIGIN ?? "http://127.0.0.1:18888",
        changeOrigin: true,
      },
    },
  },
});
