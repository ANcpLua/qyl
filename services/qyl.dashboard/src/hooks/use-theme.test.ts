import {describe, expect, it} from 'vitest';
import {readStoredTheme} from './use-theme';

describe('stored theme parsing', () => {
    it('keeps every theme the toggle can write', () => {
        for (const theme of ['light', 'dark', 'system'] as const) {
            expect(readStoredTheme(theme)).toBe(theme);
        }
    });

    it('falls back to dark for absent, stale or corrupt values', () => {
        expect(readStoredTheme(null)).toBe('dark');
        expect(readStoredTheme('')).toBe('dark');
        expect(readStoredTheme('midnight')).toBe('dark');
        expect(readStoredTheme('{"theme":"light"}')).toBe('dark');
    });
});
