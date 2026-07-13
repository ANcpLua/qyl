import {afterEach, describe, expect, it, vi} from 'vitest';
import {consumeSse, type SseEvent} from './api';

describe('consumeSse', () => {
    afterEach(() => vi.unstubAllGlobals());

    it('sends the resume cursor and preserves SSE event ids', async () => {
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
        const events: SseEvent[] = [];

        await consumeSse('/api/v1/stream/logs', new AbortController().signal, () => {},
            event => events.push(event), '41');

        expect(fetchMock).toHaveBeenCalledOnce();
        expect(events).toEqual([
            {event: 'log', data: '{"type":"log"}', id: '42'},
            {event: 'heartbeat', data: '{"type":"heartbeat"}', id: undefined},
        ]);
    });
});
