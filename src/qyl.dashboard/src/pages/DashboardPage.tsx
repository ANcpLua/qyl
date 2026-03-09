import {useNavigate, useParams} from 'react-router-dom';
import {
    Activity,
    AlertCircle,
    AlertTriangle,
    Database,
    Globe,
    LayoutDashboard,
    Loader2,
    MessageSquare,
    Zap,
} from 'lucide-react';
import type {LucideIcon} from 'lucide-react';
import type {DashboardDefinition, StatCardData, TimeSeriesPoint, TopNRow} from '@/hooks/use-dashboards';
import {useDashboard, useDashboards} from '@/hooks/use-dashboards';
import {StatCard, TimeSeriesChart, TopNTable} from '@/components/dashboards';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {cn} from '@/lib/utils';

const dashboardIconMap: Record<string, LucideIcon> = {
    'activity': Activity,
    'globe': Globe,
    'brain': Zap,
    'database': Database,
    'alert-triangle': AlertTriangle,
    'message-square': MessageSquare,
};

// ── Index mode (no ID) ────────────────────────────────────────────────────────

function DashboardIndex() {
    const navigate = useNavigate();
    const {data: dashboards, isLoading, error} = useDashboards();

    if (isLoading) {
        return (
            <div className="flex-1 flex items-center justify-center">
                <Loader2 className="w-6 h-6 text-signal-orange animate-spin"/>
            </div>
        );
    }

    if (error) {
        return (
            <div className="flex-1 flex items-center justify-center">
                <div className="text-center space-y-2">
                    <AlertCircle className="w-10 h-10 mx-auto text-red-500"/>
                    <p className="text-sm text-brutal-slate">Failed to load dashboards</p>
                </div>
            </div>
        );
    }

    if (!dashboards || dashboards.length === 0) {
        return (
            <div className="flex-1 flex items-center justify-center">
                <div className="text-center space-y-2">
                    <LayoutDashboard className="w-10 h-10 mx-auto text-brutal-zinc"/>
                    <p className="text-sm font-bold text-brutal-slate tracking-wider">
                        NO DASHBOARDS DETECTED YET
                    </p>
                    <p className="text-xs text-brutal-zinc">
                        Dashboards appear automatically as telemetry arrives.
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div className="flex-1 p-6 space-y-6 overflow-auto">
            <h1 className="text-lg font-bold text-brutal-white tracking-wider uppercase">
                DASHBOARDS
            </h1>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                {dashboards.map((db) => (
                    <DashboardCard key={db.id} dashboard={db} onClick={() => navigate(`/dashboards/${db.id}`)}/>
                ))}
            </div>
        </div>
    );
}

function DashboardCard({dashboard, onClick}: { dashboard: DashboardDefinition; onClick: () => void }) {
    const Icon = dashboardIconMap[dashboard.icon] ?? LayoutDashboard;
    return (
        <Card
            className="cursor-pointer transition-colors hover:border-brutal-zinc/70"
            onClick={onClick}
        >
            <CardContent className="pt-4 space-y-2">
                <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                        <Icon className="w-4 h-4 text-signal-orange"/>
                        <span className="text-sm font-bold text-brutal-white tracking-wider uppercase">
                            {dashboard.title}
                        </span>
                    </div>
                    <Badge
                        variant={dashboard.isAvailable ? 'default' : 'secondary'}
                        className={cn(
                            'text-[10px]',
                            dashboard.isAvailable
                                ? 'bg-green-500/20 text-green-400 border-green-500/40'
                                : 'bg-brutal-zinc/30 text-brutal-slate border-brutal-zinc',
                        )}
                    >
                        {dashboard.isAvailable ? 'LIVE' : 'PENDING'}
                    </Badge>
                </div>
                <p className="text-xs text-brutal-slate line-clamp-2">{dashboard.description}</p>
            </CardContent>
        </Card>
    );
}

// ── Detail mode (with ID) ─────────────────────────────────────────────────────

function DashboardDetail({id}: { id: string }) {
    const {data: dashboard, isLoading, error} = useDashboard(id);

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

// ── Exported page ─────────────────────────────────────────────────────────────

export function DashboardPage() {
    const {id} = useParams<{ id: string }>();
    return id ? <DashboardDetail id={id}/> : <DashboardIndex/>;
}
