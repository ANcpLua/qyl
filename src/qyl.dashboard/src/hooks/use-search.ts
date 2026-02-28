import {useQuery} from '@tanstack/react-query';

export interface SearchResult {
    entity_type: string;
    entity_id: string;
    title: string;
    snippet: string;
    timestamp?: number;
    score: number;
}

interface SearchResponse {
    items: SearchResult[];
    total: number;
}

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {credentials: 'include', ...init});
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
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
                body: JSON.stringify({query, entity_types: entityTypes, limit}),
            }),
        select: (data) => data.items,
        enabled: query.length >= 2,
        staleTime: 30_000,
    });
}

