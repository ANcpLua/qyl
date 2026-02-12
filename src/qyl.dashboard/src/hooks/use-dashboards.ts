import {useQuery} from '@tanstack/react-query';

// Dashboard types (inline, not from api.ts)
export interface DashboardDefinition {
    id: string;
    title: string;
    description: string;
    icon: string;
    isAvailable: boolean;
}

export interface DashboardData {
    dashboardId: string;
    title: string;
    description: string;
    icon: string;
    widgets: DashboardWidget[];
}

export interface DashboardWidget {
    id: string;
    title: string;
    type: 'chart' | 'table' | 'stat';
    data: StatCardData | TimeSeriesPoint[] | TopNRow[];
}

export interface StatCardData {
    label: string;
    value: string;
    unit?: string;
    change?: number;
}

export interface TimeSeriesPoint {
    time: string;
    value: number;
    label?: string;
}

export interface TopNRow {
    name: string;
    value: number;
    unit?: string;
    count?: number;
    errorRate?: number;
}

// Query keys
export const dashboardKeys = {
    all: ['dashboards'] as const,
    list: () => [...dashboardKeys.all, 'list'] as const,
    detail: (id: string) => [...dashboardKeys.all, id] as const,
};

async function fetchJson<T>(url: string): Promise<T> {
    const res = await fetch(url, {credentials: 'include'});
    if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    return res.json();
}

interface DashboardListResponse {
    items: DashboardDefinition[];
    total: number;
}

export function useDashboards() {
    return useQuery({
        queryKey: dashboardKeys.list(),
        queryFn: () => fetchJson<DashboardListResponse>('/api/v1/dashboards'),
        select: (data): DashboardDefinition[] => data.items,
        refetchInterval: 60_000,
    });
}

export function useDashboard(id: string) {
    return useQuery({
        queryKey: dashboardKeys.detail(id),
        queryFn: () => fetchJson<DashboardData>(`/api/v1/dashboards/${id}`),
        enabled: !!id,
        refetchInterval: 30_000,
    });
}
