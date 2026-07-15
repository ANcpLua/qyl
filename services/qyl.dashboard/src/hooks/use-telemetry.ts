import {useQuery} from '@tanstack/react-query';
import type {
    SessionEntity,
    Span,
} from '@/types';
import {getAttributesRecord, nanoToIso, nsToMs, STATUS_ERROR,} from '@/types';
import {fetchJson} from '@/lib/api';
import {parseSessionPage, parseSpanPage, parseTracePage} from '@/lib/contract-validation';

type TelemetrySpan = Span;

export const telemetryKeys = {
    all: ['telemetry'] as const,
    sessions: () => [...telemetryKeys.all, 'sessions'] as const,
    session: (id: string) => [...telemetryKeys.sessions(), id] as const,
    sessionSpans: (id: string) => [...telemetryKeys.session(id), 'traces'] as const,
    traces: () => [...telemetryKeys.all, 'traces'] as const,
    traceSpans: (id: string) => [...telemetryKeys.all, 'trace', id, 'spans'] as const,
    logs: () => [...telemetryKeys.all, 'logs'] as const,
    metrics: () => [...telemetryKeys.all, 'metrics'] as const,
};

export function useSessions() {
    return useQuery({
        queryKey: telemetryKeys.sessions(),
        queryFn: () => fetchJson('/api/v1/sessions', parseSessionPage),
        select: (data): SessionEntity[] => data.items,
        refetchInterval: 10000,
    });
}

export function useSessionSpans(sessionId: string) {
    return useQuery({
        queryKey: telemetryKeys.sessionSpans(sessionId),
        queryFn: () => fetchJson(
            `/api/v1/sessions/${sessionId}/traces`,
            value => parseTracePage(value, `/api/v1/sessions/${sessionId}/traces`),
        ),
        select: (data): TelemetrySpan[] => data.items.flatMap((trace) => trace.spans),
        enabled: !!sessionId,
    });
}

export function useTraceSpans(traceId: string) {
    return useQuery({
        queryKey: telemetryKeys.traceSpans(traceId),
        queryFn: () => fetchJson(
            `/api/v1/traces/${traceId}/spans`,
            value => parseSpanPage(value, `/api/v1/traces/${traceId}/spans`),
        ),
        select: (data): TelemetrySpan[] => data.items,
        enabled: !!traceId,
    });
}

export function useTraces(enabled = true) {
    return useQuery({
        queryKey: telemetryKeys.traces(),
        queryFn: () => fetchJson('/api/v1/traces', parseTracePage),
        select: (data): TelemetrySpan[] => data.items.flatMap((trace) => trace.spans),
        enabled,
        refetchInterval: 10000,
    });
}

export type TraceViewSource = 'trace' | 'session' | 'all-traces';

export function selectTraceViewSource(args: {
    hasTraceId: boolean;
    sessionResolved: boolean;
    sessionSpanCount: number;
}): TraceViewSource {
    if (args.hasTraceId) return 'trace';
    if (args.sessionResolved && args.sessionSpanCount === 0) return 'all-traces';
    return 'session';
}

export function getSpanColor(span: TelemetrySpan): string {
    const attrs = getAttributesRecord(span);
    if (attrs['gen_ai.provider.name']) {
        return 'hsl(var(--span-genai))';
    }
    if (attrs['http.method'] || attrs['http.request.method']) {
        return 'hsl(var(--span-http))';
    }
    if (hasDatabaseSystem(attrs)) {
        return 'hsl(var(--span-db))';
    }
    if (attrs['messaging.system']) {
        return 'hsl(var(--span-message))';
    }
    // Shipping instrumentations still emit the pre-1.43 rpc.system key.
    if (attrs['rpc.system.name'] || attrs['rpc.system']) {
        return 'hsl(var(--span-rpc))';
    }
    return 'hsl(var(--span-internal))';
}

export function getSpanTypeLabel(span: TelemetrySpan): string {
    const attrs = getAttributesRecord(span);
    if (attrs['gen_ai.provider.name']) {
        return 'GenAI';
    }
    if (attrs['http.method'] || attrs['http.request.method']) {
        return 'HTTP';
    }
    if (hasDatabaseSystem(attrs)) {
        return 'Database';
    }
    if (attrs['messaging.system']) {
        return 'Message';
    }
    if (attrs['rpc.system.name'] || attrs['rpc.system']) {
        return 'RPC';
    }
    return 'Internal';
}

export function hasDatabaseSystem(attributes: Record<string, unknown>): boolean {
    return Boolean(attributes['db.system.name'] ?? attributes['db.system']);
}

export function formatDuration(ms: number): string {
    if (ms < 1) return `${(ms * 1000).toFixed(0)}μs`;
    if (ms < 1000) return `${ms.toFixed(1)}ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`;
    return `${(ms / 60000).toFixed(1)}m`;
}

export function formatTimestamp(iso: string): string {
    const date = new Date(iso);
    return date.toLocaleTimeString('en-US', {
        hour12: false,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        fractionalSecondDigits: 3,
    });
}

export {getAttributesRecord, nanoToIso, nsToMs, STATUS_ERROR};
