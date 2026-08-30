import {describe, expect, it} from 'vitest';
import type {LogRecord} from '@ancplua/qyl-api-schema/types';
import {normalizeSeverity} from './LogsPage';

// `severity_text` is a closed contract enum at compile time, but the runtime guard in
// normalizeSeverity exists because an OTLP exporter can put any label on the wire.
const offContract = (text: string) => text as LogRecord['severity_text'];

describe('log severity normalization', () => {
    it('accepts every named level regardless of case or padding', () => {
        expect(normalizeSeverity('WARN', 9)).toBe('warn');
        expect(normalizeSeverity('FATAL', 9)).toBe('fatal');
        expect(normalizeSeverity('TRACE', 21)).toBe('trace');
        expect(normalizeSeverity(offContract('  Debug '), 17)).toBe('debug');
    });

    it('falls back to the numeric severity for unknown or absent labels', () => {
        expect(normalizeSeverity(offContract('verbose'), 9)).toBe('info');
        expect(normalizeSeverity(undefined, 17)).toBe('error');
        expect(normalizeSeverity(undefined, 21)).toBe('fatal');
        expect(normalizeSeverity(offContract(''), 1)).toBe('trace');
    });
});
