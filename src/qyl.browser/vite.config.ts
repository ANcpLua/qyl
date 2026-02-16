import {defineConfig} from 'vite';
import {resolve} from 'path';
import dts from 'vite-plugin-dts';

// Main ESM build for npm consumers + self-contained IIFE for script tags
export default defineConfig({
    plugins: [
        dts({tsconfigPath: './tsconfig.json'}),
    ],
    build: {
        lib: {
            entry: {
                index: resolve(__dirname, 'src/index.ts'),
                react: resolve(__dirname, 'src/react.ts'),
            },
            formats: ['es'],
        },
        rollupOptions: {
            external: ['react'],
            output: {
                entryFileNames: '[name].js',
                chunkFileNames: 'chunks/[name]-[hash].js',
            },
        },
        target: 'es2022',
        minify: 'terser',
        terserOptions: {
            compress: {passes: 2, toplevel: true},
            mangle: {toplevel: true},
        },
    },
});
