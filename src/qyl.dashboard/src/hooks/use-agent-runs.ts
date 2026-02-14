import {useQuery} from '@tanstack/react-query';

// Types matching the DuckDB schema
export interface AgentRun {
    run_id: string;
    trace_id?: string;
    parent_run_id?: string;
    agent_name?: string;
    agent_type?: string;
    model?: string;
    provider?: string;
    status: string;
    input_tokens: number;
    output_tokens: number;
    total_cost: number;
    tool_call_count: number;
    start_time?: number;
    end_time?: number;
    duration_ns?: number;
    error_message?: string;
    metadata_json?: string;
}

export interface ToolCall {
    call_id: string;
    run_id: string;
    trace_id?: string;
    span_id?: string;
    tool_name?: string;
    tool_type?: string;
    arguments_json?: string;
    result_json?: string;
    status: string;
    start_time?: number;
    end_time?: number;
    duration_ns?: number;
    error_message?: string;
    sequence_number: number;
}

interface AgentRunsResponse {
    items: AgentRun[];
    total: number;
}

interface ToolCallsResponse {
    items: ToolCall[];
    total: number;
}

async function fetchJson<T>(url: string): Promise<T> {
    const res = await fetch(url, {credentials: 'include'});
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

export const agentRunKeys = {
    all: ['agent-runs'] as const,
    list: (filters?: {agentName?: string; status?: string}) =>
        [...agentRunKeys.all, 'list', filters] as const,
    detail: (runId: string) => [...agentRunKeys.all, 'detail', runId] as const,
    tools: (runId: string) => [...agentRunKeys.all, 'tools', runId] as const,
};

export function useAgentRuns(filters?: {agentName?: string; status?: string}) {
    return useQuery({
        queryKey: agentRunKeys.list(filters),
        queryFn: () => {
            const params = new URLSearchParams();
            params.set('limit', '100');
            if (filters?.agentName) params.set('agentName', filters.agentName);
            if (filters?.status) params.set('status', filters.status);
            return fetchJson<AgentRunsResponse>(`/api/v1/agent-runs?${params}`);
        },
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useAgentRun(runId: string) {
    return useQuery({
        queryKey: agentRunKeys.detail(runId),
        queryFn: () => fetchJson<AgentRun>(`/api/v1/agent-runs/${runId}`),
        enabled: !!runId,
        staleTime: 30_000,
    });
}

export function useToolCalls(runId: string) {
    return useQuery({
        queryKey: agentRunKeys.tools(runId),
        queryFn: () => fetchJson<ToolCallsResponse>(`/api/v1/agent-runs/${runId}/tools`),
        select: (data) => data.items,
        enabled: !!runId,
        staleTime: 30_000,
    });
}
