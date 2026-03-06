import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";

const __dirname = fileURLToPath(new URL(".", import.meta.url));

export default defineConfig({
  build: {
    outDir: "dist-extension",
    emptyOutDir: true,
    rollupOptions: {
      input: {
        "content/content": resolve(__dirname, "extension/content/content.ts"),
        "background/service-worker": resolve(
          __dirname,
          "extension/background/service-worker.ts",
        ),
        "popup/popup": resolve(__dirname, "extension/popup/popup.ts"),
      },
      output: {
        entryFileNames: "[name].js",
        format: "iife",
        inlineDynamicImports: false,
      },
    },
    target: "chrome120",
    minify: "terser",
    sourcemap: false,
  },
  plugins: [
    {
      name: "copy-extension-assets",
      generateBundle() {
        this.emitFile({
          type: "asset",
          fileName: "manifest.json",
          source: readFileSync(
            resolve(__dirname, "extension/manifest.json"),
            "utf-8",
          ),
        });

        this.emitFile({
          type: "asset",
          fileName: "popup/popup.html",
          source: readFileSync(
            resolve(__dirname, "extension/popup/popup.html"),
            "utf-8",
          ),
        });

        this.emitFile({
          type: "asset",
          fileName: "styles/toolbar.css",
          source: readFileSync(
            resolve(__dirname, "extension/styles/toolbar.css"),
            "utf-8",
          ),
        });

        for (const size of [16, 48, 128]) {
          this.emitFile({
            type: "asset",
            fileName: `icons/icon-${size}.png`,
            source: readFileSync(
              resolve(__dirname, `extension/icons/icon-${size}.png`),
            ) as unknown as string,
          });
        }
      },
    },
  ],
});
