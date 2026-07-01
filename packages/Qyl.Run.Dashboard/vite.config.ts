import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Dev-only dashboard for the Qyl.Run runner. Proxies /runner to the runner's read-only state API so the
// browser talks same-origin in dev (the API also sends Access-Control-Allow-Origin, so direct use works too).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5051,
    proxy: {
      "/runner": { target: "http://127.0.0.1:18888", changeOrigin: true },
    },
  },
});
