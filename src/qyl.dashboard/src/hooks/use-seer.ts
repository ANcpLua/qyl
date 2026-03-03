import {useMutation, useQuery, useQueryClient} from '@tanstack/react-query';
import {fetchJson, postJson} from '@/lib/api';

export interface TriageResult {
    triage_id: string;
    issue_id: string;
    fixability_score: number;
    automation_level: 'auto' | 'assisted' | 'manual' | 'skip';
    ai_summary?: string;
    triggered_by?: string;
    created_at: string;
}

export interface AutofixStep {
    step_id: string;
    run_id: string;
    step_number: number;
    step_name: string;
    status: string;
    input_json?: string;
    output_json?: string;
    error_message?: string;
    started_at?: string;
    completed_at?: string;
    created_at?: string;
}

export interface RegressionEvent {
    eventId: string;
    issueId: string;
    oldValue?: string;
    newValue?: string;
    reason?: string;
    createdAt?: string;
}

export interface AgentHandoff {
    handoff_id: string;
    run_id: string;
    agent_type: string;
    status: 'pending' | 'accepted' | 'completed' | 'failed' | 'timed_out';
    context_json?: string;
    result_json?: string;
    error_message?: string;
    accepted_at?: string;
    submitted_at?: string;
    failed_at?: string;
    timeout_at?: string;
    created_at?: string;
}

export interface CodeReviewResult {
    repoFullName: string;
    prNumber: number;
    comments: CodeReviewComment[];
    reviewed: boolean;
}

export interface CodeReviewComment {
    file: string;
    line: number;
    severity: 'critical' | 'warning' | 'suggestion';
    comment: string;
    suggestion?: string;
}

export interface GitHubEvent {
    eventId: string;
    eventType: string;
    action?: string;
    repoFullName: string;
    sender?: string;
    prNumber?: number;
    prUrl?: string;
    ref?: string;
    createdAt?: string;
}

interface TriageListResponse {
    items: TriageResult[];
    total: number;
}

interface AutofixStepsResponse {
    items: AutofixStep[];
    total: number;
}

interface RegressionsResponse {
    items: RegressionEvent[];
    total: number;
}

interface HandoffsResponse {
    items: AgentHandoff[];
    total: number;
}

interface GitHubEventsResponse {
    items: GitHubEvent[];
    total: number;
}

export const seerKeys = {
    triage: (issueId: string) => ['seer', 'triage', issueId] as const,
    triageList: (limit?: number) => ['seer', 'triage', 'list', limit] as const,
    fixRunSteps: (issueId: string, runId: string) => ['seer', 'steps', issueId, runId] as const,
    regressions: (limit?: number) => ['seer', 'regressions', limit] as const,
    issueRegressions: (issueId: string) => ['seer', 'regressions', 'issue', issueId] as const,
    handoffs: ['seer', 'handoffs'] as const,
    pendingHandoffs: ['seer', 'handoffs', 'pending'] as const,
    handoffDetail: (id: string) => ['seer', 'handoffs', id] as const,
    githubEvents: (limit?: number) => ['seer', 'github-events', limit] as const,
};

export function useTriageResult(issueId?: string) {
    return useQuery({
        queryKey: seerKeys.triage(issueId!),
        queryFn: () => fetchJson<TriageResult>(`/api/v1/issues/${issueId}/triage`),
        enabled: !!issueId,
        staleTime: 30_000,
    });
}

export function useTriageResults(limit?: number) {
    return useQuery({
        queryKey: seerKeys.triageList(limit),
        queryFn: () => fetchJson<TriageListResponse>(`/api/v1/triage${limit != null ? `?limit=${limit}` : ''}`),
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useFixRunSteps(issueId?: string, runId?: string) {
    return useQuery({
        queryKey: seerKeys.fixRunSteps(issueId!, runId!),
        queryFn: () => fetchJson<AutofixStepsResponse>(`/api/v1/issues/${issueId}/fix-runs/${runId}/steps`),
        select: (data) => data.items,
        enabled: !!issueId && !!runId,
        staleTime: 30_000,
    });
}

export function useRegressions(limit?: number) {
    return useQuery({
        queryKey: seerKeys.regressions(limit),
        queryFn: () => fetchJson<RegressionsResponse>(`/api/v1/regressions${limit != null ? `?limit=${limit}` : ''}`),
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useIssueRegressions(issueId?: string) {
    return useQuery({
        queryKey: seerKeys.issueRegressions(issueId!),
        queryFn: () => fetchJson<RegressionsResponse>(`/api/v1/issues/${issueId}/regressions`),
        select: (data) => data.items,
        enabled: !!issueId,
        staleTime: 30_000,
    });
}

export function usePendingHandoffs() {
    return useQuery({
        queryKey: seerKeys.pendingHandoffs,
        queryFn: () => fetchJson<HandoffsResponse>('/api/v1/handoffs/pending'),
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useHandoff(handoffId?: string) {
    return useQuery({
        queryKey: seerKeys.handoffDetail(handoffId!),
        queryFn: () => fetchJson<AgentHandoff>(`/api/v1/handoffs/${handoffId}`),
        enabled: !!handoffId,
        staleTime: 30_000,
    });
}

export function useTriggerCodeReview() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({repo, prNumber}: {
            repo: string;
            prNumber: number;
        }) => postJson<CodeReviewResult>(`/api/v1/code-review/${repo}/pulls/${prNumber}`, {}),
        onSettled: () => {
            queryClient.invalidateQueries({queryKey: seerKeys.handoffs});
        },
    });
}

export function useGitHubEvents(limit?: number) {
    return useQuery({
        queryKey: seerKeys.githubEvents(limit),
        queryFn: () => fetchJson<GitHubEventsResponse>(`/api/v1/github/events${limit != null ? `?limit=${limit}` : ''}`),
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useCheckRegressions() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({serviceName}: {
            serviceName: string;
        }) => postJson<RegressionEvent[]>(`/api/v1/regressions/check/${serviceName}`, {}),
        onSettled: () => {
            queryClient.invalidateQueries({queryKey: seerKeys.regressions()});
        },
    });
}
