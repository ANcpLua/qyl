import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

export interface ErrorRow {
    errorId: string;
    errorType: string;
    message: string;
    category: string;
    fingerprint: string;
    firstSeen: string;
    lastSeen: string;
    occurrenceCount: number;
    affectedUserIds: string | null;
    affectedServices: string | null;
    status: 'new' | 'acknowledged' | 'resolved' | 'ignored';
    assignedTo: string | null;
    issueUrl: string | null;
    sampleTraces: string | null;
}

export interface ErrorCategoryStat {
    category: string;
    count: number;
}

export interface ErrorStats {
    totalCount: number;
    byCategory: ErrorCategoryStat[];
}

interface ErrorsResponse {
    items: ErrorRow[];
    total: number;
}

interface ErrorFilters {
    category?: string;
    status?: string;
    serviceName?: string;
    limit?: number;
}

export const errorKeys = {
    all: ['errors'] as const,
    list: (filters?: ErrorFilters) => [...errorKeys.all, 'list', filters] as const,
    stats: () => [...errorKeys.all, 'stats'] as const,
    detail: (id: string) => [...errorKeys.all, id] as const,
};

export function useErrors(filters?: ErrorFilters) {
    return useQuery({
        queryKey: errorKeys.list(filters),
        queryFn: () => {
            const params = new URLSearchParams();
            params.set('limit', String(filters?.limit ?? 50));
            if (filters?.category) params.set('category', filters.category);
            if (filters?.status) params.set('status', filters.status);
            if (filters?.serviceName) params.set('serviceName', filters.serviceName);
            return fetchJson<ErrorsResponse>(`/api/v1/errors?${params}`);
        },
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useErrorStats() {
    return useQuery({
        queryKey: errorKeys.stats(),
        queryFn: () => fetchJson<ErrorStats>('/api/v1/errors/stats'),
        staleTime: 30_000,
    });
}

export function useError(errorId: string) {
    return useQuery({
        queryKey: errorKeys.detail(errorId),
        queryFn: () => fetchJson<ErrorRow>(`/api/v1/errors/${errorId}`),
        enabled: !!errorId,
        staleTime: 30_000,
    });
}
