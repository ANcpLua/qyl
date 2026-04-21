import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

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
    track_mode?: string;
    approval_status?: string;
    evidence_count?: number;
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

export interface AgentDecision {
    decision_id: string;
    run_id?: string;
    trace_id?: string;
    decision_type?: string;
    outcome?: string;
    requires_approval: boolean;
    approval_status?: string;
    reason?: string;
    evidence_json?: string;
    metadata_json?: string;
    created_at_unix_nano?: number;
}

interface AgentRunsResponse {
    items: unknown[];
    total: number;
}

interface ToolCallsResponse {
    items: unknown[];
    total: number;
}

interface AgentDecisionsResponse {
    items: unknown[];
    total: number;
}

export const agentRunKeys = {
    all: ['agent-runs'] as const,
    list: (filters?: {
        agentName?: string;
        status?: string;
        trackMode?: string;
        approvalStatus?: string;
    }) =>
        [...agentRunKeys.all, 'list', filters] as const,
    detail: (runId: string) => [...agentRunKeys.all, 'detail', runId] as const,
    tools: (runId: string) => [...agentRunKeys.all, 'tools', runId] as const,
    decisions: (runId: string) => [...agentRunKeys.all, 'decisions', runId] as const,
};

function pick<T>(value: unknown, ...keys: string[]): T | undefined {
    if (value === null || value === undefined || typeof value !== 'object') return undefined;
    const record = value as Record<string, unknown>;
    for (const key of keys) {
        const candidate = record[key];
        if (candidate !== null && candidate !== undefined) {
            return candidate as T;
        }
    }
    return undefined;
}

function toNumber(value: unknown, fallback = 0): number {
    if (typeof value === 'number' && Number.isFinite(value)) return value;
    if (typeof value === 'string') {
        const parsed = Number(value);
        if (Number.isFinite(parsed)) return parsed;
    }
    return fallback;
}

function normalizeAgentRun(value: unknown): AgentRun {
    return {
        run_id: String(pick(value, 'run_id', 'runId') ?? ''),
        trace_id: pick(value, 'trace_id', 'traceId'),
        parent_run_id: pick(value, 'parent_run_id', 'parentRunId'),
        agent_name: pick(value, 'agent_name', 'agentName'),
        agent_type: pick(value, 'agent_type', 'agentType'),
        model: pick(value, 'model'),
        provider: pick(value, 'provider'),
        status: String(pick(value, 'status') ?? 'unknown'),
        input_tokens: toNumber(pick(value, 'input_tokens', 'inputTokens')),
        output_tokens: toNumber(pick(value, 'output_tokens', 'outputTokens')),
        total_cost: toNumber(pick(value, 'total_cost', 'totalCost')),
        tool_call_count: toNumber(pick(value, 'tool_call_count', 'toolCallCount')),
        start_time: pick(value, 'start_time', 'startTime'),
        end_time: pick(value, 'end_time', 'endTime'),
        duration_ns: pick(value, 'duration_ns', 'durationNs'),
        error_message: pick(value, 'error_message', 'errorMessage'),
        metadata_json: pick(value, 'metadata_json', 'metadataJson'),
        track_mode: pick(value, 'track_mode', 'trackMode'),
        approval_status: pick(value, 'approval_status', 'approvalStatus'),
        evidence_count: toNumber(pick(value, 'evidence_count', 'evidenceCount')),
    };
}

function normalizeToolCall(value: unknown): ToolCall {
    return {
        call_id: String(pick(value, 'call_id', 'callId') ?? ''),
        run_id: String(pick(value, 'run_id', 'runId') ?? ''),
        trace_id: pick(value, 'trace_id', 'traceId'),
        span_id: pick(value, 'span_id', 'spanId'),
        tool_name: pick(value, 'tool_name', 'toolName'),
        tool_type: pick(value, 'tool_type', 'toolType'),
        arguments_json: pick(value, 'arguments_json', 'argumentsJson'),
        result_json: pick(value, 'result_json', 'resultJson'),
        status: String(pick(value, 'status') ?? 'unknown'),
        start_time: pick(value, 'start_time', 'startTime'),
        end_time: pick(value, 'end_time', 'endTime'),
        duration_ns: pick(value, 'duration_ns', 'durationNs'),
        error_message: pick(value, 'error_message', 'errorMessage'),
        sequence_number: toNumber(pick(value, 'sequence_number', 'sequenceNumber')),
    };
}

function normalizeDecision(value: unknown): AgentDecision {
    return {
        decision_id: String(pick(value, 'decision_id', 'decisionId') ?? ''),
        run_id: pick(value, 'run_id', 'runId'),
        trace_id: pick(value, 'trace_id', 'traceId'),
        decision_type: pick(value, 'decision_type', 'decisionType'),
        outcome: pick(value, 'outcome'),
        requires_approval: Boolean(pick(value, 'requires_approval', 'requiresApproval')),
        approval_status: pick(value, 'approval_status', 'approvalStatus'),
        reason: pick(value, 'reason'),
        evidence_json: pick(value, 'evidence_json', 'evidenceJson'),
        metadata_json: pick(value, 'metadata_json', 'metadataJson'),
        created_at_unix_nano: pick(value, 'created_at_unix_nano', 'createdAtUnixNano'),
    };
}

export function useAgentRuns(filters?: {
    agentName?: string;
    status?: string;
    trackMode?: string;
    approvalStatus?: string;
}) {
    return useQuery({
        queryKey: agentRunKeys.list(filters),
        queryFn: async () => {
            const params = new URLSearchParams();
            params.set('limit', '100');
            if (filters?.agentName) params.set('agentName', filters.agentName);
            if (filters?.status) params.set('status', filters.status);
            if (filters?.trackMode) params.set('trackMode', filters.trackMode);
            if (filters?.approvalStatus) params.set('approvalStatus', filters.approvalStatus);
            const response = await fetchJson<AgentRunsResponse>(`/api/v1/agent-runs?${params}`);
            return response.items.map(normalizeAgentRun);
        },
        staleTime: 30_000,
    });
}

export function useAgentRun(runId: string) {
    return useQuery({
        queryKey: agentRunKeys.detail(runId),
        queryFn: async () => {
            const item = await fetchJson<unknown>(`/api/v1/agent-runs/${runId}`);
            return normalizeAgentRun(item);
        },
        enabled: !!runId,
        staleTime: 30_000,
    });
}

export function useToolCalls(runId: string) {
    return useQuery({
        queryKey: agentRunKeys.tools(runId),
        queryFn: async () => {
            const response = await fetchJson<ToolCallsResponse>(`/api/v1/agent-runs/${runId}/tools`);
            return response.items.map(normalizeToolCall);
        },
        enabled: !!runId,
        staleTime: 30_000,
    });
}

export function useAgentDecisions(runId: string) {
    return useQuery({
        queryKey: agentRunKeys.decisions(runId),
        queryFn: async () => {
            const response = await fetchJson<AgentDecisionsResponse>(`/api/v1/agent-runs/${runId}/decisions`);
            return response.items.map(normalizeDecision);
        },
        enabled: !!runId,
        staleTime: 30_000,
    });
}
