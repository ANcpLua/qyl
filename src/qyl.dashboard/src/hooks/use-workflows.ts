import {useQuery} from '@tanstack/react-query';

export interface WorkflowRun {
    run_id: string;
    trace_id?: string;
    workflow_name?: string;
    workflow_type?: string;
    status: string;
    trigger?: string;
    node_count: number;
    completed_nodes: number;
    input_tokens: number;
    output_tokens: number;
    total_cost: number;
    start_time?: number;
    end_time?: number;
    duration_ns?: number;
    error_message?: string;
    metadata_json?: string;
}

export interface WorkflowEvent {
    event_id: string;
    run_id: string;
    event_type: string;
    node_id?: string;
    timestamp?: number;
    payload_json?: string;
}

export interface WorkflowCheckpoint {
    checkpoint_id: string;
    run_id: string;
    node_id?: string;
    timestamp?: number;
    state_json?: string;
}

interface WorkflowRunsResponse {
    items: WorkflowRun[];
    total: number;
}

interface WorkflowEventsResponse {
    items: WorkflowEvent[];
    total: number;
}

interface WorkflowCheckpointsResponse {
    items: WorkflowCheckpoint[];
    total: number;
}

async function fetchJson<T>(url: string): Promise<T> {
    const res = await fetch(url, {credentials: 'include'});
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

export const workflowKeys = {
    all: ['workflow-runs'] as const,
    list: (filters?: { status?: string }) =>
        [...workflowKeys.all, 'list', filters] as const,
    detail: (runId: string) => [...workflowKeys.all, 'detail', runId] as const,
    events: (runId: string) => [...workflowKeys.all, 'events', runId] as const,
    checkpoints: (runId: string) => [...workflowKeys.all, 'checkpoints', runId] as const,
};

export function useWorkflowRuns(filters?: { status?: string }) {
    return useQuery({
        queryKey: workflowKeys.list(filters),
        queryFn: () => {
            const params = new URLSearchParams();
            params.set('limit', '100');
            if (filters?.status) params.set('status', filters.status);
            return fetchJson<WorkflowRunsResponse>(`/api/v1/workflows/runs?${params}`);
        },
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useWorkflowRun(runId: string) {
    return useQuery({
        queryKey: workflowKeys.detail(runId),
        queryFn: () => fetchJson<WorkflowRun>(`/api/v1/workflows/runs/${runId}`),
        enabled: !!runId,
        staleTime: 30_000,
    });
}

export function useWorkflowEvents(runId: string) {
    return useQuery({
        queryKey: workflowKeys.events(runId),
        queryFn: () => fetchJson<WorkflowEventsResponse>(`/api/v1/workflows/runs/${runId}/events`),
        select: (data) => data.items,
        enabled: !!runId,
        staleTime: 30_000,
    });
}

export function useWorkflowCheckpoints(runId: string) {
    return useQuery({
        queryKey: workflowKeys.checkpoints(runId),
        queryFn: () => fetchJson<WorkflowCheckpointsResponse>(`/api/v1/workflows/runs/${runId}/checkpoints`),
        select: (data) => data.items,
        enabled: !!runId,
        staleTime: 30_000,
    });
}
