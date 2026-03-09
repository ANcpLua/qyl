import {useMemo, useState} from 'react';
import {
    Area,
    AreaChart,
    CartesianGrid,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts';
import {
    Activity,
    AlertCircle,
    ArrowUpDown,
    Clock,
    Gauge,
    Loader2,
    Server,
    Zap,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import type {ServiceSummary, TrafficBucket} from '@/hooks/usePerformance';
import {useErrorStats, useLatencyBaseline, useServices, useStorageStats, useTraffic} from '@/hooks/usePerformance';

type SortField = 'serviceName' | 'serviceType' | 'latestVersion' | 'firstSeen' | 'lastSeen' | 'lastErrorAt';
type SortDir = 'asc' | 'desc';

function formatTimestamp(iso?: string | null): string {
    if (!iso) return '\u2014';
    return new Date(iso).toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
    });
}

function formatLatency(ms: number): string {
    if (ms < 1) return `${(ms * 1000).toFixed(0)}\u00B5s`;
    if (ms < 1000) return `${ms.toFixed(1)}ms`;
    return `${(ms / 1000).toFixed(2)}s`;
}

function formatHour(iso: string): string {
    return new Date(iso).toLocaleString('en-US', {
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
    });
}

function sortServices(services: ServiceSummary[], field: SortField, dir: SortDir): ServiceSummary[] {
    return [...services].sort((a, b) => {
        const aVal = a[field] ?? '';
        const bVal = b[field] ?? '';
        const cmp = String(aVal).localeCompare(String(bVal));
        return dir === 'asc' ? cmp : -cmp;
    });
}

function SkeletonRow() {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            <div className="flex-1 h-4 bg-brutal-zinc rounded"/>
            <div className="w-20 h-4 bg-brutal-zinc rounded"/>
            <div className="w-24 h-4 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
        </div>
    );
}

