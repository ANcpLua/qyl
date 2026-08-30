import {describe, expect, it} from 'vitest';
import {
    parseHealthReport,
    parseHeartbeatEvent,
    parseLogStreamEvent,
    parseProblemDetails,
    parseSessionPage,
    parseSessionTracePage,
    parseSpanPage,
    parseTracePage,
} from './contract-validation';

const span = {
    span_id: '1111111111111111',
    trace_id: '22222222222222222222222222222222',
    name: 'dashboard-test',
    kind: 1,
    start_time_unix_nano: '1000000000',
    end_time_unix_nano: '2000000000',
    status: {code: 1},
    resource: {service_name: 'dashboard-test'},
};

const trace = {
    trace_id: span.trace_id,
    span_count: 1,
    duration_ns: '1000000000',
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
            contract_revision: 'sha256:9ba787d0bd3269a9',
            total_duration_ms: 1,
            entries: {duckdb: {status: 'healthy', duration_ms: 1}},
        }).status).toBe('healthy');
        expect(parseTracePage({items: [trace], has_more: false}).items[0].spans[0].resource.service_name)
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
                time_unix_nano: '1000000000',
                observed_time_unix_nano: '1000000001',
                severity_number: 9,
                body: {string_value: 'MCP JSON-RPC request completed'},
                event_name: 'mcp.request',
                trace_id: '22222222222222222222222222222222',
                span_id: '1111111111111111',
                resource: {service_name: 'dashboard-test'},
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

// The collector serialises offsets, never `Z`: `2026-01-09T23:06:40+00:00`. Fixtures that only
// ever used `Z` could not catch a validator that rejects the shape the wire actually carries,
// and a seconds-less `2026-01-09T23:06Z` must still be rejected as a non-contract timestamp.
const OFFSET_TIMESTAMP = '2026-01-09T23:06:40+00:00';
const SECONDS_MISSING_TIMESTAMP = '2026-01-09T23:06Z';

const session = {
    session_id: 'session-1',
    start_time: OFFSET_TIMESTAMP,
    end_time: OFFSET_TIMESTAMP,
    trace_count: 1,
    span_count: 1,
    error_count: 0,
    services: ['dashboard-test'],
    state: 'ended',
};

const offsetTrace = {...trace, start_time: OFFSET_TIMESTAMP, end_time: OFFSET_TIMESTAMP};

describe('wire timestamp shapes the collector actually emits', () => {
    it('accepts the offset timestamps every dated contract carries', () => {
        expect(parseSessionPage({items: [session], has_more: false}).items[0].start_time)
            .toBe(OFFSET_TIMESTAMP);
        expect(parseTracePage({items: [offsetTrace], has_more: false}).items[0].start_time)
            .toBe(OFFSET_TIMESTAMP);
        expect(parseSessionTracePage({items: [offsetTrace], has_more: false}, 'session-1').items[0].end_time)
            .toBe(OFFSET_TIMESTAMP);
        expect(parseLogStreamEvent({
            type: 'log',
            timestamp: OFFSET_TIMESTAMP,
            data: {
                time_unix_nano: '1000000000',
                observed_time_unix_nano: '1000000001',
                severity_number: 9,
                body: {string_value: 'offset timestamp'},
                resource: {service_name: 'dashboard-test'},
            },
        }).timestamp).toBe(OFFSET_TIMESTAMP);
        expect(parseHeartbeatEvent({type: 'heartbeat', timestamp: OFFSET_TIMESTAMP}).timestamp)
            .toBe(OFFSET_TIMESTAMP);
        expect(parseProblemDetails({
            type: 'about:blank',
            title: 'Not Found',
            status: 404,
            timestamp: OFFSET_TIMESTAMP,
        }).timestamp).toBe(OFFSET_TIMESTAMP);
    });

    it('rejects a timestamp that drops seconds on every dated contract', () => {
        expect(() => parseSessionPage({
            items: [{...session, start_time: SECONDS_MISSING_TIMESTAMP}],
            has_more: false,
        })).toThrow(/Collector contract mismatch/);
        expect(() => parseTracePage({
            items: [{...offsetTrace, start_time: SECONDS_MISSING_TIMESTAMP}],
            has_more: false,
        })).toThrow(/Collector contract mismatch/);
        expect(() => parseSessionTracePage({
            items: [{...offsetTrace, end_time: SECONDS_MISSING_TIMESTAMP}],
            has_more: false,
        }, 'session-1')).toThrow(/Collector contract mismatch/);
        expect(() => parseLogStreamEvent({
            type: 'log',
            timestamp: SECONDS_MISSING_TIMESTAMP,
            data: {
                time_unix_nano: '1000000000',
                observed_time_unix_nano: '1000000001',
                severity_number: 9,
                body: {string_value: 'offset timestamp'},
                resource: {service_name: 'dashboard-test'},
            },
        })).toThrow(/Collector contract mismatch/);
        expect(() => parseHeartbeatEvent({type: 'heartbeat', timestamp: SECONDS_MISSING_TIMESTAMP}))
            .toThrow(/Collector contract mismatch/);
        expect(() => parseProblemDetails({
            type: 'about:blank',
            title: 'Not Found',
            status: 404,
            timestamp: SECONDS_MISSING_TIMESTAMP,
        })).toThrow(/Collector contract mismatch/);
    });

    // `Operations.health_ready.Response.200` and `Operations.TracesApi_getSpans.Response.200`
    // publish no `format: date-time` member, so there is no timestamp shape to pin for them.
    it('still pins the two dated-field-free parsers against drift', () => {
        expect(parseSpanPage({items: [span], has_more: false}, span.trace_id).items[0].span_id)
            .toBe(span.span_id);
        expect(() => parseSpanPage({items: [{...span, kind: 'server'}], has_more: false}, span.trace_id))
            .toThrow(/Collector contract mismatch/);
        expect(() => parseHealthReport({status: 'healthy'}))
            .toThrow(/Collector contract mismatch/);
    });

    it('names the failing member in the mismatch detail', () => {
        expect(() => parseTracePage({items: [{...trace, spans: [{...span, resource: {}}]}], has_more: false}))
            .toThrow(/items\[0\]\.spans\[0\]\.resource\.service_name/);
    });
});
