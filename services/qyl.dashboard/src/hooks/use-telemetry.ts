import {useCallback, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import type {SessionEntity, Span, Trace} from '@/types';
import {getAttributesRecord, nanoToIso, nsToMs, STATUS_ERROR,} from '@/types';
import {fetchJson} from '@/lib/api';

// Dashboard span type.
type TelemetrySpan = Span;

// Query keys
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

// API response types (actual API shape)
interface ApiSessionsResponse {
    items: SessionEntity[];
    total?: number;
}

interface ApiSpanPage {
    items: Span[];
    has_more: boolean;
}

interface ApiTracePage {
    items: Trace[];
    has_more: boolean;
}

// Sessions - return array directly for components
export function useSessions() {
    return useQuery({
        queryKey: telemetryKeys.sessions(),
        queryFn: () => fetchJson<ApiSessionsResponse>('/api/v1/sessions'),
        select: (data): SessionEntity[] => data.items,
        refetchInterval: 10000,
    });
}

export function useSessionSpans(sessionId: string) {
    return useQuery({
        queryKey: telemetryKeys.sessionSpans(sessionId),
        queryFn: () => fetchJson<ApiTracePage>(`/api/v1/sessions/${sessionId}/traces`),
        select: (data): TelemetrySpan[] => data.items.flatMap((trace) => trace.spans),
        enabled: !!sessionId,
    });
}

export function useTraceSpans(traceId: string) {
    return useQuery({
        queryKey: telemetryKeys.traceSpans(traceId),
        queryFn: () => fetchJson<ApiSpanPage>(`/api/v1/traces/${traceId}/spans`),
        select: (data): TelemetrySpan[] => data.items,
        enabled: !!traceId,
    });
}

// Project-wide recent traces, flattened to spans. Used as the TracesPage fallback when a session
// surfaces no retrievable traces (e.g. plain HTTP telemetry without a session join), so the
// waterfall stays populated instead of showing "No traces found".
export function useTraces(enabled = true) {
    return useQuery({
        queryKey: telemetryKeys.traces(),
        queryFn: () => fetchJson<ApiTracePage>('/api/v1/traces'),
        select: (data): TelemetrySpan[] => data.items.flatMap((trace) => trace.spans),
        enabled,
        refetchInterval: 10000,
    });
}

export type TraceViewSource = 'trace' | 'session' | 'all-traces';

// Decides which span source feeds the trace waterfall:
//   - 'trace'      a ?traceId= deep-link is present
//   - 'all-traces' the selected session resolved with zero traces (fallback)
//   - 'session'    default: the session's own traces (incl. while still loading)
export function selectTraceViewSource(args: {
    hasTraceId: boolean;
    sessionResolved: boolean;
    sessionSpanCount: number;
}): TraceViewSource {
    if (args.hasTraceId) return 'trace';
    if (args.sessionResolved && args.sessionSpanCount === 0) return 'all-traces';
    return 'session';
}

// Live SSE Stream
interface UseLiveStreamOptions {
    onConnect?: () => void;
    onDisconnect?: () => void;
    enabled?: boolean;
}

export function useLiveStream(options: UseLiveStreamOptions = {}) {
    const {onConnect, onDisconnect, enabled = true} = options;
    const [isConnected, setIsConnected] = useState(false);
    const [connectionId, setConnectionId] = useState<string | null>(null);
    const [recentSpans, setRecentSpans] = useState<TelemetrySpan[]>([]);

    const connect = useCallback(() => {
        if (!enabled) {
            setIsConnected(false);
            setConnectionId(null);
            onDisconnect?.();
            return;
        }

        setIsConnected(false);
        setConnectionId(null);
        onConnect?.();
    }, [enabled, onConnect, onDisconnect]);

    const disconnect = useCallback(() => {
        setIsConnected(false);
        setConnectionId(null);
        onDisconnect?.();
    }, [onDisconnect]);

    return {
        isConnected,
        connectionId,
        recentSpans,
        disconnect,
        reconnect: connect,
        clearSpans: () => setRecentSpans([]),
    };
}

// Span utilities.
export function getSpanColor(span: TelemetrySpan): string {
    const attrs = getAttributesRecord(span);
    // GenAI spans
    if (attrs['gen_ai.system'] || attrs['gen_ai.provider.name']) {
        return 'hsl(var(--span-genai))';
    }
    // HTTP spans
    if (attrs['http.method'] || attrs['http.request.method']) {
        return 'hsl(var(--span-http))';
    }
    // Database spans
    if (attrs['db.system']) {
        return 'hsl(var(--span-db))';
    }
    // Messaging spans
    if (attrs['messaging.system']) {
        return 'hsl(var(--span-message))';
    }
    // Default
    return 'hsl(var(--span-internal))';
}

export function getSpanTypeLabel(span: TelemetrySpan): string {
    const attrs = getAttributesRecord(span);
    if (attrs['gen_ai.system'] || attrs['gen_ai.provider.name']) {
        return 'GenAI';
    }
    if (attrs['http.method'] || attrs['http.request.method']) {
        return 'HTTP';
    }
    if (attrs['db.system']) {
        return 'Database';
    }
    if (attrs['messaging.system']) {
        return 'Message';
    }
    return 'Internal';
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

// Re-export utilities for convenience
export {getAttributesRecord, nanoToIso, nsToMs, STATUS_ERROR};
