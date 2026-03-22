import {defineConfig} from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import {resolve} from 'path';

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
                manualChunks: {
                    'echarts': ['echarts', 'echarts-for-react'],
                    'recharts': ['recharts'],
                    'react-vendor': ['react', 'react-dom', 'react-router-dom'],
                    'tanstack': ['@tanstack/react-query', '@tanstack/react-table', '@tanstack/react-virtual'],
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
