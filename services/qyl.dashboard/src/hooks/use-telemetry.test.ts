import {describe, expect, it} from 'vitest';

import {hasDatabaseSystem, selectTraceViewSource} from './use-telemetry';

describe('selectTraceViewSource', () => {
    it('uses the trace endpoint when a traceId deep-link is present', () => {
        expect(selectTraceViewSource({hasTraceId: true, sessionResolved: true, sessionSpanCount: 0}))
            .toBe('trace');
    });

    it('keeps the session source while the session traces query is still loading', () => {
        expect(selectTraceViewSource({hasTraceId: false, sessionResolved: false, sessionSpanCount: 0}))
            .toBe('session');
    });

    it('keeps the session source when the session has spans', () => {
        expect(selectTraceViewSource({hasTraceId: false, sessionResolved: true, sessionSpanCount: 3}))
            .toBe('session');
    });

    // Non-session HTTP telemetry: the session join can legitimately surface zero traces. Rather than
    // render an empty waterfall, fall back to recent traces so the telemetry stays visible.
    it('falls back to all traces when a resolved session has no retrievable traces', () => {
        expect(selectTraceViewSource({hasTraceId: false, sessionResolved: true, sessionSpanCount: 0}))
            .toBe('all-traces');
    });
});

describe('hasDatabaseSystem', () => {
    it('recognizes the canonical database semantic-convention key', () => {
        expect(hasDatabaseSystem({'db.system.name': 'sqlite'})).toBe(true);
    });

    it('keeps recognizing the deprecated key before collector normalization', () => {
        expect(hasDatabaseSystem({'db.system': 'sqlite'})).toBe(true);
    });
});
