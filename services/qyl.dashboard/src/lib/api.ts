/** Shared HTTP helpers for the local, unsecured development dashboard. */

import type {
    GenAiEtlAuditEvaluationRequest,
    GenAiEtlAuditEvaluationResponse,
    GenAiEtlAuditReport,
} from '@/types';

export interface SseEvent {
    event: string;
    data: string;
    id?: string;
}

export async function consumeSse(
    url: string,
    signal: AbortSignal,
    onOpen: () => void,
    onEvent: (event: SseEvent) => void,
    lastEventId?: string,
): Promise<void> {
    const headers: Record<string, string> = {Accept: 'text/event-stream'};
    if (lastEventId) headers['Last-Event-ID'] = lastEventId;

    const response = await fetch(url, {
        credentials: 'include',
        headers,
        cache: 'no-store',
        signal,
    });

    if (!response.ok) throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    if (!response.body) throw new Error('SSE response has no body');
    onOpen();

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
        const {done, value} = await reader.read();
        buffer += decoder.decode(value, {stream: !done}).replaceAll('\r\n', '\n');

        let boundary: number;
        while ((boundary = buffer.indexOf('\n\n')) >= 0) {
            const frame = buffer.slice(0, boundary);
            buffer = buffer.slice(boundary + 2);

            let event = 'message';
            let id: string | undefined;
            const data: string[] = [];
            for (const line of frame.split('\n')) {
                if (line.startsWith('event:')) event = line.slice(6).trimStart();
                if (line.startsWith('id:')) id = line.slice(3).trimStart();
                if (line.startsWith('data:')) data.push(line.slice(5).trimStart());
            }

            if (data.length > 0) onEvent({event, data: data.join('\n'), id});
        }

        if (done) break;
    }
}

export async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        credentials: 'include',
        ...init,
        headers: init?.headers,
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

export async function postJson<T>(url: string, body: unknown, headers?: HeadersInit): Promise<T> {
    return fetchJson<T>(url, {
        method: 'POST',
        headers: {...Object.fromEntries(new Headers(headers)), 'Content-Type': 'application/json'},
        body: JSON.stringify(body),
    });
}

function costAuditUrl(path: string, startTime?: string, endTime?: string, limit?: number): string {
    const query = new URLSearchParams();
    if (startTime) query.set('startTime', startTime);
    if (endTime) query.set('endTime', endTime);
    if (limit !== undefined) query.set('limit', String(limit));
    const suffix = query.toString();
    return suffix ? `${path}?${suffix}` : path;
}

function projectScopeHeaders(projectScope?: string): HeadersInit | undefined {
    const normalized = projectScope?.trim();
    return normalized ? {'X-Qyl-Project': normalized} : undefined;
}

export function fetchGenAiEtlAudit(
    startTime?: string,
    endTime?: string,
    limit = 25,
    projectScope?: string,
): Promise<GenAiEtlAuditReport> {
    return fetchJson<GenAiEtlAuditReport>(
        costAuditUrl('/api/v1/cost/etl-audit', startTime, endTime, limit),
        {headers: projectScopeHeaders(projectScope)},
    );
}

export function evaluateGenAiEtlAudit(
    request: GenAiEtlAuditEvaluationRequest,
    startTime?: string,
    endTime?: string,
    projectScope?: string,
): Promise<GenAiEtlAuditEvaluationResponse> {
    return postJson<GenAiEtlAuditEvaluationResponse>(
        costAuditUrl('/api/v1/cost/etl-audit/evaluate', startTime, endTime),
        request,
        projectScopeHeaders(projectScope),
    );
}
