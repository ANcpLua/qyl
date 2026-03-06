import {useMutation, useQuery, useQueryClient} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

interface ClaudeCodeAttachStatus {
    attached: boolean;
}

export const claudeCodeHooksKeys = {
    status: ['claude-code-hooks-status'] as const,
};

export function useClaudeCodeHooksStatus() {
    return useQuery({
        queryKey: claudeCodeHooksKeys.status,
        queryFn: () => fetchJson<ClaudeCodeAttachStatus>('/api/v1/claude-code/attach'),
        staleTime: 30_000,
    });
}

export function useAttachClaudeCodeHooks() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => fetchJson<ClaudeCodeAttachStatus>('/api/v1/claude-code/attach', {method: 'POST'}),
        onSettled: () => {
            queryClient.invalidateQueries({queryKey: claudeCodeHooksKeys.status});
        },
    });
}

export function useDetachClaudeCodeHooks() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => fetchJson<ClaudeCodeAttachStatus>('/api/v1/claude-code/attach', {method: 'DELETE'}),
        onSettled: () => {
            queryClient.invalidateQueries({queryKey: claudeCodeHooksKeys.status});
        },
    });
}
