import {defineConfig} from 'vite';
import {resolve} from 'path';

// Separate build for the self-contained IIFE script tag bundle
export default defineConfig({
    build: {
        lib: {
            entry: resolve(__dirname, 'src/script.ts'),
            name: 'qyl',
            formats: ['iife'],
            fileName: () => 'qyl.js',
        },
        outDir: 'dist',
        emptyOutDir: false, // Don't wipe the ESM build
        target: 'es2022',
        minify: 'terser',
        terserOptions: {
            compress: {passes: 3, toplevel: true},
            mangle: {toplevel: true},
        },
    },
});
