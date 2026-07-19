/** Shared HTTP helpers for the local, unsecured development dashboard. */

import type {
} from '@/types';
import type {HeartbeatEvent, LogStreamEvent} from '@ancplua/qyl-api-schema/types';
import {
    parseHeartbeatEvent,
    parseLogStreamEvent,
    parseProblemDetails,
} from '@/lib/contract-validation';

interface SseFrame {
    event: string;
    data: string;
    id?: string;
}

export function parseLogSsePayload(frame: SseFrame): LogStreamEvent | HeartbeatEvent {
    let value: unknown;
    try {
        value = JSON.parse(frame.data);
    } catch {
        throw new Error(`Collector contract mismatch for /api/v1/stream/logs ${frame.event} event: invalid JSON`);
    }

    if (frame.event === 'log') return parseLogStreamEvent(value);
    if (frame.event === 'heartbeat') return parseHeartbeatEvent(value);
    throw new Error(`Collector contract mismatch for /api/v1/stream/logs: unsupported event '${frame.event}'`);
}

export async function consumeSse(
    url: string,
    signal: AbortSignal,
    onOpen: () => void,
    onEvent: (event: SseFrame) => void,
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

export async function fetchJson<T>(
    url: string,
    parse: (value: unknown) => T,
    init?: RequestInit,
): Promise<T> {
    const res = await fetch(url, {
        credentials: 'include',
        ...init,
        headers: init?.headers,
    });
    const mediaType = res.headers.get('content-type')?.split(';', 1)[0].trim().toLowerCase();
    if (!res.ok) {
        if (mediaType !== 'application/problem+json') {
            throw new Error(
                `Collector contract mismatch for ${url}: expected application/problem+json, got ${mediaType ?? 'no content type'}`,
            );
        }
        const problem = parseProblemDetails(await res.json() as unknown);
        throw new Error(`HTTP ${res.status}: ${problem.detail ?? problem.title}`);
    }
    if (mediaType !== 'application/json') {
        throw new Error(
            `Collector contract mismatch for ${url}: expected application/json, got ${mediaType ?? 'no content type'}`,
        );
    }
    return parse(await res.json() as unknown);
}

export async function postJson<T>(
    url: string,
    body: unknown,
    parse: (value: unknown) => T,
    headers?: HeadersInit,
): Promise<T> {
    return fetchJson(url, parse, {
        method: 'POST',
        headers: {...Object.fromEntries(new Headers(headers)), 'Content-Type': 'application/json'},
        body: JSON.stringify(body),
    });
}
