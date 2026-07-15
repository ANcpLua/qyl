import {resolve} from 'node:path';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import {defineConfig} from 'vite';

export default defineConfig({
    plugins: [react(), tailwindcss()],
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
});
