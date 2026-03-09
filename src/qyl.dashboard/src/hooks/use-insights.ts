import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface InsightTierStatus {
    tier: string;
    hash: string;
    materializedAt: string | null;
    durationMs: number;
}

export interface InsightsResponse {
    markdown: string;
    lastUpdated: string | null;
    tiers: InsightTierStatus[];
}

// ── Query keys ────────────────────────────────────────────────────────────────

export const insightKeys = {
    all: ['insights'] as const,
    overview: () => [...insightKeys.all, 'overview'] as const,
    tier: (tier: string) => [...insightKeys.all, tier] as const,
};

// ── Hooks ─────────────────────────────────────────────────────────────────────

export function useInsights() {
    return useQuery({
        queryKey: insightKeys.overview(),
        queryFn: () => fetchJson<InsightsResponse>('/api/v1/insights'),
        staleTime: 30_000,
    });
}

export function useInsightTier(tier: string) {
    return useQuery({
        queryKey: insightKeys.tier(tier),
        queryFn: () => fetchJson<InsightsResponse>(`/api/v1/insights/${tier}`),
        enabled: !!tier,
        staleTime: 30_000,
    });
}
