import {useMemo} from 'react';
import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

interface StorageStats {
    spansExported: number;
    spansDropped: number;
    metricsExported: number;
    metricsDropped: number;
    logsExported: number;
    logsDropped: number;
    exportErrors: number;
    queueUtilization: number;
}

export interface TrafficBucket {
    time: string;
    runs: number;
    errors: number;
    errorRate: number;
}

interface TrafficResponse {
    buckets: TrafficBucket[];
}

export interface ServiceSummary {
    serviceNamespace: string | null;
    serviceName: string;
    serviceType: string | null;
    latestVersion: string | null;
    providerName: string | null;
    defaultModel: string | null;
    firstSeen: string;
    lastSeen: string;
    lastErrorAt: string | null;
}

interface ServicesResponse {
    services: ServiceSummary[];
    total: number;
}

interface ErrorStats {
    errorCount: number;
    errorRate: number;
}

export const performanceKeys = {
    all: ['performance'] as const,
    stats: () => [...performanceKeys.all, 'stats'] as const,
    services: () => [...performanceKeys.all, 'services'] as const,
    errors: () => [...performanceKeys.all, 'errors'] as const,
    traffic: (from: number, to: number) => [...performanceKeys.all, 'traffic', from, to] as const,
};

export function useStorageStats() {
    return useQuery({
        queryKey: performanceKeys.stats(),
        queryFn: () => fetchJson<StorageStats>('/api/v1/telemetry/stats'),
        staleTime: 30_000,
    });
}

export function useServices() {
    return useQuery({
        queryKey: performanceKeys.services(),
        queryFn: () => fetchJson<ServicesResponse>('/api/v1/services'),
        staleTime: 30_000,
    });
}

export function useErrorStats() {
    return useQuery({
        queryKey: performanceKeys.errors(),
        queryFn: () => fetchJson<ErrorStats>('/api/v1/errors/stats'),
        staleTime: 30_000,
    });
}

export function useTraffic() {
    const range = useMemo(() => {
        const to = Date.now();
        const from = to - 24 * 60 * 60 * 1000; // 24h window
        return {from, to};
    }, []);

    return useQuery({
        queryKey: performanceKeys.traffic(range.from, range.to),
        queryFn: () => fetchJson<TrafficResponse>(
            `/api/v1/agents/overview/traffic?from=${range.from}&to=${range.to}&bucket=hour`
        ),
        staleTime: 30_000,
    });
}
