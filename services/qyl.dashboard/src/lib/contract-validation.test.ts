import {describe, expect, it} from 'vitest';
import {parseHealthReport, parseLogStreamEvent, parseTracePage} from './contract-validation';

const span = {
    span_id: '1111111111111111',
    trace_id: '22222222222222222222222222222222',
    name: 'dashboard-test',
    kind: 1,
    start_time_unix_nano: 1_000_000_000,
    end_time_unix_nano: 2_000_000_000,
    status: {code: 1},
    resource: {'service.name': 'dashboard-test'},
};

const trace = {
    trace_id: span.trace_id,
    span_count: 1,
    duration_ns: 1_000_000_000,
    start_time: '2026-07-15T00:00:00Z',
    end_time: '2026-07-15T00:00:01Z',
    services: ['dashboard-test'],
    has_error: false,
    spans: [span],
};

describe('generated Collector contract validation', () => {
    it('accepts generated health and trace-page contracts', () => {
        expect(parseHealthReport({
            status: 'healthy',
            totalDurationMs: 1,
            entries: {duckdb: {status: 'healthy', durationMs: 1}},
        }).status).toBe('healthy');
        expect(parseTracePage({items: [trace], has_more: false}).items[0].spans[0].resource['service.name'])
            .toBe('dashboard-test');
    });

    it('rejects invalid page envelopes and generated trace items', () => {
        expect(() => parseTracePage({items: [], has_more: 'false'}))
            .toThrow(/Collector contract mismatch/);
        expect(() => parseTracePage({items: [{...trace, spans: [{...span, resource: {}}]}], has_more: false}))
            .toThrow(/Collector contract mismatch/);
        expect(() => parseTracePage({items: [], has_more: false, total: 0}))
            .toThrow(/Collector contract mismatch/);
    });

    it('retains correlated MCP log fields on streamed log records', () => {
        const event = parseLogStreamEvent({
            type: 'log',
            timestamp: '2026-07-15T00:00:00Z',
            data: {
                time_unix_nano: 1_000_000_000,
                observed_time_unix_nano: 1_000_000_001,
                severity_number: 9,
                body: {string_value: 'MCP JSON-RPC request completed'},
                event_name: 'mcp.request',
                trace_id: '22222222222222222222222222222222',
                span_id: '1111111111111111',
                resource: {'service.name': 'dashboard-test'},
            },
        });

        expect(event.data).toMatchObject({
            event_name: 'mcp.request',
            body: {string_value: 'MCP JSON-RPC request completed'},
            trace_id: '22222222222222222222222222222222',
            span_id: '1111111111111111',
        });
    });
});
