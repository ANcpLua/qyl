import {createHash} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import type {OutputChunk} from 'rollup';
import {defineConfig, type Plugin} from 'vite';

interface DashboardBuildRuntime {
    buildId: string;
    builtAtUtc: string;
    version: string;
    commit: string | null;
}

interface DashboardBuildManifest extends DashboardBuildRuntime {
    entryAsset: string;
}

const PACKAGE_JSON = JSON.parse(
    readFileSync(resolve(__dirname, './package.json'), 'utf8'),
) as { version?: string };

function resolveSourceCommit(): string | null {
    const commit = process.env.QYL_SOURCE_COMMIT
        ?? process.env.SOURCE_COMMIT
        ?? process.env.GIT_COMMIT
        ?? process.env.VERCEL_GIT_COMMIT_SHA
        ?? null;

    return commit?.trim() || null;
}

function resolveDashboardBuildRuntime(command: 'build' | 'serve'): DashboardBuildRuntime | null {
    if (command !== 'build') {
        return null;
    }

    const builtAtUtc = new Date().toISOString();
    const version = PACKAGE_JSON.version?.trim() || '0.0.0';
    const commit = resolveSourceCommit();
    const buildId = createHash('sha256')
        .update(`${version}:${builtAtUtc}:${commit ?? ''}`)
        .digest('hex')
        .slice(0, 12);

    return {
        buildId,
        builtAtUtc,
        version,
        commit,
    };
}

function qylDashboardBuildManifest(runtime: DashboardBuildRuntime | null): Plugin {
    return {
        name: 'qyl-dashboard-build-manifest',
        apply: 'build',
        generateBundle(_, bundle) {
            if (runtime === null) {
                return;
            }

            const entryChunk = Object.values(bundle).find(
                (output): output is OutputChunk =>
                    output.type === 'chunk'
                    && output.isEntry
                    && output.facadeModuleId?.endsWith('/src/main.tsx'),
            );

            if (!entryChunk) {
                this.error('Unable to resolve the qyl dashboard entry chunk for dashboard-build.json.');
                return;
            }

            const manifest: DashboardBuildManifest = {
                ...runtime,
                entryAsset: entryChunk.fileName,
            };

            this.emitFile({
                type: 'asset',
                fileName: 'dashboard-build.json',
                source: `${JSON.stringify(manifest, null, 2)}\n`,
            });
        },
    };
}

export default defineConfig(({command}) => {
    const dashboardBuildRuntime = resolveDashboardBuildRuntime(command);

    return {
        define: {
            __QYL_DASHBOARD_BUILD__: JSON.stringify(dashboardBuildRuntime),
        },
        plugins: [react(), tailwindcss(), qylDashboardBuildManifest(dashboardBuildRuntime)],
        resolve: {
            alias: {
                '@': resolve(__dirname, './src'),
            },
        },
        build: {
            rollupOptions: {
                output: {
                    manualChunks(id) {
                        if (id.includes('node_modules/react/') || id.includes('node_modules/react-dom/') || id.includes('node_modules/react-router')) {
                            return 'react-vendor';
                        }
                        if (id.includes('node_modules/@tanstack/')) {
                            return 'tanstack';
                        }
                        if (id.includes('node_modules/echarts')) {
                            return 'echarts';
                        }
                        if (id.includes('node_modules/recharts')) {
                            return 'recharts';
                        }
                    },
                },
            },
        },
        server: {
            port: 5173,
            proxy: {
                '/api': {
                    target: process.env.VITE_API_URL || 'http://localhost:5100',
                    changeOrigin: true,
                },
                '/health': {
                    target: process.env.VITE_API_URL || 'http://localhost:5100',
                    changeOrigin: true,
                },
            },
        },
    };
});
