import {useQuery} from '@tanstack/react-query';

// ── Fetch utility ──────────────────────────────────────────────────────────────

async function fetchJson<T>(url: string): Promise<T> {
    const res = await fetch(url, {credentials: 'include'});
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

// ── Shared filter types ────────────────────────────────────────────────────────

export interface TimeFilter {
    from: number; // unix ms
    to: number;   // unix ms
    bucket?: string; // auto | 1h | 1d | 1w
}

function buildParams(filter: TimeFilter, extra?: Record<string, string | number>): string {
    const params = new URLSearchParams();
    params.set('from', String(filter.from));
    params.set('to', String(filter.to));
    if (filter.bucket) params.set('bucket', filter.bucket);
    if (extra) {
        for (const [k, v] of Object.entries(extra)) {
            params.set(k, String(v));
        }
    }
    return params.toString();
}

// ── Response types ─────────────────────────────────────────────────────────────

export interface TrafficBucket {
    time: string;
    runs: number;
    errors: number;
    errorRate: number;
}

export interface TrafficResponse {
    buckets: TrafficBucket[];
}

export interface DurationBucket {
    time: string;
    avgMs: number;
    p95Ms: number;
}

export interface DurationResponse {
    buckets: DurationBucket[];
}

export interface IssueItem {
    error: string;
    count: number;
    sampleTraceIds: string[];
}

export interface IssuesResponse {
    issues: IssueItem[];
}

export interface ModelBucket {
    time: string;
    models: Record<string, number>;
}

export interface LegendEntry {
    name: string;
    total: number;
}

export interface LlmCallsResponse {
    buckets: ModelBucket[];
    legend: LegendEntry[];
}

export interface TokensResponse {
    buckets: ModelBucket[];
    legend: LegendEntry[];
}

export interface ToolBucket {
    time: string;
    tools: Record<string, number>;
}

export interface ToolCallsResponse {
    buckets: ToolBucket[];
    legend: LegendEntry[];
}

export interface AgentTrace {
    traceId: string;
    timestamp: string;
    rootDurationMs: number;
    errors: number;
    llmCalls: number;
    toolCalls: number;
    totalTokens: number;
    totalCost: number;
    rootName: string;
    agentName: string;
}

export interface TracesResponse {
    items: AgentTrace[];
    total: number;
}

export interface TraceSpan {
    spanId: string;
    parentSpanId: string | null;
    name: string;
    timestamp: string;
    durationMs: number;
    statusCode: number;
    statusMessage: string | null;
    provider: string | null;
    model: string | null;
    inputTokens: number | null;
    outputTokens: number | null;
    toolName: string | null;
    cost: number | null;
    stopReason: string | null;
    attributesJson: string | null;
}

export interface TraceSpansResponse {
    spans: TraceSpan[];
}

export interface ModelSummary {
    name: string;
    calls: number;
    inputTokens: number;
    outputTokens: number;
    cost: number;
    avgDurationMs: number;
    errorRate: number;
}

export interface ModelsResponse {
    models: ModelSummary[];
    timeseries: ModelBucket[];
    legend: LegendEntry[];
}

export interface ToolSummary {
    name: string;
    calls: number;
    avgDurationMs: number;
    errorRate: number;
}

export interface ToolsResponse {
    tools: ToolSummary[];
    timeseries: ToolBucket[];
    legend: LegendEntry[];
}

// ── Query keys ─────────────────────────────────────────────────────────────────

export const agentInsightKeys = {
    all: ['agent-insights'] as const,
    traffic: (f: TimeFilter) => [...agentInsightKeys.all, 'traffic', f] as const,
    duration: (f: TimeFilter) => [...agentInsightKeys.all, 'duration', f] as const,
    issues: (f: TimeFilter) => [...agentInsightKeys.all, 'issues', f] as const,
    llmCalls: (f: TimeFilter) => [...agentInsightKeys.all, 'llm-calls', f] as const,
    tokens: (f: TimeFilter) => [...agentInsightKeys.all, 'tokens', f] as const,
    toolCalls: (f: TimeFilter) => [...agentInsightKeys.all, 'tool-calls', f] as const,
    traces: (f: TimeFilter, limit: number, offset: number) =>
        [...agentInsightKeys.all, 'traces', f, limit, offset] as const,
    traceSpans: (traceId: string) => [...agentInsightKeys.all, 'trace-spans', traceId] as const,
    models: (f: TimeFilter) => [...agentInsightKeys.all, 'models', f] as const,
    tools: (f: TimeFilter) => [...agentInsightKeys.all, 'tools', f] as const,
};

// ── Hooks ──────────────────────────────────────────────────────────────────────

export function useAgentTraffic(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.traffic(filter),
        queryFn: () => fetchJson<TrafficResponse>(
            `/api/v1/agents/overview/traffic?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}

export function useAgentDuration(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.duration(filter),
        queryFn: () => fetchJson<DurationResponse>(
            `/api/v1/agents/overview/duration?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}

export function useAgentIssues(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.issues(filter),
        queryFn: () => fetchJson<IssuesResponse>(
            `/api/v1/agents/overview/issues?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}

export function useAgentLlmCalls(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.llmCalls(filter),
        queryFn: () => fetchJson<LlmCallsResponse>(
            `/api/v1/agents/overview/llm-calls?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}

export function useAgentTokens(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.tokens(filter),
        queryFn: () => fetchJson<TokensResponse>(
            `/api/v1/agents/overview/tokens?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}

export function useAgentToolCalls(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.toolCalls(filter),
        queryFn: () => fetchJson<ToolCallsResponse>(
            `/api/v1/agents/overview/tool-calls?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}

export function useAgentTraces(filter: TimeFilter, limit = 50, offset = 0) {
    return useQuery({
        queryKey: agentInsightKeys.traces(filter, limit, offset),
        queryFn: () => fetchJson<TracesResponse>(
            `/api/v1/agents/traces?${buildParams(filter, {limit, offset})}`
        ),
        staleTime: 30_000,
    });
}

export function useTraceSpans(traceId: string | null) {
    return useQuery({
        queryKey: agentInsightKeys.traceSpans(traceId ?? ''),
        queryFn: () => fetchJson<TraceSpansResponse>(
            `/api/v1/agents/traces/${traceId}/spans`
        ),
        enabled: !!traceId,
        staleTime: 60_000,
    });
}

export function useAgentModels(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.models(filter),
        queryFn: () => fetchJson<ModelsResponse>(
            `/api/v1/agents/models?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}

export function useAgentTools(filter: TimeFilter) {
    return useQuery({
        queryKey: agentInsightKeys.tools(filter),
        queryFn: () => fetchJson<ToolsResponse>(
            `/api/v1/agents/tools?${buildParams(filter)}`
        ),
        staleTime: 30_000,
    });
}
