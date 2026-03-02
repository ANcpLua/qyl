import {useQuery} from '@tanstack/react-query';

interface LlmProviderStatus {
    configured: boolean;
    provider?: string;
    model?: string;
}

async function fetchLlmStatus(): Promise<LlmProviderStatus> {
    const res = await fetch('/api/v1/copilot/llm/status');
    if (!res.ok) return {configured: false};
    return res.json();
}

export function useLlmStatus() {
    return useQuery({
        queryKey: ['llm', 'status'],
        queryFn: fetchLlmStatus,
        retry: false,
        staleTime: 60_000,
    });
}