export function PerformancePage() {
    const [sortField, setSortField] = useState<SortField>('lastSeen');
    const [sortDir, setSortDir] = useState<SortDir>('desc');

    const {data: stats, isLoading: statsLoading, error: statsError} = useStorageStats();
    const {data: servicesData, isLoading: servicesLoading, error: servicesError} = useServices();
    const {data: errorStats, isLoading: errorsLoading, error: errorsError} = useErrorStats();
    const {data: latency, isLoading: latencyLoading, error: latencyError} = useLatencyBaseline();
    const {data: trafficData, isLoading: trafficLoading} = useTraffic();

    const chartData = useMemo(() =>
        (trafficData?.buckets ?? []).map((b: TrafficBucket) => ({
            time: formatHour(b.time),
            requests: b.runs,
            errors: b.errors,
        })), [trafficData]);

    const isLoading = statsLoading || servicesLoading || errorsLoading || latencyLoading;
    const error = statsError || servicesError || errorsError || latencyError;

    const handleSort = (field: SortField) => {
        if (sortField === field) {
            setSortDir(prev => prev === 'asc' ? 'desc' : 'asc');
        } else {
            setSortField(field);
            setSortDir('asc');
        }
    };

    const services = servicesData?.services ?? [];
    const sorted = sortServices(services, sortField, sortDir);
    const hasData = (stats?.spanCount ?? 0) > 0 || services.length > 0;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-destructive"/>
                        <p className="text-destructive">Failed to load performance data</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {error instanceof Error ? error.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    if (!isLoading && !hasData) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center text-brutal-slate">
                        <Gauge className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                        <p>No performance data yet</p>
                        <p className="text-sm mt-1">Performance metrics will appear as telemetry is ingested</p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return (
        <div className="p-6 space-y-6">
            {/* Stats Row */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Activity className="w-4 h-4 text-signal-cyan"/>
                            <span className="text-sm text-brutal-slate">Total Requests</span>
                        </div>
                        {statsLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 font-mono">
                                {(stats?.spanCount ?? 0).toLocaleString()}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Server className="w-4 h-4 text-signal-violet"/>
                            <span className="text-sm text-brutal-slate">Active Services</span>
                        </div>
                        {servicesLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 font-mono">
                                {services.length}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <AlertCircle className="w-4 h-4 text-signal-red"/>
                            <span className="text-sm text-brutal-slate">Error Rate</span>
                        </div>
                        {errorsLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className={cn(
                                'text-2xl font-bold mt-1 font-mono',
                                (errorStats?.errorRate ?? 0) > 5 ? 'text-signal-red' : 'text-signal-green',
                            )}>
                                {((errorStats?.errorRate ?? 0)).toFixed(1)}%
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Zap className="w-4 h-4 text-signal-orange"/>
                            <span className="text-sm text-brutal-slate">Avg Latency</span>
                        </div>
                        {latencyLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin text-brutal-slate"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 font-mono">
                                {latency ? formatLatency(latency.mean) : '\u2014'}
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Throughput Chart */}
            <Card>
                <CardHeader className="pb-2">
                    <div className="flex items-center justify-between">
                        <h2 className="text-lg font-semibold">Throughput</h2>
                        <span className="text-[10px] font-bold text-brutal-slate tracking-wider">LAST 24 HOURS</span>
                    </div>
                </CardHeader>
                <CardContent>
                    {trafficLoading ? (
                        <div className="flex items-center justify-center h-48">
                            <Loader2 className="w-5 h-5 animate-spin text-brutal-slate"/>
                        </div>
                    ) : chartData.length === 0 ? (
                        <div className="flex items-center justify-center h-48 text-brutal-slate text-sm">
                            No traffic data in the last 24 hours
                        </div>
                    ) : (
                        <div className="h-56">
                            <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={0}>
                                <AreaChart data={chartData} margin={{top: 4, right: 4, bottom: 0, left: 0}}>
                                    <defs>
                                        <linearGradient id="fillRequests" x1="0" y1="0" x2="0" y2="1">
                                            <stop offset="5%" stopColor="#f97316" stopOpacity={0.3}/>
                                            <stop offset="95%" stopColor="#f97316" stopOpacity={0}/>
                                        </linearGradient>
                                        <linearGradient id="fillErrors" x1="0" y1="0" x2="0" y2="1">
                                            <stop offset="5%" stopColor="#ef4444" stopOpacity={0.3}/>
                                            <stop offset="95%" stopColor="#ef4444" stopOpacity={0}/>
                                        </linearGradient>
                                    </defs>
                                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(0, 0%, 20%)"/>
                                    <XAxis
                                        dataKey="time"
                                        tick={{fill: 'hsl(0, 0%, 50%)', fontSize: 10}}
                                        axisLine={{stroke: 'hsl(0, 0%, 25%)'}}
                                        tickLine={false}
                                    />
                                    <YAxis
                                        tick={{fill: 'hsl(0, 0%, 50%)', fontSize: 10}}
                                        axisLine={{stroke: 'hsl(0, 0%, 25%)'}}
                                        tickLine={false}
                                        width={50}
                                        allowDecimals={false}
                                    />
                                    <Tooltip
                                        contentStyle={{
                                            backgroundColor: 'hsl(0, 0%, 8%)',
                                            border: '2px solid hsl(0, 0%, 25%)',
                                            borderRadius: 0,
                                            color: 'hsl(0, 0%, 90%)',
                                            fontSize: 12,
                                        }}
                                    />
                                    <Area
                                        type="monotone"
                                        dataKey="requests"
                                        name="Requests"
                                        stroke="#f97316"
                                        strokeWidth={2}
                                        fill="url(#fillRequests)"
                                    />
                                    <Area
                                        type="monotone"
                                        dataKey="errors"
                                        name="Errors"
                                        stroke="#ef4444"
                                        strokeWidth={1.5}
                                        fill="url(#fillErrors)"
                                    />
                                </AreaChart>
                            </ResponsiveContainer>
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Latency Overview */}
            {latency && (
                <div>
                    <h2 className="text-lg font-semibold mb-3">Latency Percentiles</h2>
                    <div className="grid grid-cols-3 gap-4">
                        <Card>
                            <CardHeader className="pb-2">
                                <div className="flex items-center gap-2">
                                    <Clock className="w-4 h-4 text-signal-green"/>
                                    <span className="text-xs font-bold text-brutal-slate tracking-wider">P50</span>
                                </div>
                            </CardHeader>
                            <CardContent>
                                <div className="text-2xl font-bold font-mono text-signal-green">
                                    {formatLatency(latency.p50)}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardHeader className="pb-2">
                                <div className="flex items-center gap-2">
                                    <Clock className="w-4 h-4 text-signal-orange"/>
                                    <span className="text-xs font-bold text-brutal-slate tracking-wider">P95</span>
                                </div>
                            </CardHeader>
                            <CardContent>
                                <div className="text-2xl font-bold font-mono text-signal-orange">
                                    {formatLatency(latency.p95)}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardHeader className="pb-2">
                                <div className="flex items-center gap-2">
                                    <Clock className="w-4 h-4 text-signal-red"/>
                                    <span className="text-xs font-bold text-brutal-slate tracking-wider">P99</span>
                                </div>
                            </CardHeader>
                            <CardContent>
                                <div className="text-2xl font-bold font-mono text-signal-red">
                                    {formatLatency(latency.p99)}
                                </div>
                            </CardContent>
                        </Card>
                    </div>
                </div>
            )}

            {/* Services Table */}
            <div>
                <div className="flex items-center justify-between mb-3">
                    <h2 className="text-lg font-semibold">Services</h2>
                    <span className="text-xs font-bold text-brutal-slate tracking-wider">
                        {services.length} SERVICE{services.length !== 1 ? 'S' : ''}
                    </span>
                </div>

                <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                    <div
                        className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                        <SortableHeader
                            label="SERVICE NAME"
                            field="serviceName"
                            className="flex-1"
                            activeField={sortField}
                            dir={sortDir}
                            onSort={handleSort}
                        />
                        <SortableHeader
                            label="TYPE"
                            field="serviceType"
                            className="w-24"
                            activeField={sortField}
                            dir={sortDir}
                            onSort={handleSort}
                        />
                        <SortableHeader
                            label="VERSION"
                            field="latestVersion"
                            className="w-24"
                            activeField={sortField}
                            dir={sortDir}
                            onSort={handleSort}
                        />
                        <SortableHeader
                            label="FIRST SEEN"
                            field="firstSeen"
                            className="w-28 text-right"
                            activeField={sortField}
                            dir={sortDir}
                            onSort={handleSort}
                        />
                        <SortableHeader
                            label="LAST SEEN"
                            field="lastSeen"
                            className="w-28 text-right"
                            activeField={sortField}
                            dir={sortDir}
                            onSort={handleSort}
                        />
                        <SortableHeader
                            label="LAST ERROR"
                            field="lastErrorAt"
                            className="w-28 text-right"
                            activeField={sortField}
                            dir={sortDir}
                            onSort={handleSort}
                        />
                    </div>

                    {servicesLoading ? (
                        <>
                            <SkeletonRow/>
                            <SkeletonRow/>
                            <SkeletonRow/>
                        </>
                    ) : sorted.length === 0 ? (
                        <div className="py-12 text-center">
                            <Server className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                            <p className="text-brutal-slate text-sm">No services discovered</p>
                            <p className="text-brutal-zinc text-xs mt-1">Services will appear as telemetry is ingested</p>
                        </div>
                    ) : (
                        sorted.map((svc) => (
                            <div
                                key={`${svc.serviceNamespace ?? ''}-${svc.serviceName}`}
                                className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 transition-colors"
                            >
                                <div className="flex-1 min-w-0">
                                    <span className="text-sm font-bold text-brutal-white truncate block">
                                        {svc.serviceName}
                                    </span>
                                    {svc.serviceNamespace && (
                                        <span className="text-xs text-brutal-slate">{svc.serviceNamespace}</span>
                                    )}
                                </div>

                                <div className="w-24">
                                    {svc.serviceType ? (
                                        <Badge variant="outline" className="text-[10px]">
                                            {svc.serviceType}
                                        </Badge>
                                    ) : (
                                        <span className="text-xs text-brutal-slate">{'\u2014'}</span>
                                    )}
                                </div>

                                <div className="w-24">
                                    <span className="font-mono text-xs text-brutal-slate">
                                        {svc.latestVersion ?? '\u2014'}
                                    </span>
                                </div>

                                <div className="w-28 text-right">
                                    <span className="font-mono text-xs text-brutal-slate">
                                        {formatTimestamp(svc.firstSeen)}
                                    </span>
                                </div>

                                <div className="w-28 text-right">
                                    <span className="font-mono text-xs text-brutal-slate">
                                        {formatTimestamp(svc.lastSeen)}
                                    </span>
                                </div>

                                <div className="w-28 text-right">
                                    <span className={cn(
                                        'font-mono text-xs',
                                        svc.lastErrorAt ? 'text-signal-red' : 'text-brutal-slate',
                                    )}>
                                        {formatTimestamp(svc.lastErrorAt)}
                                    </span>
                                </div>
                            </div>
                        ))
                    )}
                </div>
            </div>
        </div>
    );
}

function SortableHeader({
    label,
    field,
    className,
    activeField,
    dir,
    onSort,
}: {
    label: string;
    field: SortField;
    className?: string;
    activeField: SortField;
    dir: SortDir;
    onSort: (field: SortField) => void;
}) {
    const isActive = activeField === field;
    return (
        <button
            type="button"
            className={cn(
                'flex items-center gap-1 cursor-pointer hover:text-brutal-white transition-colors',
                className,
            )}
            onClick={() => onSort(field)}
        >
            {label}
            <ArrowUpDown className={cn('w-3 h-3', isActive ? 'text-brutal-white' : 'text-brutal-zinc')}/>
            {isActive && (
                <span className="text-brutal-white">{dir === 'asc' ? '\u2191' : '\u2193'}</span>
            )}
        </button>
    );
}
