import {useQuery} from '@tanstack/react-query';
import {useEffect, useState} from 'react';

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

interface SuggestionsResponse {
    suggestions: string[];
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
    suggestions: (prefix: string) =>
        [...searchKeys.all, 'suggestions', prefix] as const,
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

function useDebounce<T>(value: T, delay: number): T {
    const [debounced, setDebounced] = useState(value);
    useEffect(() => {
        const id = setTimeout(() => setDebounced(value), delay);
        return () => clearTimeout(id);
    }, [value, delay]);
    return debounced;
}

export function useSearchSuggestions(prefix: string) {
    const debounced = useDebounce(prefix, 300);
    return useQuery({
        queryKey: searchKeys.suggestions(debounced),
        queryFn: () => {
            const params = new URLSearchParams({prefix: debounced});
            return fetchJson<SuggestionsResponse>(`/api/v1/search/suggestions?${params}`);
        },
        select: (data) => data.suggestions,
        enabled: debounced.length >= 2,
        staleTime: 60_000,
    });
}
