import {useCallback, useEffect, useRef, useState} from 'react';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import type {Session, SessionListResponse, Span, SpanBatch, SpanListResponse, TraceResponse,} from '@/types';
// REMOVED: import type { Resource } from '@/types/telemetry';
// Resource type and useResources() were calling non-existent endpoint

// Query keys
export const telemetryKeys = {
  all: ['telemetry'] as const,
  sessions: () => [...telemetryKeys.all, 'sessions'] as const,
  session: (id: string) => [...telemetryKeys.sessions(), id] as const,
  sessionSpans: (id: string) => [...telemetryKeys.session(id), 'spans'] as const,
  traces: () => [...telemetryKeys.all, 'traces'] as const,
  trace: (id: string) => [...telemetryKeys.traces(), id] as const,
  logs: () => [...telemetryKeys.all, 'logs'] as const,
  metrics: () => [...telemetryKeys.all, 'metrics'] as const,
  // REMOVED: resources key - endpoint doesn't exist
};

// API functions
async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url, {
    credentials: 'include',
  });
  if (!res.ok) {
    throw new Error(`HTTP ${res.status}: ${res.statusText}`);
  }
  return res.json();
}

// Sessions - using OpenAPI-aligned response types
export function useSessions() {
  return useQuery({
    queryKey: telemetryKeys.sessions(),
    queryFn: () => fetchJson<SessionListResponse>('/api/v1/sessions'),
    select: (data) => data.sessions,
    refetchInterval: 10000, // Refresh every 10s
  });
}

export function useSession(sessionId: string) {
  return useQuery({
    queryKey: telemetryKeys.session(sessionId),
    queryFn: () => fetchJson<Session>(`/api/v1/sessions/${sessionId}`),
    enabled: !!sessionId,
  });
}

export function useSessionSpans(sessionId: string) {
  return useQuery({
    queryKey: telemetryKeys.sessionSpans(sessionId),
    queryFn: () => fetchJson<SpanListResponse>(`/api/v1/sessions/${sessionId}/spans`),
    select: (data) => data.spans,
    enabled: !!sessionId,
  });
}

// Traces - using OpenAPI-aligned response types
export function useTrace(traceId: string) {
  return useQuery({
    queryKey: telemetryKeys.trace(traceId),
    queryFn: () => fetchJson<TraceResponse>(`/api/v1/traces/${traceId}`),
    select: (data) => data.spans,
    enabled: !!traceId,
  });
}

// REMOVED: useResources() - endpoint /api/v1/resources doesn't exist
// If you need this, add the endpoint to Program.cs first

// Live SSE Stream
interface UseLiveStreamOptions {
  sessionFilter?: string;
  onSpans?: (batch: SpanBatch) => void;
  onConnect?: () => void;
  onDisconnect?: () => void;
  enabled?: boolean;
}

export function useLiveStream(options: UseLiveStreamOptions = {}) {
  const {
    sessionFilter,
    onSpans,
    onConnect,
    onDisconnect,
    enabled = true,
  } = options;

  const queryClient = useQueryClient();
  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimeoutRef = useRef<number | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [connectionId, setConnectionId] = useState<string | null>(null);
  const [recentSpans, setRecentSpans] = useState<Span[]>([]);

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

    eventSource.addEventListener('connected', (e) => {
      const data = JSON.parse(e.data);
      setConnectionId(data.connectionId);
      setIsConnected(true);
      onConnect?.();
    });

    eventSource.addEventListener('spans', (e) => {
      const batch: SpanBatch = JSON.parse(e.data);
      onSpans?.(batch);

      // Update recent spans (keep last 100)
      setRecentSpans((prev) => {
        const updated = [...batch.spans, ...prev];
        return updated.slice(0, 100);
      });

      // Invalidate relevant queries
      queryClient.invalidateQueries({queryKey: telemetryKeys.sessions()});
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

// Span utilities
export function getSpanColor(span: Span): string {
  // GenAI spans
  if (span.genai || span.attributes['gen_ai.provider.name']) {
    return 'hsl(var(--span-genai))';
  }
  // HTTP spans
  if (span.attributes['http.method'] || span.attributes['http.request.method']) {
    return 'hsl(var(--span-http))';
  }
  // Database spans
  if (span.attributes['db.system']) {
    return 'hsl(var(--span-db))';
  }
  // Messaging spans
  if (span.attributes['messaging.system']) {
    return 'hsl(var(--span-message))';
  }
  // Default
  return 'hsl(var(--span-internal))';
}

export function getSpanTypeLabel(span: Span): string {
  if (span.genai || span.attributes['gen_ai.provider.name']) {
    return 'GenAI';
  }
  if (span.attributes['http.method'] || span.attributes['http.request.method']) {
    return 'HTTP';
  }
  if (span.attributes['db.system']) {
    return 'Database';
  }
  if (span.attributes['messaging.system']) {
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
