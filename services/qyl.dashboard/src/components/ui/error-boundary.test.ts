import {describe, expect, it} from 'vitest';
import {parseChunkRetryRecord} from './error-boundary';

describe('stale-chunk retry record parsing', () => {
    it('reads a well-formed retry marker', () => {
        expect(parseChunkRetryRecord('{"signature":"/logs|/assets/x.js","at":1700000000000}'))
            .toEqual({signature: '/logs|/assets/x.js', at: 1700000000000});
    });

    it('treats an absent or malformed marker as no previous retry', () => {
        expect(parseChunkRetryRecord(null)).toBeNull();
        expect(parseChunkRetryRecord('{"signature":"/logs|/assets/x.js"}')).toBeNull();
        expect(parseChunkRetryRecord('{"signature":"/logs|/assets/x.js","at":"1700000000000"}')).toBeNull();
        expect(parseChunkRetryRecord('"not-an-object"')).toBeNull();
    });

    it('propagates unparseable JSON to the caller that already guards it', () => {
        expect(() => parseChunkRetryRecord('{')).toThrow(SyntaxError);
    });
});
