import {useQuery} from '@tanstack/react-query';

interface MetaBuild {
    commit: string | null;
    informationalVersion: string | null;
}

interface MetaCapabilities {
    tracing: boolean;
    grpc: boolean;
    alerting: boolean;
    genAi: boolean;
    copilot: boolean;
    embeddedDashboard: boolean;
}

interface MetaStatus {
    grpcEnabled: boolean;
    authMode: string;
}

interface MetaLinks {
    dashboard: string | null;
    otlpHttp: string | null;
    otlpGrpc: string | null;
}

interface MetaPorts {
    http: number;
    grpc: number;
}

export interface MetaResponse {
    version: string;
    runtime: string;
    build: MetaBuild;
    capabilities: MetaCapabilities;
    status: MetaStatus;
    links: MetaLinks;
    ports: MetaPorts;
}

export const metaKeys = {
    all: ['meta'] as const,
};

async function fetchMeta(): Promise<MetaResponse> {
    const res = await fetch('/api/v1/meta');
    if (!res.ok) throw new Error('Failed to fetch capabilities');
    return res.json();
}

export function useCapabilities() {
    return useQuery({
        queryKey: metaKeys.all,
        queryFn: fetchMeta,
        staleTime: Infinity,
    });
}

export function useHasCapability(key: keyof MetaCapabilities): boolean {
    const {data} = useCapabilities();
    return data?.capabilities[key] ?? false;
}
