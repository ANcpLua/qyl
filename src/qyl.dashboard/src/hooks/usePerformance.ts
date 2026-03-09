import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

interface StorageStats {
    spanCount: number;
    sessionCount: number;
    logCount: number;
    oldestSpan: string | null;
    newestSpan: string | null;
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

interface LatencyBaseline {
    mean: number;
    stddev: number;
    p50: number;
    p95: number;
    p99: number;
}

export const performanceKeys = {
    all: ['performance'] as const,
    stats: () => [...performanceKeys.all, 'stats'] as const,
    services: () => [...performanceKeys.all, 'services'] as const,
    errors: () => [...performanceKeys.all, 'errors'] as const,
    latency: () => [...performanceKeys.all, 'latency'] as const,
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

export function useLatencyBaseline() {
    return useQuery({
        queryKey: performanceKeys.latency(),
        queryFn: () => fetchJson<LatencyBaseline>('/api/v1/analytics/anomaly/baseline?metric=latency&hours=24'),
        staleTime: 60_000,
    });
}
