import {describe, expect, it} from 'vitest';
import {completedUtcDayRange, costAuditKeys, normalizeProjectScope} from './use-cost-audit';

describe('completedUtcDayRange', () => {
    it('uses the previous 30 completed UTC days instead of a partial current day', () => {
        expect(completedUtcDayRange(new Date('2026-07-14T18:43:22.456+02:00'))).toEqual({
            startTime: '2026-06-14T00:00:00.000Z',
            endTime: '2026-07-14T00:00:00.000Z',
        });
    });

    it('handles UTC calendar boundaries', () => {
        expect(completedUtcDayRange(new Date('2024-03-01T00:00:01Z'), 1)).toEqual({
            startTime: '2024-02-29T00:00:00.000Z',
            endTime: '2024-03-01T00:00:00.000Z',
        });
    });
});

describe('project-scoped audit queries', () => {
    it('normalizes an explicit project without inventing a default', () => {
        expect(normalizeProjectScope(' project-a ')).toBe('project-a');
        expect(normalizeProjectScope('   ')).toBeUndefined();
        expect(normalizeProjectScope(undefined)).toBeUndefined();
    });

    it('isolates query cache entries by optional project scope', () => {
        const unscoped = costAuditKeys.report('start', 'end', 25);
        const scoped = costAuditKeys.report('start', 'end', 25, 'project-a');

        expect(unscoped).not.toEqual(scoped);
        expect(scoped.at(-1)).toBe('project-a');
    });
});
