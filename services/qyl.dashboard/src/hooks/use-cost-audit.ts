import {useMutation, useQuery} from '@tanstack/react-query';
import type {GenAiEtlAuditEvaluationRequest} from '@/types';
import {evaluateGenAiEtlAudit, fetchGenAiEtlAudit} from '@/lib/api';

export const costAuditKeys = {
    all: ['cost', 'etl-audit'] as const,
    report: (startTime: string, endTime: string, limit: number, projectScope?: string) =>
        [...costAuditKeys.all, 'report', startTime, endTime, limit, projectScope ?? null] as const,
};

export function normalizeProjectScope(value: string | undefined): string | undefined {
    const normalized = value?.trim();
    return normalized || undefined;
}

export function completedUtcDayRange(now = new Date(), days = 30): { startTime: string; endTime: string } {
    const end = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
    const start = new Date(end);
    start.setUTCDate(start.getUTCDate() - days);
    return {startTime: start.toISOString(), endTime: end.toISOString()};
}

export function useGenAiEtlAudit(limit = 25, requestedProjectScope?: string) {
    const {startTime, endTime} = completedUtcDayRange();
    const projectScope = normalizeProjectScope(requestedProjectScope);
    return useQuery({
        queryKey: costAuditKeys.report(startTime, endTime, limit, projectScope),
        queryFn: () => fetchGenAiEtlAudit(startTime, endTime, limit, projectScope),
        refetchInterval: 60_000,
    });
}

export function useEvaluateGenAiEtlAudit() {
    return useMutation({
        mutationFn: ({request, startTime, endTime, projectScope}: {
            request: GenAiEtlAuditEvaluationRequest;
            startTime: string;
            endTime: string;
            projectScope?: string;
        }) => evaluateGenAiEtlAudit(request, startTime, endTime, normalizeProjectScope(projectScope)),
    });
}
