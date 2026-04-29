import {useQuery} from '@tanstack/react-query';
import {fetchJson} from '@/lib/api';

export interface ConversationCaptureFlags {
    messageContent: boolean;
    recordInputs: boolean;
    recordOutputs: boolean;
}

export interface ConversationListItem {
    sessionId: string;
    spanCount: number;
    firstSeen: string;
    lastSeen: string;
    durationMs: number;
    totalCostUsd: number;
    inputTokens: number | null;
    outputTokens: number | null;
    services: string[];
    models: string[];
}

export interface ConversationListResponse {
    items: ConversationListItem[];
    total: number;
    captureFlags: ConversationCaptureFlags;
}

export interface ConversationSpan {
    spanId: string;
    traceId: string;
    parentSpanId: string | null;
    name: string;
    serviceName: string;
    startTime: string;
    durationMs: number;
    provider: string | null;
    requestModel: string | null;
    responseModel: string | null;
    inputTokens: number | null;
    outputTokens: number | null;
    toolName: string | null;
    toolCallId: string | null;
    costUsd: number | null;
    statusCode: number;
    statusMessage: string | null;
    attributes: Record<string, unknown> | null;
}

export interface ConversationDetail {
    sessionId: string;
    spanCount: number;
    firstSeen: string;
    lastSeen: string;
    totalCostUsd: number;
    spans: ConversationSpan[];
    captureFlags: ConversationCaptureFlags;
}

export const conversationKeys = {
    all: ['conversations'] as const,
    list: (limit?: number, hours?: number) => [...conversationKeys.all, 'list', limit, hours] as const,
    detail: (sessionId: string | null) => [...conversationKeys.all, 'detail', sessionId] as const,
};

export function useConversations(limit = 100, hours = 168) {
    return useQuery({
        queryKey: conversationKeys.list(limit, hours),
        queryFn: () => fetchJson<ConversationListResponse>(
            `/api/v1/conversations?limit=${limit}&hours=${hours}`,
        ),
        staleTime: 30_000,
    });
}

export function useConversationDetail(sessionId: string | null) {
    return useQuery({
        queryKey: conversationKeys.detail(sessionId),
        queryFn: () => fetchJson<ConversationDetail>(
            `/api/v1/conversations/${encodeURIComponent(sessionId ?? '')}`,
        ),
        enabled: sessionId !== null && sessionId.length > 0,
        staleTime: 30_000,
    });
}
