import {useParams} from 'react-router-dom';
import {Loader2} from 'lucide-react';
import type {StatCardData, TimeSeriesPoint, TopNRow} from '@/hooks/use-dashboards';
import {useDashboard} from '@/hooks/use-dashboards';
import {StatCard, TimeSeriesChart, TopNTable} from '@/components/dashboards';

export function DashboardPage() {
    const {id} = useParams<{ id: string }>();
    const {data: dashboard, isLoading, error} = useDashboard(id ?? '');

    if (isLoading) {
        return (
            <div className="flex-1 flex items-center justify-center">
                <Loader2 className="w-6 h-6 text-signal-orange animate-spin"/>
            </div>
        );
    }

    if (error || !dashboard) {
        return (
            <div className="flex-1 flex items-center justify-center">
                <div className="text-brutal-slate text-sm font-bold tracking-wider">
                    DASHBOARD NOT FOUND
                </div>
            </div>
        );
    }

    const statWidgets = dashboard.widgets.filter(w => w.type === 'stat');
    const chartWidgets = dashboard.widgets.filter(w => w.type === 'chart');
    const tableWidgets = dashboard.widgets.filter(w => w.type === 'table');

    return (
        <div className="flex-1 p-6 space-y-6 overflow-auto">
            {/* Header */}
            <div className="space-y-1">
                <h1 className="text-lg font-bold text-brutal-white tracking-wider uppercase">
                    {dashboard.title}
                </h1>
                <p className="text-xs text-brutal-slate tracking-wider">
                    {dashboard.description}
                </p>
            </div>

            {/* Stat cards row */}
            {statWidgets.length > 0 && (
                <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                    {statWidgets.map(w => (
                        <StatCard
                            key={w.id}
                            title={w.title}
                            data={w.data as StatCardData}
                        />
                    ))}
                </div>
            )}

            {/* Charts */}
            {chartWidgets.length > 0 && (
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
                    {chartWidgets.map(w => (
                        <TimeSeriesChart
                            key={w.id}
                            title={w.title}
                            data={w.data as TimeSeriesPoint[]}
                        />
                    ))}
                </div>
            )}

            {/* Tables */}
            {tableWidgets.length > 0 && (
                <div className="grid grid-cols-1 gap-3">
                    {tableWidgets.map(w => (
                        <TopNTable
                            key={w.id}
                            title={w.title}
                            data={w.data as TopNRow[]}
                        />
                    ))}
                </div>
            )}

            {/* Empty state */}
            {dashboard.widgets.length === 0 && (
                <div className="flex items-center justify-center h-48 border-3 border-brutal-zinc bg-brutal-carbon">
                    <div className="text-brutal-slate text-xs font-bold tracking-widest">
                        NO DATA YET - WAITING FOR TELEMETRY
                    </div>
                </div>
            )}
        </div>
    );
}
