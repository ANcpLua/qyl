import {describe, expect, it} from 'vitest';
import type {AttributeValue} from '@ancplua/qyl-api-schema/types';
import {
    attributeNumber,
    attributeString,
    decodeAttributeValue,
    formatAttributeValue,
} from './attribute-value';

describe('AttributeValue presentation', () => {
    it('preserves int64 text and refuses unsafe numeric coercion', () => {
        const maximum = {type: 'int', value: '9223372036854775807'} as AttributeValue;
        const safe = {type: 'int', value: '42'} as AttributeValue;

        expect(formatAttributeValue(maximum)).toBe('9223372036854775807');
        expect(attributeNumber(maximum)).toBeUndefined();
        expect(attributeNumber(safe)).toBe(42);
    });

    it('decodes finite and canonical named doubles', () => {
        expect(attributeNumber({type: 'double', value: 1.5} as AttributeValue)).toBe(1.5);
        expect(attributeNumber({type: 'double', value: 'Infinity'} as AttributeValue)).toBe(Infinity);
        expect(attributeNumber({type: 'double', value: '-Infinity'} as AttributeValue)).toBe(-Infinity);
        expect(attributeNumber({type: 'double', value: 'NaN'} as AttributeValue)).toBeNaN();
    });

    it('formats empty, bytes, arrays, and recursive key-value lists', () => {
        const value = {
            type: 'kvlist',
            values: {
                empty: null,
                count: {type: 'int', value: '7'},
                nested: [true, {type: 'double', value: 'Infinity'}],
                payload: {type: 'bytes', base64: '/w=='},
            },
        } as unknown as AttributeValue;

        expect(decodeAttributeValue(value)).toEqual({
            empty: null,
            count: '7',
            nested: [true, 'Infinity'],
            payload: {type: 'bytes', base64: '/w=='},
        });
        expect(formatAttributeValue(value)).toBe(
            '{"empty":null,"count":"7","nested":[true,"Infinity"],"payload":{"type":"bytes","base64":"/w=="}}',
        );
    });

    it('only treats the actual string variant as text', () => {
        expect(attributeString('openai')).toBe('openai');
        expect(attributeString({type: 'int', value: '1'} as AttributeValue)).toBeUndefined();
    });
});
