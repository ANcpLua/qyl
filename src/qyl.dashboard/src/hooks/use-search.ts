import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

export interface SearchResult {
    entityType: string;
    entityId: string;
    title: string;
    snippet: string;
    timestamp?: string;
    score: number;
}

interface SearchResponse {
    items: SearchResult[];
    total: number;
}

export const searchKeys = {
    all: ['search'] as const,
    query: (query: string, entityTypes: string[], limit: number) =>
        [...searchKeys.all, 'query', query, entityTypes, limit] as const,
};

export function useSearch(query: string, entityTypes: string[] = [], limit = 50) {
    return useQuery({
        queryKey: searchKeys.query(query, entityTypes, limit),
        queryFn: () =>
            fetchJson<SearchResponse>('/api/v1/search/query', {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({
                    text: query,
                    entityTypes,
                    limit,
                    // Backward-compatible aliases.
                    query,
                    entity_types: entityTypes,
                }),
            }),
        select: (data) => data.items,
        enabled: query.length >= 2,
        staleTime: 30_000,
    });
}
