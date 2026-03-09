import {useQuery} from '@tanstack/react-query';
import type {Span} from '@/types';
import {fetchJson} from '@/lib/api';

export interface RecentSpansResponse {
    spans: Span[];
    generation: number;
    source: string;
    bufferCount: number;
    bufferCapacity: number;
}

export const spanKeys = {
    all: ['spans'] as const,
    recent: (limit?: number) => [...spanKeys.all, 'recent', limit] as const,
};

export function useRecentSpans(limit = 100) {
    return useQuery({
        queryKey: spanKeys.recent(limit),
        queryFn: () => fetchJson<RecentSpansResponse>(`/api/v1/spans/recent?limit=${limit}`),
        staleTime: 30_000,
    });
}
