import {useMutation, useQuery, useQueryClient} from '@tanstack/react-query';
import {fetchJson, postJson} from '@/lib/api';

export interface CodingAgentRun {
    id: string;
    fix_run_id: string;
    provider: string;
    status: 'pending' | 'running' | 'completed' | 'failed';
    agent_url?: string;
    pr_url?: string;
    repo_full_name?: string;
    created_at: string;
    completed_at?: string;
}

export interface LoomSettings {
    id: string;
    default_coding_agent: string;
    default_coding_agent_integration_id?: string;
    automation_tuning: string;
    updated_at: string;
}

interface CodingAgentRunsResponse {
    items: CodingAgentRun[];
    total: number;
}

export interface FixRun {
    run_id: string;
    issue_id: string;
    execution_id?: string;
    status: string;
    policy: string;
    fix_description?: string;
    confidence_score?: number;
    changes_json?: string;
    created_at: string;
    completed_at?: string;
}

interface FixRunsResponse {
    items: FixRun[];
    total: number;
}

export const CODING_AGENT_PROVIDERS = [
    {value: 'Loom', label: 'Loom (built-in)', description: 'Default autofix pipeline'},
    {value: 'cursor', label: 'Cursor', description: 'Cursor Background Agent'},
    {value: 'github_copilot', label: 'GitHub Copilot', description: 'Copilot Coding Agent'},
    {value: 'claude_code', label: 'Claude Code', description: 'Anthropic Claude Code agent'},
] as const;

export const codingAgentKeys = {
    all: ['coding-agents'] as const,
    forFixRun: (fixRunId: string) => [...codingAgentKeys.all, 'fix-run', fixRunId] as const,
    detail: (id: string) => [...codingAgentKeys.all, 'detail', id] as const,
    fixRuns: (issueId: string) => ['fix-runs', issueId] as const,
    settings: ['Loom-settings'] as const,
};

export function useFixRuns(issueId?: string) {
    return useQuery({
        queryKey: codingAgentKeys.fixRuns(issueId!),
        queryFn: () => fetchJson<FixRunsResponse>(`/api/v1/issues/${issueId}/fix-runs`),
        select: (data) => data.items,
        enabled: !!issueId,
        staleTime: 30_000,
    });
}

export function useCodingAgentRuns(fixRunId?: string) {
    return useQuery({
        queryKey: codingAgentKeys.forFixRun(fixRunId!),
        queryFn: () => fetchJson<CodingAgentRunsResponse>(`/api/v1/fix-runs/${fixRunId}/coding-agents`),
        select: (data) => data.items,
        enabled: !!fixRunId,
        staleTime: 30_000,
    });
}

export function useLaunchCodingAgent() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({fixRunId, provider, repoFullName}: {
            fixRunId: string;
            provider?: string;
            repoFullName?: string;
        }) => postJson<CodingAgentRun>(`/api/v1/fix-runs/${fixRunId}/coding-agents`, {
            provider,
            repo_full_name: repoFullName,
        }),
        onSettled: (_data, _err, {fixRunId}) => {
            queryClient.invalidateQueries({queryKey: codingAgentKeys.forFixRun(fixRunId)});
        },
    });
}

export function useLoomSettings() {
    return useQuery({
        queryKey: codingAgentKeys.settings,
        queryFn: () => fetchJson<LoomSettings>('/api/v1/Loom/settings'),
        staleTime: 60_000,
    });
}

export function useUpdateLoomSettings() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (settings: {
            default_coding_agent?: string;
            default_coding_agent_integration_id?: string;
            automation_tuning?: string;
        }) => fetchJson<LoomSettings>('/api/v1/Loom/settings', {
            method: 'PUT',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify(settings),
        }),
        onSettled: () => {
            queryClient.invalidateQueries({queryKey: codingAgentKeys.settings});
        },
    });
}

export function getProviderLabel(provider: string): string {
    return CODING_AGENT_PROVIDERS.find(p => p.value === provider)?.label ?? provider;
}

export function getProviderButtonText(provider: string): string {
    switch (provider) {
        case 'cursor':
            return 'Open in Cursor';
        case 'claude_code':
            return 'Open in Claude Code';
        case 'github_copilot':
            return 'Open in GitHub Copilot';
        default:
            return 'Open PR';
    }
}
