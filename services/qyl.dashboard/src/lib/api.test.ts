import {afterEach, describe, expect, it, vi} from 'vitest';
import type {
} from '@/types';
import {
    consumeSse,
    parseLogSsePayload,
} from './api';

describe('consumeSse', () => {
    afterEach(() => vi.unstubAllGlobals());

    it('sends the resume cursor and preserves SSE event ids', async () => {
        type ParsedSseFrame = Parameters<Parameters<typeof consumeSse>[3]>[0];
        const encoded = new TextEncoder().encode(
            'id: 42\nevent: log\ndata: {"type":"log"}\n\n' +
            'event: heartbeat\ndata: {"type":"heartbeat"}\n\n',
        );
        const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
            expect(new Headers(init?.headers).get('Last-Event-ID')).toBe('41');
            return new Response(encoded, {
                status: 200,
                headers: {'Content-Type': 'text/event-stream'},
            });
        });
        vi.stubGlobal('fetch', fetchMock);
        const events: ParsedSseFrame[] = [];

        await consumeSse('/api/v1/stream/logs', new AbortController().signal, () => {},
            event => events.push(event), '41');

        expect(fetchMock).toHaveBeenCalledOnce();
        expect(events).toEqual([
            {event: 'log', data: '{"type":"log"}', id: '42'},
            {event: 'heartbeat', data: '{"type":"heartbeat"}', id: undefined},
        ]);
    });

    it('accepts generated log-stream payloads and rejects incomplete SSE data', () => {
        const logPayload = {
            type: 'log',
            data: {
                time_unix_nano: 1_000_000_000,
                observed_time_unix_nano: 1_000_000_000,
                severity_number: 9,
                severity_text: 'INFO',
                body: {string_value: 'ready'},
                resource: {'service.name': 'dashboard-test'},
            },
            timestamp: '2026-07-15T00:00:00Z',
        };

        expect(parseLogSsePayload({event: 'log', data: JSON.stringify(logPayload), id: '42'}))
            .toEqual(logPayload);
        expect(parseLogSsePayload({
            event: 'heartbeat',
            data: JSON.stringify({type: 'heartbeat', timestamp: '2026-07-15T00:00:01Z'}),
        })).toEqual({type: 'heartbeat', timestamp: '2026-07-15T00:00:01Z'});
        expect(() => parseLogSsePayload({event: 'log', data: '{"type":"log"}'}))
            .toThrow(/Collector contract mismatch/);
    });

});
