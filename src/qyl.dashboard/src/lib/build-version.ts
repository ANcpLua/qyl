import type {CollectorMeta} from './onboarding';

const DASHBOARD_ENTRY_ASSET_PATTERN = /(?:^|\/)(index-[^/?#]+\.js)\b/;

type RuntimeDashboardBuild = {
    buildId: string;
    builtAtUtc: string;
    version: string;
    commit: string | null;
};

function normalizeString(value: string | null | undefined): string | null {
    const trimmed = value?.trim();
    return trimmed ? trimmed : null;
}

function resolveRuntimeDashboardBuild(): RuntimeDashboardBuild | null {
    return typeof __QYL_DASHBOARD_BUILD__ !== 'undefined'
        ? __QYL_DASHBOARD_BUILD__
        : null;
}

export function extractDashboardBuildIdFromUrls(urls: readonly string[]): string | null {
    for (const url of urls) {
        const match = url.match(DASHBOARD_ENTRY_ASSET_PATTERN);
        if (match?.[1]) {
            return match[1];
        }
    }

    return null;
}

export function resolveCurrentDashboardBuildId(
    doc: Pick<Document, 'querySelectorAll'> = document,
    runtimeBuild: RuntimeDashboardBuild | null = resolveRuntimeDashboardBuild(),
): string | null {
    const runtimeBuildId = normalizeString(runtimeBuild?.buildId);
    if (runtimeBuildId) {
        return runtimeBuildId;
    }

    const urls = Array.from(doc.querySelectorAll('script[type="module"][src]'))
        .map(script => script.getAttribute('src') ?? '')
        .filter(Boolean);

    return extractDashboardBuildIdFromUrls(urls);
}

export function resolveServerDashboardBuildId(meta: CollectorMeta | null | undefined): string | null {
    return normalizeString(meta?.build?.dashboardBuildId)
        ?? extractDashboardBuildIdFromUrls([
            meta?.build?.dashboardEntryAsset ?? '',
        ]);
}

export function resolveServerBuildLabel(meta: CollectorMeta | null | undefined): string | null {
    return normalizeString(meta?.build?.dashboardBuiltAtUtc)
        ?? normalizeString(meta?.build?.informationalVersion)
        ?? normalizeString(meta?.version);
}

export function hasDashboardBuildUpdate(
    currentBuildId: string | null,
    serverBuildId: string | null,
): boolean {
    return currentBuildId !== null && serverBuildId !== null && currentBuildId !== serverBuildId;
}
