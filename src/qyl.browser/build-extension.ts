import { cpSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "vite";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const outDir = resolve(__dirname, "dist-extension");

const entries = [
  { input: "extension/content/content.ts", out: "content/content" },
  { input: "extension/background/service-worker.ts", out: "background/service-worker" },
  { input: "extension/popup/popup.ts", out: "popup/popup" },
];

for (const entry of entries) {
  await build({
    configFile: false,
    build: {
      outDir,
      emptyOutDir: false,
      lib: {
        entry: resolve(__dirname, entry.input),
        name: "_",
        formats: ["iife"],
        fileName: () => `${entry.out}.js`,
      },
      rollupOptions: {
        output: { extend: true },
      },
      target: "chrome120",
      minify: "terser",
      sourcemap: false,
    },
    logLevel: "warn",
  });
}

// Copy static assets
const assets = [
  ["extension/manifest.json", "manifest.json"],
  ["extension/popup/popup.html", "popup/popup.html"],
  ["extension/styles/toolbar.css", "styles/toolbar.css"],
] as const;

for (const [src, dest] of assets) {
  const destPath = resolve(outDir, dest);
  mkdirSync(resolve(destPath, ".."), { recursive: true });
  cpSync(resolve(__dirname, src), destPath);
}

mkdirSync(resolve(outDir, "icons"), { recursive: true });
for (const size of [16, 48, 128]) {
  cpSync(
    resolve(__dirname, `extension/icons/icon-${size}.png`),
    resolve(outDir, `icons/icon-${size}.png`),
  );
}

console.log("Extension built to dist-extension/");
