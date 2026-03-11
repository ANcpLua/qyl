import {describe, expect, it} from 'vitest';

import {resolveOnboardingConnection, type CollectorMeta} from './onboarding';

describe('resolveOnboardingConnection', () => {
    it('uses qyl defaults when meta has not loaded yet', () => {
        const connection = resolveOnboardingConnection(undefined, {
            hostname: 'localhost',
            host: 'localhost:5173',
            origin: 'http://localhost:5173',
        });

        expect(connection).toMatchObject({
            isLocal: true,
            dashboardPort: 5100,
            otlpHttpPort: 4318,
            grpcPort: 4317,
            grpcEnabled: true,
            otlpHttpEndpoint: 'http://localhost:4318',
            otlpHttpTraceUrl: 'http://localhost:4318/v1/traces',
            grpcEndpoint: 'http://localhost:4317',
            grpcHost: 'localhost:4317',
        });
    });

    it('uses qyl defaults when meta fetch failed (null)', () => {
        const connection = resolveOnboardingConnection(null, {
            hostname: 'localhost',
            host: 'localhost:5173',
            origin: 'http://localhost:5173',
        });

        expect(connection).toMatchObject({
            isLocal: true,
            dashboardPort: 5100,
            otlpHttpPort: 4318,
            grpcPort: 4317,
        });
    });

    it('treats 127.0.0.1 as local', () => {
        const connection = resolveOnboardingConnection(undefined, {
            hostname: '127.0.0.1',
            host: '127.0.0.1:5173',
            origin: 'http://127.0.0.1:5173',
        });

        expect(connection.isLocal).toBe(true);
        expect(connection.otlpHttpEndpoint).toBe('http://localhost:4318');
    });

    it('falls back to the dashboard listener when the dedicated OTLP HTTP port is disabled', () => {
        const meta: CollectorMeta = {
            ports: {
                http: 5100,
                grpc: 4317,
                otlpHttp: 0,
            },
        };

        const connection = resolveOnboardingConnection(meta, {
            hostname: 'localhost',
            host: 'localhost:5173',
            origin: 'http://localhost:5173',
        });

        expect(connection.otlpHttpPort).toBe(5100);
        expect(connection.otlpHttpEndpoint).toBe('http://localhost:5100');
        expect(connection.otlpHttpTraceUrl).toBe('http://localhost:5100/v1/traces');
    });

    it('marks gRPC as disabled when the collector turns that listener off', () => {
        const meta: CollectorMeta = {
            ports: {
                http: 5100,
                grpc: 0,
                otlpHttp: 4318,
            },
        };

        const connection = resolveOnboardingConnection(meta, {
            hostname: 'localhost',
            host: 'localhost:5173',
            origin: 'http://localhost:5173',
        });

        expect(connection.grpcEnabled).toBe(false);
        expect(connection.grpcEndpoint).toBeNull();
        expect(connection.grpcHost).toBeNull();
    });

    it('uses same-origin OTLP HTTP when onboarding runs on a remote host', () => {
        const connection = resolveOnboardingConnection(undefined, {
            hostname: 'qyl.example.com',
            host: 'qyl.example.com',
            origin: 'https://qyl.example.com',
        });

        expect(connection).toMatchObject({
            isLocal: false,
            otlpHttpEndpoint: 'https://qyl.example.com',
            otlpHttpTraceUrl: 'https://qyl.example.com/v1/traces',
            grpcEndpoint: null,
            grpcHost: null,
        });
    });

    it('uses server-computed links when available on remote host', () => {
        const meta: CollectorMeta = {
            ports: {http: 5100, grpc: 4317, otlpHttp: 4318},
            links: {otlpHttp: 'https://otlp.example.com/v1/traces'},
        };

        const connection = resolveOnboardingConnection(meta, {
            hostname: 'qyl.example.com',
            host: 'qyl.example.com',
            origin: 'https://qyl.example.com',
        });

        expect(connection.otlpHttpEndpoint).toBe('https://otlp.example.com');
        expect(connection.otlpHttpTraceUrl).toBe('https://otlp.example.com/v1/traces');
    });
});
