import {useMemo, useRef, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {
    createColumnHelper,
    flexRender,
    getCoreRowModel,
    getFilteredRowModel,
    getPaginationRowModel,
    getSortedRowModel,
    type SortingState,
    useReactTable,
} from '@tanstack/react-table';
import ReactEChartsCore from 'echarts-for-react/lib/core';
import * as echarts from 'echarts/core';
import {LineChart} from 'echarts/charts';
import {DataZoomComponent, GridComponent, LegendComponent, TooltipComponent,} from 'echarts/components';
import {CanvasRenderer} from 'echarts/renderers';
import {
    Activity,
    ArrowDown,
    ArrowUp,
    ChevronLeft,
    ChevronRight,
    CircleDollarSign,
    Filter,
    TriangleAlert,
    Zap,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {fetchJson} from '@/lib/api';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';

echarts.use([LineChart, GridComponent, TooltipComponent, LegendComponent, DataZoomComponent, CanvasRenderer]);

// ── Types ────────────────────────────────────────────────────────────────────

interface CostByModel {
    model: string;
    provider: string;
    callCount: number;
    totalInputTokens: number;
    totalOutputTokens: number;
    totalCost: number;
}

interface CostByService {
    service: string;
    callCount: number;
    totalCost: number;
}

interface CostTimeSeries {
    bucket: string;
    model: string;
    cost: number;
}

interface BudgetStatus {
    dailyBudget: number | null;
    spentToday: number;
    remaining: number | null;
    status: 'ok' | 'warning' | 'exceeded';
}

// ── API hooks ────────────────────────────────────────────────────────────────

function useCostByModel() {
    return useQuery({
        queryKey: ['cost', 'by-model'],
        queryFn: () => fetchJson<CostByModel[]>('/api/v1/cost/by-model'),
        staleTime: 30_000,
    });
}

function useCostByService() {
    return useQuery({
        queryKey: ['cost', 'by-service'],
        queryFn: () => fetchJson<CostByService[]>('/api/v1/cost/by-service'),
        staleTime: 30_000,
    });
}

function useCostTimeSeries() {
    return useQuery({
        queryKey: ['cost', 'timeseries'],
        queryFn: () => fetchJson<CostTimeSeries[]>('/api/v1/cost/timeseries'),
        staleTime: 30_000,
    });
}

function useBudgetStatus() {
    return useQuery({
        queryKey: ['cost', 'budget'],
        queryFn: () => fetchJson<BudgetStatus>('/api/v1/cost/budget'),
        staleTime: 30_000,
    });
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function formatCost(value: number): string {
    if (value < 0.01) return `$${value.toFixed(4)}`;
    if (value < 1) return `$${value.toFixed(3)}`;
    return `$${value.toFixed(2)}`;
}

function formatTokens(n: number): string {
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
    return String(n);
}

// ── KPI Cards ────────────────────────────────────────────────────────────────

function KpiCard({
                     label,
                     value,
                     icon: Icon,
                     variant = 'default',
                 }: {
    label: string;
    value: string;
    icon: React.ElementType;
    variant?: 'default' | 'warning' | 'danger';
}) {
    return (
        <Card className={cn(
            'border-3',
            variant === 'warning' && 'border-signal-yellow/50',
            variant === 'danger' && 'border-signal-red/50',
        )}>
            <CardContent className="pt-4 pb-3 space-y-1">
                <div className="flex items-center gap-1.5">
                    <Icon className={cn(
                        'w-3.5 h-3.5',
                        variant === 'default' && 'text-signal-orange',
                        variant === 'warning' && 'text-signal-yellow',
                        variant === 'danger' && 'text-signal-red',
                    )}/>
                    <span className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                        {label}
                    </span>
                </div>
                <div className={cn(
                    'text-xl font-bold tracking-wider',
                    variant === 'default' && 'text-brutal-white',
                    variant === 'warning' && 'text-signal-yellow',
                    variant === 'danger' && 'text-signal-red',
                )}>
                    {value}
                </div>
            </CardContent>
        </Card>
    );
}

// ── ECharts cost timeseries ──────────────────────────────────────────────────

function CostTimeSeriesChart({data}: { data: CostTimeSeries[] }) {
    const chartRef = useRef<ReactEChartsCore>(null);

    const option = useMemo(() => {
        const modelSet = new Set(data.map(d => d.model));
        const models = [...modelSet];
        const buckets = [...new Set(data.map(d => d.bucket))].sort();

        const series = models.map((model, i) => {
            const modelData = data.filter(d => d.model === model);
            const costMap = new Map(modelData.map(d => [d.bucket, d.cost]));
            return {
                name: model,
                type: 'line' as const,
                smooth: true,
                symbol: 'none',
                areaStyle: {opacity: 0.15},
                lineStyle: {width: 2},
                data: buckets.map(b => costMap.get(b) ?? 0),
                color: [
                    'var(--color-signal-orange)',
                    'var(--color-signal-cyan)',
                    'var(--color-signal-green)',
                    'var(--color-signal-violet)',
                    'var(--color-signal-yellow)',
                ][i % 5],
            };
        });

        return {
            backgroundColor: 'transparent',
            grid: {left: 60, right: 16, top: 40, bottom: 60},
            tooltip: {
                trigger: 'axis' as const,
                backgroundColor: 'var(--color-brutal-black)',
                borderColor: 'var(--color-brutal-dark)',
                textStyle: {color: 'var(--color-brutal-white)', fontSize: 11},
                valueFormatter: (v: number) => formatCost(v),
            },
            legend: {
                top: 8,
                textStyle: {color: 'var(--color-brutal-zinc)', fontSize: 10},
            },
            dataZoom: [{type: 'inside'}],
            xAxis: {
                type: 'category' as const,
                data: buckets.map(b => {
                    const d = new Date(b);
                    return `${String(d.getHours()).padStart(2, '0')}:00`;
                }),
                axisLine: {lineStyle: {color: 'var(--color-brutal-dark)'}},
                axisLabel: {color: 'var(--color-brutal-zinc)', fontSize: 10},
            },
            yAxis: {
                type: 'value' as const,
                axisLine: {lineStyle: {color: 'var(--color-brutal-dark)'}},
                axisLabel: {color: 'var(--color-brutal-zinc)', fontSize: 10, formatter: (v: number) => formatCost(v)},
                splitLine: {lineStyle: {color: 'var(--color-brutal-dark)', type: 'dashed' as const}},
            },
            series,
        };
    }, [data]);

    return (
        <div className="border-3 border-brutal-zinc bg-brutal-carbon p-4 space-y-3">
            <div className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                COST OVER TIME (HOURLY)
            </div>
            <ReactEChartsCore
                ref={chartRef}
                echarts={echarts}
                option={option}
                style={{height: 280}}
                notMerge
            />
        </div>
    );
}

// ── TanStack cost breakdown table ────────────────────────────────────────────

const columnHelper = createColumnHelper<CostByModel>();

const columns = [
    columnHelper.accessor('model', {
        header: 'Model',
        cell: info => (
            <span className="font-semibold text-brutal-white">{info.getValue()}</span>
        ),
    }),
    columnHelper.accessor('provider', {
        header: 'Provider',
        cell: info => (
            <Badge variant="secondary" className="text-[10px] bg-brutal-zinc/30 border-brutal-zinc">
                {info.getValue()}
            </Badge>
        ),
    }),
    columnHelper.accessor('callCount', {
        header: 'Calls',
        cell: info => formatTokens(info.getValue()),
    }),
    columnHelper.accessor('totalInputTokens', {
        header: 'Input Tokens',
        cell: info => formatTokens(info.getValue()),
    }),
    columnHelper.accessor('totalOutputTokens', {
        header: 'Output Tokens',
        cell: info => formatTokens(info.getValue()),
    }),
    columnHelper.accessor('totalCost', {
        header: 'Total Cost',
        cell: info => (
            <span className="font-bold text-signal-orange">{formatCost(info.getValue())}</span>
        ),
    }),
];

function CostBreakdownTable({data}: { data: CostByModel[] }) {
    const [sorting, setSorting] = useState<SortingState>([{id: 'totalCost', desc: true}]);
    const [globalFilter, setGlobalFilter] = useState('');

    const table = useReactTable({
        data,
        columns,
        state: {sorting, globalFilter},
        onSortingChange: setSorting,
        onGlobalFilterChange: setGlobalFilter,
        getCoreRowModel: getCoreRowModel(),
        getSortedRowModel: getSortedRowModel(),
        getFilteredRowModel: getFilteredRowModel(),
        getPaginationRowModel: getPaginationRowModel(),
        initialState: {pagination: {pageSize: 15}},
    });

    return (
        <div className="border-3 border-brutal-zinc bg-brutal-carbon space-y-0">
            <div className="flex items-center justify-between px-4 py-3 border-b border-brutal-zinc/70">
                <span className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                    COST BY MODEL
                </span>
                <div className="flex items-center gap-2">
                    <Filter className="w-3.5 h-3.5 text-brutal-slate"/>
                    <input
                        type="text"
                        placeholder="Filter..."
                        value={globalFilter}
                        onChange={e => setGlobalFilter(e.target.value)}
                        className="bg-brutal-dark border border-brutal-zinc/70 px-2 py-1 text-[11px] text-brutal-white placeholder:text-brutal-zinc focus:border-signal-orange/50 outline-hidden focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal-orange w-40"
                        aria-label="Filter cost table"
                    />
                </div>
            </div>

            <div className="overflow-x-auto">
                <table className="w-full text-[11px]">
                    <thead>
                    {table.getHeaderGroups().map(headerGroup => (
                        <tr key={headerGroup.id} className="border-b border-brutal-zinc/70">
                            {headerGroup.headers.map(header => (
                                <th
                                    key={header.id}
                                    className="px-4 py-2 text-left font-bold text-brutal-slate tracking-widest uppercase cursor-pointer select-none hover:text-brutal-white"
                                    onClick={header.column.getToggleSortingHandler()}
                                >
                                    <div className="flex items-center gap-1">
                                        {flexRender(header.column.columnDef.header, header.getContext())}
                                        {header.column.getIsSorted() === 'asc' && <ArrowUp className="w-3 h-3"/>}
                                        {header.column.getIsSorted() === 'desc' && <ArrowDown className="w-3 h-3"/>}
                                    </div>
                                </th>
                            ))}
                        </tr>
                    ))}
                    </thead>
                    <tbody>
                    {table.getRowModel().rows.map(row => (
                        <tr key={row.id} className="border-b border-brutal-zinc/30 hover:bg-brutal-dark/60">
                            {row.getVisibleCells().map(cell => (
                                <td key={cell.id} className="px-4 py-2 text-brutal-slate">
                                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                                </td>
                            ))}
                        </tr>
                    ))}
                    {table.getRowModel().rows.length === 0 && (
                        <tr>
                            <td colSpan={columns.length}
                                className="px-4 py-8 text-center text-brutal-zinc text-xs font-bold tracking-widest">
                                NO COST DATA YET
                            </td>
                        </tr>
                    )}
                    </tbody>
                </table>
            </div>

            {table.getPageCount() > 1 && (
                <div className="flex items-center justify-between px-4 py-2 border-t border-brutal-zinc/70">
                    <span className="text-[10px] text-brutal-slate">
                        Page {table.getState().pagination.pageIndex + 1} of {table.getPageCount()}
                    </span>
                    <div className="flex gap-1">
                        <button
                            onClick={() => table.previousPage()}
                            disabled={!table.getCanPreviousPage()}
                            className="p-1 text-brutal-slate hover:text-brutal-white disabled:opacity-30"
                            aria-label="Previous page"
                        >
                            <ChevronLeft className="w-4 h-4"/>
                        </button>
                        <button
                            onClick={() => table.nextPage()}
                            disabled={!table.getCanNextPage()}
                            className="p-1 text-brutal-slate hover:text-brutal-white disabled:opacity-30"
                            aria-label="Next page"
                        >
                            <ChevronRight className="w-4 h-4"/>
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}

// ── CostPage ─────────────────────────────────────────────────────────────────

export function CostPage() {
    const {data: byModel, isLoading: loadingModel} = useCostByModel();
    const {data: byService, isLoading: loadingService} = useCostByService();
    const {data: timeseries, isLoading: loadingTs} = useCostTimeSeries();
    const {data: budget} = useBudgetStatus();

    const isLoading = loadingModel || loadingService || loadingTs;

    const totalSpendToday = useMemo(
        () => byModel?.reduce((sum, m) => sum + m.totalCost, 0) ?? 0,
        [byModel],
    );

    const topModel = useMemo(
        () => byModel?.reduce<CostByModel | null>((top, m) => (!top || m.totalCost > top.totalCost) ? m : top, null),
        [byModel],
    );

    const budgetVariant = budget?.status === 'exceeded' ? 'danger'
        : budget?.status === 'warning' ? 'warning'
            : 'default';

    return (
        <div className="flex-1 p-6 space-y-6 overflow-auto">
            <h1 className="text-lg font-bold text-brutal-white tracking-wider uppercase">
                COST
            </h1>

            {/* KPI cards */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                <KpiCard
                    label="Spend Today"
                    value={isLoading ? '...' : formatCost(totalSpendToday)}
                    icon={CircleDollarSign}
                />
                <KpiCard
                    label="Top Model"
                    value={isLoading ? '...' : (topModel?.model ?? 'N/A')}
                    icon={Zap}
                />
                <KpiCard
                    label="Total Calls"
                    value={isLoading ? '...' : formatTokens(byModel?.reduce((s, m) => s + m.callCount, 0) ?? 0)}
                    icon={Activity}
                />
                <KpiCard
                    label="Budget"
                    value={budget ? (budget.dailyBudget ? `${formatCost(budget.remaining ?? 0)} left` : 'No limit') : '...'}
                    icon={TriangleAlert}
                    variant={budgetVariant}
                />
            </div>

            {/* Timeseries chart */}
            {timeseries && timeseries.length > 0 && (
                <CostTimeSeriesChart data={timeseries}/>
            )}

            {/* Cost breakdown table */}
            {byModel && (
                <CostBreakdownTable data={byModel}/>
            )}

            {/* Service cost summary */}
            {byService && byService.length > 0 && (
                <div className="border-3 border-brutal-zinc bg-brutal-carbon p-4 space-y-3">
                    <div className="text-[10px] font-bold text-brutal-slate tracking-widest uppercase">
                        COST BY SERVICE
                    </div>
                    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2">
                        {byService.map(s => (
                            <div key={s.service}
                                 className="border border-brutal-zinc/50 bg-brutal-dark/50 px-3 py-2 space-y-1">
                                <div className="text-[11px] font-semibold text-brutal-white truncate">{s.service}</div>
                                <div className="flex items-center justify-between">
                                    <span
                                        className="text-[10px] text-brutal-slate">{formatTokens(s.callCount)} calls</span>
                                    <span
                                        className="text-[11px] font-bold text-signal-orange">{formatCost(s.totalCost)}</span>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/* Empty state */}
            {!isLoading && (!byModel || byModel.length === 0) && (
                <div className="flex items-center justify-center h-48 border-3 border-brutal-zinc bg-brutal-carbon">
                    <div className="text-center space-y-2">
                        <CircleDollarSign className="w-8 h-8 mx-auto text-brutal-zinc"/>
                        <div className="text-brutal-slate text-xs font-bold tracking-widest">
                            NO COST DATA YET
                        </div>
                        <div className="text-brutal-zinc text-[10px]">
                            Cost tracking starts when GenAI spans arrive with token counts.
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
