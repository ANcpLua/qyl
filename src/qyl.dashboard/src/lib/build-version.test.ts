import {describe, expect, it} from 'vitest';

import {
    extractDashboardBuildIdFromUrls,
    hasDashboardBuildUpdate,
    resolveCurrentDashboardBuildId,
    resolveServerBuildLabel,
    resolveServerDashboardBuildId,
} from './build-version';
import type {CollectorMeta} from './onboarding';

describe('build version helpers', () => {
    it('extracts hashed dashboard entry filenames from URLs', () => {
        expect(extractDashboardBuildIdFromUrls([
            '/@vite/client',
            '/assets/index-YS8O-KjX.js',
        ])).toBe('index-YS8O-KjX.js');
    });

    it('ignores dev-only scripts that are not built assets', () => {
        expect(extractDashboardBuildIdFromUrls([
            '/@vite/client',
            '/src/main.tsx',
        ])).toBeNull();
    });

    it('prefers the explicit runtime build id over DOM asset scraping', () => {
        expect(resolveCurrentDashboardBuildId(
            {
                querySelectorAll: () => [],
            },
            {
                buildId: 'build-123',
                builtAtUtc: '2026-04-08T12:00:00.000Z',
                version: '0.1.0',
                commit: 'abc123',
            },
        )).toBe('build-123');
    });

    it('falls back to the current entry asset when runtime build metadata is missing', () => {
        expect(resolveCurrentDashboardBuildId(
            {
                querySelectorAll: () => [
                    {
                        getAttribute: () => '/assets/index-OLDHASH.js',
                    },
                ] as unknown as NodeListOf<HTMLScriptElement>,
            },
            null,
        )).toBe('index-OLDHASH.js');
    });

    it('resolves the server dashboard build id from explicit meta or entry assets', () => {
        const explicitMeta: CollectorMeta = {
            build: {
                dashboardBuildId: 'build-999',
                dashboardEntryAsset: 'assets/index-NEWBUILD.js',
            },
        };
        const fallbackMeta: CollectorMeta = {
            build: {
                dashboardEntryAsset: 'assets/index-NEWBUILD.js',
            },
        };

        expect(resolveServerDashboardBuildId(explicitMeta)).toBe('build-999');
        expect(resolveServerDashboardBuildId(fallbackMeta)).toBe('index-NEWBUILD.js');
    });

    it('uses dashboard build timestamps before falling back to collector versions', () => {
        const meta: CollectorMeta = {
            version: '0.1.0',
            build: {
                informationalVersion: '0.1.0+abc123',
                dashboardBuiltAtUtc: '2026-04-08T12:00:00.000Z',
            },
        };

        expect(resolveServerBuildLabel(meta)).toBe('2026-04-08T12:00:00.000Z');
    });

    it('treats differing current and server build ids as an available update', () => {
        expect(hasDashboardBuildUpdate('build-OLD', 'build-NEW')).toBe(true);
        expect(hasDashboardBuildUpdate('build-SAME', 'build-SAME')).toBe(false);
        expect(hasDashboardBuildUpdate(null, 'build-NEW')).toBe(false);
    });
});
