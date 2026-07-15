import {afterEach, describe, expect, it, vi} from 'vitest';
import type {
    GenAiEtlAuditEvaluationRequest,
    GenAiEtlAuditEvaluationResponse,
    GenAiEtlAuditReport,
} from '@/types';
import {
    consumeSse,
    evaluateGenAiEtlAudit,
    fetchGenAiEtlAudit,
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

    it('queries the typed ETL audit route with the completed-day range', async () => {
        const report: GenAiEtlAuditReport = {
            generated_at: '2026-07-14T00:01:00Z',
            period_start: '2026-06-14T00:00:00Z',
            period_end: '2026-07-14T00:00:00Z',
            summary: {
                total_calls: 0,
                total_input_tokens: 0,
                total_output_tokens: 0,
                catalog_token_priced_call_coverage: 0,
            },
            billing_sources: [],
            catalog_sources: [],
            clusters: [],
        };
        const fetchMock = vi.fn(async () => Response.json(report));
        vi.stubGlobal('fetch', fetchMock);

        await expect(fetchGenAiEtlAudit(report.period_start, report.period_end, 17, ' project-a '))
            .resolves.toEqual(report);

        expect(fetchMock).toHaveBeenCalledWith(
            '/api/v1/cost/etl-audit?startTime=2026-06-14T00%3A00%3A00Z&endTime=2026-07-14T00%3A00%3A00Z&limit=17',
            expect.objectContaining({credentials: 'include'}),
        );
        const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
        expect(new Headers(init.headers).get('X-Qyl-Project')).toBe('project-a');
    });

    it('rejects a successful Collector response that violates the generated contract', async () => {
        vi.stubGlobal('fetch', vi.fn(async () => Response.json({clusters: []})));

        await expect(fetchGenAiEtlAudit()).rejects.toThrow(/Collector contract mismatch/);
    });

    it('posts only the generated scenario contract and preserves the report period', async () => {
        const request: GenAiEtlAuditEvaluationRequest = {
            scenarios: [{
                cluster_id: 'workflow-1',
                coverage: 0.75,
                frontier_cost_per_call_usd: 0.04,
                alternative_cost_per_call_usd: 0.01,
                period_maintenance_cost_usd: 25,
                period_error_cost_usd: 5,
            }],
        };
        const response: GenAiEtlAuditEvaluationResponse = {
            generated_at: '2026-07-14T00:01:00Z',
            period_start: '2026-06-14T00:00:00Z',
            period_end: '2026-07-14T00:00:00Z',
            results: [],
        };
        const fetchMock = vi.fn(async () => Response.json(response));
        vi.stubGlobal('fetch', fetchMock);

        await expect(evaluateGenAiEtlAudit(request, response.period_start, response.period_end, 'project-a'))
            .resolves.toEqual(response);

        const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
        expect(url).toBe(
            '/api/v1/cost/etl-audit/evaluate?startTime=2026-06-14T00%3A00%3A00Z&endTime=2026-07-14T00%3A00%3A00Z',
        );
        expect(init).toEqual(expect.objectContaining({
            method: 'POST',
            credentials: 'include',
            body: JSON.stringify(request),
        }));
        expect(new Headers(init.headers).get('Content-Type')).toBe('application/json');
        expect(new Headers(init.headers).get('X-Qyl-Project')).toBe('project-a');
    });

    it('omits project scope when the dashboard leaves it empty', async () => {
        const fetchMock = vi.fn(async () => Response.json({
            generated_at: '2026-07-15T00:01:00Z',
            period_start: '2026-06-15T00:00:00Z',
            period_end: '2026-07-15T00:00:00Z',
            summary: {
                total_calls: 0,
                total_input_tokens: 0,
                total_output_tokens: 0,
                catalog_token_priced_call_coverage: 0,
            },
            billing_sources: [],
            catalog_sources: [],
            clusters: [],
        }));
        vi.stubGlobal('fetch', fetchMock);

        await fetchGenAiEtlAudit(undefined, undefined, 25, '   ');

        const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
        expect(new Headers(init.headers).has('X-Qyl-Project')).toBe(false);
    });
});
