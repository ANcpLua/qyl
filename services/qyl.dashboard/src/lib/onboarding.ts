import {useQuery} from '@tanstack/react-query';

export interface CollectorMeta {
    version?: string | null;
    build?: {
        commit?: string | null;
        informationalVersion?: string | null;
        dashboardBuildId?: string | null;
        dashboardEntryAsset?: string | null;
        dashboardBuiltAtUtc?: string | null;
    } | null;
    ports?: {
        http?: number | null;
        grpc?: number | null;
        otlpHttp?: number | null;
    } | null;
    links?: {
        otlpHttp?: string | null;
        otlpGrpc?: string | null;
        dashboard?: string | null;
    } | null;
}

export interface OnboardingConnection {
    isLocal: boolean;
    dashboardPort: number;
    otlpHttpPort: number;
    grpcPort: number;
    grpcEnabled: boolean;
    otlpHttpEndpoint: string;
    otlpHttpTraceUrl: string;
    grpcEndpoint: string | null;
    grpcHost: string | null;
}

type BrowserLocation = Pick<Location, 'host' | 'hostname' | 'origin'>;

export function useCollectorMeta() {
    return useQuery({
        queryKey: ['meta'],
        queryFn: async (): Promise<CollectorMeta | null> => null,
        staleTime: 1000 * 60 * 5,
    });
}

export function resolveOnboardingConnection(
    meta: CollectorMeta | null | undefined,
    location: BrowserLocation,
): OnboardingConnection {
    const dashboardPort = meta?.ports?.http ?? 5100;
    const configuredOtlpHttpPort = meta?.ports?.otlpHttp ?? 4318;
    const otlpHttpPort = configuredOtlpHttpPort > 0 ? configuredOtlpHttpPort : dashboardPort;
    const grpcPort = meta?.ports?.grpc ?? 4317;
    const grpcEnabled = grpcPort > 0;
    const isLocal = location.hostname === 'localhost' || location.hostname === '127.0.0.1';

    if (!isLocal) {
        return {
            isLocal: false,
            dashboardPort,
            otlpHttpPort,
            grpcPort,
            grpcEnabled,
            otlpHttpEndpoint: meta?.links?.otlpHttp?.replace(/\/v1\/traces$/, '') ?? location.origin,
            otlpHttpTraceUrl: meta?.links?.otlpHttp ?? `${location.origin}/v1/traces`,
            grpcEndpoint: null,
            grpcHost: null,
        };
    }

    return {
        isLocal: true,
        dashboardPort,
        otlpHttpPort,
        grpcPort,
        grpcEnabled,
        otlpHttpEndpoint: `http://localhost:${otlpHttpPort}`,
        otlpHttpTraceUrl: `http://localhost:${otlpHttpPort}/v1/traces`,
        grpcEndpoint: grpcEnabled ? `http://localhost:${grpcPort}` : null,
        grpcHost: grpcEnabled ? `localhost:${grpcPort}` : null,
    };
}
