import {useCallback, useEffect, useRef, useState} from 'react';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import type {SessionEntity, Span} from '@/types';
import {getAttributesRecord, nanoToIso, nsToMs, STATUS_ERROR,} from '@/types';
import {fetchJson} from '@/lib/api';

// Alias for backward compatibility
type SpanRecord = Span;

// Query keys
export const telemetryKeys = {
    all: ['telemetry'] as const,
    sessions: () => [...telemetryKeys.all, 'sessions'] as const,
    session: (id: string) => [...telemetryKeys.sessions(), id] as const,
    sessionSpans: (id: string) => [...telemetryKeys.session(id), 'spans'] as const,
    logs: () => [...telemetryKeys.all, 'logs'] as const,
    metrics: () => [...telemetryKeys.all, 'metrics'] as const,
};

// API response types (actual API shape)
interface ApiSessionsResponse {
    items: SessionEntity[];
    total?: number;
}

interface ApiSpansResponse {
    items: SpanRecord[];
    total?: number;
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
        queryFn: () => fetchJson<ApiSpansResponse>(`/api/v1/sessions/${sessionId}/spans`),
        select: (data): SpanRecord[] => data.items,
        enabled: !!sessionId,
    });
}

// Live SSE Stream
interface UseLiveStreamOptions {
    sessionFilter?: string;
    onSpans?: (spans: SpanRecord[]) => void;
    onConnect?: () => void;
    onDisconnect?: () => void;
    enabled?: boolean;
}

export function useLiveStream(options: UseLiveStreamOptions = {}) {
    const {sessionFilter, onSpans, onConnect, onDisconnect, enabled = true} = options;

    const queryClient = useQueryClient();
    const eventSourceRef = useRef<EventSource | null>(null);
    const reconnectTimeoutRef = useRef<number | null>(null);
    const [isConnected, setIsConnected] = useState(false);
    const [connectionId, setConnectionId] = useState<string | null>(null);
    const [recentSpans, setRecentSpans] = useState<SpanRecord[]>([]);

    const connect = useCallback(() => {
        if (!enabled) return;

        if (eventSourceRef.current) {
            eventSourceRef.current.close();
        }

        const url = sessionFilter
            ? `/api/v1/live?session=${encodeURIComponent(sessionFilter)}`
            : '/api/v1/live';

        const eventSource = new EventSource(url);
        eventSourceRef.current = eventSource;
        let connectedNotified = false;

        const markConnected = (id?: string | null) => {
            if (id) setConnectionId(id);
            setIsConnected(true);
            if (connectedNotified) return;
            connectedNotified = true;
            onConnect?.();
        };

        const safeParseJson = (raw: string): unknown => {
            try {
                return JSON.parse(raw);
            } catch {
                return null;
            }
        };

        const parseEnvelope = (raw: string): { eventType: string; payload: unknown } | null => {
            const parsed = safeParseJson(raw);
            if (!parsed || typeof parsed !== 'object')
                return null;

            const outer = parsed as { eventType?: unknown; data?: unknown };
            if (typeof outer.eventType !== 'string')
                return null;

            // .NET TypedResults.ServerSentEvents wraps payloads as:
            // { eventType, data: { eventType, data, timestamp } }
            if (outer.data && typeof outer.data === 'object')
            {
                const inner = outer.data as { eventType?: unknown; data?: unknown };
                if (typeof inner.eventType === 'string')
                {
                    return {
                        eventType: inner.eventType.toLowerCase(),
                        payload: inner.data
                    };
                }
            }

            return {
                eventType: outer.eventType.toLowerCase(),
                payload: outer.data
            };
        };

        const handleSpansPayload = (payload: unknown) => {
            const data = payload as
                | SpanRecord[]
                | { spans?: SpanRecord[]; items?: SpanRecord[] }
                | null
                | undefined;
            const records = Array.isArray(data)
                ? data
                : data?.spans ?? data?.items ?? [];

            if (records.length === 0)
                return;

            onSpans?.(records);

            // Update recent spans (keep last 100)
            setRecentSpans((prev) => {
                const updated = [...records, ...prev];
                return updated.slice(0, 100);
            });

            // Invalidate relevant queries
            queryClient.invalidateQueries({queryKey: telemetryKeys.sessions()});
        };

        const dispatchEvent = (eventType: string, payload: unknown) => {
            switch (eventType.toLowerCase()) {
                case 'connected': {
                    const data = payload as { connectionId?: string; id?: string } | null | undefined;
                    markConnected(data?.connectionId ?? data?.id ?? null);
                    break;
                }
                case 'spans':
                    handleSpansPayload(payload);
                    break;
            }
        };

        eventSource.onopen = () => {
            // Mark open transport as connected even when custom connected events are absent.
            markConnected();
        };

        eventSource.onmessage = (e) => {
            const envelope = parseEnvelope(e.data);
            if (!envelope) return;
            dispatchEvent(envelope.eventType, envelope.payload);
        };

        eventSource.addEventListener('connected', (e) => {
            const envelope = parseEnvelope(e.data);
            if (envelope) {
                dispatchEvent(envelope.eventType, envelope.payload);
                return;
            }
            dispatchEvent('connected', safeParseJson(e.data));
        });

        eventSource.addEventListener('spans', (e) => {
            const envelope = parseEnvelope(e.data);
            if (envelope) {
                dispatchEvent(envelope.eventType, envelope.payload);
                return;
            }
            dispatchEvent('spans', safeParseJson(e.data));
        });

        eventSource.onerror = () => {
            setIsConnected(false);
            setConnectionId(null);
            onDisconnect?.();
            eventSource.close();

            // Reconnect after 3s
            reconnectTimeoutRef.current = window.setTimeout(() => {
                connect();
            }, 3000);
        };
    }, [enabled, sessionFilter, onSpans, onConnect, onDisconnect, queryClient]);

    const disconnect = useCallback(() => {
        if (reconnectTimeoutRef.current) {
            clearTimeout(reconnectTimeoutRef.current);
            reconnectTimeoutRef.current = null;
        }
        if (eventSourceRef.current) {
            eventSourceRef.current.close();
            eventSourceRef.current = null;
        }
        setIsConnected(false);
        setConnectionId(null);
    }, []);

    useEffect(() => {
        connect();
        return () => disconnect();
    }, [connect, disconnect]);

    return {
        isConnected,
        connectionId,
        recentSpans,
        disconnect,
        reconnect: connect,
        clearSpans: () => setRecentSpans([]),
    };
}

// Span utilities - work with SpanRecord directly
export function getSpanColor(span: SpanRecord): string {
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

export function getSpanTypeLabel(span: SpanRecord): string {
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
