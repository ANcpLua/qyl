import {useQuery, useMutation, useQueryClient} from '@tanstack/react-query';

export interface Issue {
    issue_id: string;
    error_type: string;
    message: string;
    status: string;
    event_count: number;
    first_seen: string;
    last_seen: string;
    owner?: string;
    trace_id?: string;
    metadata_json?: string;
}

export interface IssueEvent {
    event_id: string;
    issue_id: string;
    event_type: string;
    timestamp: string;
    old_value?: string;
    new_value?: string;
    reason?: string;
    actor?: string;
    trace_id?: string;
}

interface IssuesResponse {
    items: Issue[];
    total: number;
}

interface IssueEventsResponse {
    items: IssueEvent[];
    total: number;
}

async function fetchJson<T>(url: string): Promise<T> {
    const res = await fetch(url, {credentials: 'include'});
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
    const res = await fetch(url, {
        method: 'POST',
        credentials: 'include',
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify(body),
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

export const issueKeys = {
    all: ['issues'] as const,
    list: (filters?: {status?: string; errorType?: string}) =>
        [...issueKeys.all, 'list', filters] as const,
    detail: (issueId: string) => [...issueKeys.all, 'detail', issueId] as const,
    events: (issueId: string) => [...issueKeys.all, 'events', issueId] as const,
};

export function useIssues(filters?: {status?: string; errorType?: string}) {
    return useQuery({
        queryKey: issueKeys.list(filters),
        queryFn: () => {
            const params = new URLSearchParams();
            params.set('limit', '100');
            if (filters?.status) params.set('status', filters.status);
            if (filters?.errorType) params.set('errorType', filters.errorType);
            return fetchJson<IssuesResponse>(`/api/v1/issues?${params}`);
        },
        select: (data) => data.items,
        staleTime: 30_000,
    });
}

export function useIssue(issueId: string) {
    return useQuery({
        queryKey: issueKeys.detail(issueId),
        queryFn: () => fetchJson<Issue>(`/api/v1/issues/${issueId}`),
        enabled: !!issueId,
        staleTime: 30_000,
    });
}

export function useIssueEvents(issueId: string) {
    return useQuery({
        queryKey: issueKeys.events(issueId),
        queryFn: () => fetchJson<IssueEventsResponse>(`/api/v1/issues/${issueId}/events`),
        select: (data) => data.items,
        enabled: !!issueId,
        staleTime: 30_000,
    });
}

export function useUpdateIssueStatus() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({issueId, status, reason}: {issueId: string; status: string; reason?: string}) =>
            postJson<Issue>(`/api/v1/issues/${issueId}/status`, {status, reason}),
        onMutate: async ({issueId, status}) => {
            await queryClient.cancelQueries({queryKey: issueKeys.detail(issueId)});
            const previous = queryClient.getQueryData<Issue>(issueKeys.detail(issueId));
            if (previous) {
                queryClient.setQueryData(issueKeys.detail(issueId), {...previous, status});
            }
            return {previous};
        },
        onError: (_err, {issueId}, context) => {
            if (context?.previous) {
                queryClient.setQueryData(issueKeys.detail(issueId), context.previous);
            }
        },
        onSettled: (_data, _err, {issueId}) => {
            queryClient.invalidateQueries({queryKey: issueKeys.detail(issueId)});
            queryClient.invalidateQueries({queryKey: issueKeys.list()});
            queryClient.invalidateQueries({queryKey: issueKeys.events(issueId)});
        },
    });
}

export function useAssignIssue() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({issueId, owner}: {issueId: string; owner: string}) =>
            postJson<Issue>(`/api/v1/issues/${issueId}/assign`, {owner}),
        onMutate: async ({issueId, owner}) => {
            await queryClient.cancelQueries({queryKey: issueKeys.detail(issueId)});
            const previous = queryClient.getQueryData<Issue>(issueKeys.detail(issueId));
            if (previous) {
                queryClient.setQueryData(issueKeys.detail(issueId), {...previous, owner});
            }
            return {previous};
        },
        onError: (_err, {issueId}, context) => {
            if (context?.previous) {
                queryClient.setQueryData(issueKeys.detail(issueId), context.previous);
            }
        },
        onSettled: (_data, _err, {issueId}) => {
            queryClient.invalidateQueries({queryKey: issueKeys.detail(issueId)});
            queryClient.invalidateQueries({queryKey: issueKeys.list()});
        },
    });
}
