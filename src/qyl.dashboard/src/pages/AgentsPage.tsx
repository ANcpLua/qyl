import {useCallback, useMemo, useState} from 'react';
import {useSearchParams} from 'react-router-dom';
import {
    AlertTriangle,
    Bot,
    ChevronLeft,
    ChevronRight,
    Clock,
    Coins,
    Cpu,
    ExternalLink,
    Hash,
    Search,
    Wrench,
    X,
    Zap,
} from 'lucide-react';
import {
    Bar,
    BarChart,
    CartesianGrid,
    ComposedChart,
    Line,
    LineChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts';
import {cn} from '@/lib/utils';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {ScrollArea} from '@/components/ui/scroll-area';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import type {
    AgentTrace,
    DurationBucket,
    IssueItem,
    LegendEntry,
    ModelBucket,
    ModelSummary,
    TimeFilter,
    ToolBucket,
    ToolSummary,
    TrafficBucket,
} from '@/hooks/use-agent-insights';
import {
    useAgentDuration,
    useAgentIssues,
    useAgentLlmCalls,
    useAgentModels,
    useAgentTokens,
    useAgentToolCalls,
    useAgentTools,
    useAgentTraces,
    useAgentTraffic,
    useTraceSpans,
} from '@/hooks/use-agent-insights';

// ── Formatting helpers ─────────────────────────────────────────────────────────

function formatCompact(n: number): string {
    if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}b`;
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(0)}m`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
    return n.toString();
}

function formatDurationHuman(ms: number): string {
    if (ms >= 3600_000) return `${(ms / 3600_000).toFixed(2)}hr`;
    if (ms >= 60_000) return `${(ms / 60_000).toFixed(2)}min`;
    if (ms >= 1000) return `${(ms / 1000).toFixed(2)}s`;
    return `${ms.toFixed(0)}ms`;
}

function formatCost(usd: number): string {
    if (usd === 0) return '\u2014';
    return `$${usd.toFixed(4)}`;
}

function formatRelativeTime(isoString: string): string {
    const diff = Date.now() - new Date(isoString).getTime();
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1) return 'now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `${days}d ago`;
    const weeks = Math.floor(days / 7);
    return `${weeks}wk ago`;
}

function formatBucketTime(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleTimeString('en-US', {hour12: false, hour: '2-digit', minute: '2-digit'});
}

// ── Constants ──────────────────────────────────────────────────────────────────

const CHART_COLORS = [
    'oklch(0.60 0.25 300)', // signal-violet
    'oklch(0.70 0.20 45)',  // signal-orange
    'oklch(0.80 0.15 210)', // signal-cyan
    'oklch(0.85 0.25 145)', // signal-green
    'oklch(0.90 0.18 95)',  // signal-yellow
    'oklch(0.65 0.25 25)',  // signal-red
];

const DARKER_VIOLET = 'oklch(0.45 0.18 280)';

const CHART_GRID_STROKE = 'oklch(0.25 0 0)';
const CHART_AXIS_TICK = {fill: 'oklch(0.55 0 0)', fontSize: 10};
const CHART_AXIS_LINE = {stroke: 'oklch(0.30 0 0)'};

const TOOLTIP_STYLE: React.CSSProperties = {
    backgroundColor: 'oklch(0.18 0 0)',
    border: '2px solid oklch(0.50 0 0)',
    borderRadius: 0,
    color: 'oklch(0.97 0 0)',
    fontSize: 11,
    fontFamily: "'JetBrains Mono', monospace",
};

const PRESETS: { label: string; ms: number }[] = [
    {label: '24H', ms: 24 * 60 * 60 * 1000},
    {label: '7D', ms: 7 * 24 * 60 * 60 * 1000},
    {label: '30D', ms: 30 * 24 * 60 * 60 * 1000},
];

// ── Skeleton components ────────────────────────────────────────────────────────

function ChartSkeleton({className}: { className?: string }) {
    return (
        <div className={cn('bg-brutal-carbon border-2 border-brutal-zinc p-4', className)}>
            <div className="h-3 w-24 bg-brutal-zinc animate-pulse mb-4"/>
            <div className="space-y-2">
                {Array.from({length: 5}).map((_, i) => (
                    <div
                        key={i}
                        className="bg-brutal-zinc/30 animate-pulse"
                        style={{height: '12px', width: `${60 + Math.random() * 40}%`}}
                    />
                ))}
            </div>
        </div>
    );
}

function TableRowSkeleton() {
    return (
        <div className="flex items-center gap-3 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            <div className="w-20 h-4 bg-brutal-zinc/40"/>
            <div className="w-28 h-4 bg-brutal-zinc/40"/>
            <div className="w-16 h-4 bg-brutal-zinc/40"/>
            <div className="w-12 h-4 bg-brutal-zinc/40"/>
            <div className="w-12 h-4 bg-brutal-zinc/40"/>
            <div className="w-12 h-4 bg-brutal-zinc/40"/>
            <div className="w-16 h-4 bg-brutal-zinc/40"/>
            <div className="w-16 h-4 bg-brutal-zinc/40"/>
            <div className="flex-1"/>
            <div className="w-14 h-4 bg-brutal-zinc/40"/>
        </div>
    );
}

// ── Panel header ───────────────────────────────────────────────────────────────

function PanelHeader({title, icon: Icon}: { title: string; icon: React.ComponentType<{ className?: string }> }) {
    return (
        <div className="flex items-center gap-2 mb-3">
            <Icon className="w-3.5 h-3.5 text-brutal-slate"/>
            <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                {title}
            </span>
        </div>
    );
}

// ── Stagger fade-in wrapper ────────────────────────────────────────────────────

function FadeIn({delay = 0, children}: { delay?: number; children: React.ReactNode }) {
    return (
        <div
            className="animate-data-stream"
            style={{animationDelay: `${delay}ms`}}
        >
            {children}
        </div>
    );
}

// ── Overview: Traffic chart ────────────────────────────────────────────────────

function TrafficPanel({data, isLoading}: { data: TrafficBucket[] | undefined; isLoading: boolean }) {
    if (isLoading || !data) return <ChartSkeleton className="h-64"/>;

    return (
        <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64">
            <PanelHeader title="Traffic" icon={Zap}/>
            <div className="h-[calc(100%-2rem)]">
                <ResponsiveContainer width="100%" height="100%">
                    <ComposedChart data={data} margin={{top: 4, right: 8, bottom: 0, left: 0}}>
                        <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE}/>
                        <XAxis
                            dataKey="time"
                            tickFormatter={formatBucketTime}
                            tick={CHART_AXIS_TICK}
                            axisLine={CHART_AXIS_LINE}
                            tickLine={false}
                        />
                        <YAxis
                            yAxisId="left"
                            tick={CHART_AXIS_TICK}
                            axisLine={CHART_AXIS_LINE}
                            tickLine={false}
                            width={40}
                            tickFormatter={formatCompact}
                        />
                        <YAxis
                            yAxisId="right"
                            orientation="right"
                            tick={CHART_AXIS_TICK}
                            axisLine={CHART_AXIS_LINE}
                            tickLine={false}
                            width={40}
                            tickFormatter={(v: number) => `${(v * 100).toFixed(0)}%`}
                            domain={[0, 1]}
                        />
                        <Tooltip
                            contentStyle={TOOLTIP_STYLE}
                            formatter={((value: number, name: string) => {
                                if (name === 'errorRate') return [`${(value * 100).toFixed(1)}%`, 'Error Rate'];
                                return [formatCompact(value), name === 'runs' ? 'Runs' : 'Errors'];
                            }) as never}
                            labelFormatter={((label: string) => formatBucketTime(label)) as never}
                        />
                        <Bar
                            yAxisId="left"
                            dataKey="runs"
                            fill={DARKER_VIOLET}
                            name="runs"
                        />
                        <Bar
                            yAxisId="left"
                            dataKey="errors"
                            fill="oklch(0.65 0.25 25)"
                            name="errors"
                        />
                        <Line
                            yAxisId="right"
                            type="monotone"
                            dataKey="errorRate"
                            stroke="oklch(0.70 0.20 45)"
                            strokeWidth={2}
                            dot={false}
                            name="errorRate"
                        />
                    </ComposedChart>
                </ResponsiveContainer>
            </div>
        </div>
    );
}

// ── Overview: Duration chart ───────────────────────────────────────────────────

function DurationPanel({data, isLoading}: { data: DurationBucket[] | undefined; isLoading: boolean }) {
    if (isLoading || !data) return <ChartSkeleton className="h-64"/>;

    return (
        <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64">
            <PanelHeader title="Latency" icon={Clock}/>
            <div className="h-[calc(100%-2rem)]">
                <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={data} margin={{top: 4, right: 8, bottom: 0, left: 0}}>
                        <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE}/>
                        <XAxis
                            dataKey="time"
                            tickFormatter={formatBucketTime}
                            tick={CHART_AXIS_TICK}
                            axisLine={CHART_AXIS_LINE}
                            tickLine={false}
                        />
                        <YAxis
                            tick={CHART_AXIS_TICK}
                            axisLine={CHART_AXIS_LINE}
                            tickLine={false}
                            width={50}
                            tickFormatter={(v: number) => formatDurationHuman(v)}
                        />
                        <Tooltip
                            contentStyle={TOOLTIP_STYLE}
                            formatter={((value: number, name: string) => [
                                formatDurationHuman(value),
                                name === 'avgMs' ? 'Avg' : 'P95',
                            ]) as never}
                            labelFormatter={((label: string) => formatBucketTime(label)) as never}
                        />
                        <Line
                            type="monotone"
                            dataKey="avgMs"
                            stroke="oklch(0.85 0.25 145)"
                            strokeWidth={2}
                            dot={false}
                            name="avgMs"
                        />
                        <Line
                            type="monotone"
                            dataKey="p95Ms"
                            stroke="oklch(0.70 0.20 45)"
                            strokeWidth={2}
                            dot={false}
                            strokeDasharray="4 2"
                            name="p95Ms"
                        />
                    </LineChart>
                </ResponsiveContainer>
            </div>
        </div>
    );
}

// ── Overview: Issues panel ─────────────────────────────────────────────────────

function IssuesPanel({data, isLoading}: { data: IssueItem[] | undefined; isLoading: boolean }) {
    if (isLoading || !data) return <ChartSkeleton className="h-64"/>;

    return (
        <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64 flex flex-col">
            <PanelHeader title="Top Issues" icon={AlertTriangle}/>
            {data.length === 0 ? (
                <div className="flex-1 flex items-center justify-center">
                    <span className="text-xs text-brutal-slate">No issues detected</span>
                </div>
            ) : (
                <ScrollArea className="flex-1 -mx-1 px-1">
                    <div className="space-y-2">
                        {data.slice(0, 8).map((issue, i) => (
                            <div
                                key={i}
                                className="flex items-start gap-2 p-2 border border-signal-red/30 bg-signal-red/5 hover:bg-signal-red/10 transition-colors"
                            >
                                <AlertTriangle className="w-3.5 h-3.5 text-signal-red mt-0.5 flex-shrink-0"/>
                                <div className="min-w-0 flex-1">
                                    <p className="text-xs text-signal-red font-bold truncate" title={issue.error}>
                                        {issue.error}
                                    </p>
                                    <span className="text-[10px] text-brutal-slate">
                                        {issue.count} occurrence{issue.count !== 1 ? 's' : ''}
                                    </span>
                                </div>
                            </div>
                        ))}
                    </div>
                </ScrollArea>
            )}
        </div>
    );
}

// ── Stacked bar chart (shared by LLM Calls, Tokens, Tool Calls) ───────────────

interface StackedBarPanelProps {
    title: string;
    icon: React.ComponentType<{ className?: string }>;
    buckets: ModelBucket[] | ToolBucket[] | undefined;
    legend: LegendEntry[] | undefined;
    nameKey: 'models' | 'tools';
    isLoading: boolean;
}

function StackedBarPanel({title, icon, buckets, legend, nameKey, isLoading}: StackedBarPanelProps) {
    if (isLoading || !buckets || !legend) return <ChartSkeleton className="h-64"/>;

    // Flatten bucket data for recharts: each bucket needs a flat object with keys per model/tool
    const names = legend.map(l => l.name);
    const chartData = buckets.map((b) => {
        const point: Record<string, string | number> = {time: b.time};
        const dict = nameKey === 'models'
            ? (b as ModelBucket).models
            : (b as ToolBucket).tools;
        for (const name of names) {
            point[name] = dict[name] ?? 0;
        }
        return point;
    });

    return (
        <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64 flex flex-col">
            <PanelHeader title={title} icon={icon}/>
            <div className="flex-1 min-h-0">
                <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={chartData} margin={{top: 4, right: 4, bottom: 0, left: 0}}>
                        <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE}/>
                        <XAxis
                            dataKey="time"
                            tickFormatter={formatBucketTime}
                            tick={CHART_AXIS_TICK}
                            axisLine={CHART_AXIS_LINE}
                            tickLine={false}
                        />
                        <YAxis
                            tick={CHART_AXIS_TICK}
                            axisLine={CHART_AXIS_LINE}
                            tickLine={false}
                            width={40}
                            tickFormatter={formatCompact}
                        />
                        <Tooltip
                            contentStyle={TOOLTIP_STYLE}
                            formatter={((value: number, name: string) => [formatCompact(value), name]) as never}
                            labelFormatter={((label: string) => formatBucketTime(label)) as never}
                        />
                        {names.map((name, i) => (
                            <Bar
                                key={name}
                                dataKey={name}
                                stackId="stack"
                                fill={CHART_COLORS[i % CHART_COLORS.length]}
                                name={name}
                            />
                        ))}
                    </BarChart>
                </ResponsiveContainer>
            </div>
            {/* Legend strip */}
            <div className="flex flex-wrap gap-x-4 gap-y-1 mt-2">
                {legend.map((entry, i) => (
                    <div key={entry.name} className="flex items-center gap-1.5">
                        <div
                            className="w-2 h-2"
                            style={{backgroundColor: CHART_COLORS[i % CHART_COLORS.length]}}
                        />
                        <span className="text-[10px] text-brutal-slate truncate max-w-[120px]" title={entry.name}>
                            {entry.name}
                        </span>
                        <span className="text-[10px] text-brutal-zinc font-bold">
                            {formatCompact(entry.total)}
                        </span>
                    </div>
                ))}
            </div>
        </div>
    );
}

// ── Trace list table ───────────────────────────────────────────────────────────

interface TraceTableProps {
    traces: AgentTrace[] | undefined;
    total: number;
    isLoading: boolean;
    offset: number;
    limit: number;
    onPageChange: (offset: number) => void;
    onSelectTrace: (traceId: string) => void;
}

function TraceTable({traces, total, isLoading, offset, limit, onPageChange, onSelectTrace}: TraceTableProps) {
    const page = Math.floor(offset / limit) + 1;
    const totalPages = Math.ceil(total / limit);

    return (
        <div className="border-2 border-brutal-zinc bg-brutal-carbon">
            {/* Header */}
            <div className="flex items-center gap-2 px-4 py-2 border-b-2 border-brutal-zinc">
                <Bot className="w-3.5 h-3.5 text-signal-orange"/>
                <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                    Agent Traces
                </span>
                <span className="text-[10px] text-brutal-zinc ml-auto">
                    {total} total
                </span>
            </div>

            {/* Column headers */}
            <div
                className="grid grid-cols-[80px_1fr_80px_60px_60px_60px_80px_70px_70px] gap-2 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider uppercase">
                <div>TRACE</div>
                <div>AGENT / ROOT</div>
                <div className="text-right">DURATION</div>
                <div className="text-right">ERRORS</div>
                <div className="text-right">LLM</div>
                <div className="text-right">TOOLS</div>
                <div className="text-right">TOKENS</div>
                <div className="text-right">COST</div>
                <div className="text-right">WHEN</div>
            </div>

            {/* Rows */}
            {isLoading ? (
                Array.from({length: 5}).map((_, i) => <TableRowSkeleton key={i}/>)
            ) : !traces || traces.length === 0 ? (
                <div className="py-12 text-center">
                    <Bot className="w-10 h-10 mx-auto mb-3 text-brutal-zinc"/>
                    <p className="text-xs text-brutal-slate">No agent traces found in this time range</p>
                </div>
            ) : (
                traces.map((t) => (
                    <div
                        key={t.traceId}
                        className="grid grid-cols-[80px_1fr_80px_60px_60px_60px_80px_70px_70px] gap-2 px-4 py-2.5 border-b border-brutal-zinc/50 hover:bg-brutal-dark/50 cursor-pointer transition-colors group"
                        onClick={() => onSelectTrace(t.traceId)}
                    >
                        <div
                            className="font-mono text-xs text-signal-violet group-hover:text-signal-orange transition-colors truncate">
                            {t.traceId.slice(0, 8)}
                        </div>
                        <div className="min-w-0">
                            {t.agentName && (
                                <Badge variant="outline"
                                       className="text-[10px] text-signal-cyan border-signal-cyan/40 mr-1.5 px-1.5 py-0">
                                    {t.agentName}
                                </Badge>
                            )}
                            <span className="text-xs text-brutal-slate truncate">
                                {t.rootName || '\u2014'}
                            </span>
                        </div>
                        <div className="text-right font-mono text-xs text-brutal-slate">
                            {formatDurationHuman(t.rootDurationMs)}
                        </div>
                        <div
                            className={cn('text-right font-mono text-xs', t.errors > 0 ? 'text-signal-red font-bold' : 'text-brutal-zinc')}>
                            {t.errors}
                        </div>
                        <div className="text-right font-mono text-xs text-signal-green">
                            {t.llmCalls}
                        </div>
                        <div className="text-right font-mono text-xs text-brutal-slate">
                            {t.toolCalls}
                        </div>
                        <div className="text-right font-mono text-xs text-brutal-slate">
                            {formatCompact(t.totalTokens)}
                        </div>
                        <div className="text-right font-mono text-xs text-brutal-slate">
                            {formatCost(t.totalCost)}
                        </div>
                        <div className="text-right text-[10px] text-brutal-zinc">
                            {formatRelativeTime(t.timestamp)}
                        </div>
                    </div>
                ))
            )}

            {/* Pagination */}
            {totalPages > 1 && (
                <div className="flex items-center justify-between px-4 py-2 border-t-2 border-brutal-zinc">
                    <span className="text-[10px] text-brutal-slate">
                        Page {page} of {totalPages}
                    </span>
                    <div className="flex gap-1">
                        <Button
                            variant="ghost"
                            size="sm"
                            className="h-7 w-7 p-0 text-brutal-slate hover:text-brutal-white"
                            disabled={offset === 0}
                            onClick={() => onPageChange(Math.max(0, offset - limit))}
                        >
                            <ChevronLeft className="w-4 h-4"/>
                        </Button>
                        <Button
                            variant="ghost"
                            size="sm"
                            className="h-7 w-7 p-0 text-brutal-slate hover:text-brutal-white"
                            disabled={offset + limit >= total}
                            onClick={() => onPageChange(offset + limit)}
                        >
                            <ChevronRight className="w-4 h-4"/>
                        </Button>
                    </div>
                </div>
            )}
        </div>
    );
}

// ── Trace detail slide-in ──────────────────────────────────────────────────────

function TraceSlideIn({
                          traceId,
                          onClose,
                      }: {
    traceId: string;
    onClose: () => void;
}) {
    const {data, isLoading} = useTraceSpans(traceId);
    const [selectedSpanId, setSelectedSpanId] = useState<string | null>(null);

    const spans = data?.spans ?? [];

    // Find root span (no parentSpanId) to determine trace start time
    const rootSpan = spans.find(s => !s.parentSpanId);
    const traceStart = rootSpan ? new Date(rootSpan.timestamp).getTime() : 0;
    const traceEnd = rootSpan ? traceStart + rootSpan.durationMs : 0;
    const traceDuration = traceEnd - traceStart;

    // Filter to AI-relevant spans (gen_ai.* and tool spans)
    const aiSpans = spans.filter(s => {
        const name = s.name.toLowerCase();
        return s.provider || s.model || s.toolName
            || name.includes('gen_ai') || name.includes('chat')
            || name.includes('tool') || name.includes('llm')
            || name.includes('completion') || name.includes('invoke');
    });
    const displaySpans = aiSpans.length > 0 ? aiSpans : spans;

    const selectedSpan = selectedSpanId
        ? spans.find(s => s.spanId === selectedSpanId) ?? null
        : null;

    return (
        <>
            {/* Overlay */}
            <div
                className="fixed inset-0 bg-brutal-black/60 z-40"
                onClick={onClose}
            />
            {/* Panel */}
            <div
                className="fixed top-0 right-0 bottom-0 w-[clamp(600px,55vw,900px)] z-50 bg-brutal-carbon border-l-3 border-brutal-zinc flex flex-col shadow-brutal-lg animate-data-stream">
                {/* Header */}
                <div
                    className="flex items-center justify-between px-5 py-3 border-b-3 border-brutal-zinc bg-brutal-dark">
                    <div className="flex items-center gap-3">
                        <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                            TRACE
                        </span>
                        <span className="font-mono text-xs text-signal-violet">
                            {traceId.slice(0, 16)}...
                        </span>
                        <a
                            href={`/traces?traceId=${traceId}`}
                            target="_blank"
                            rel="noreferrer"
                            className="text-brutal-slate hover:text-signal-orange transition-colors"
                            title="Open in Traces view"
                        >
                            <ExternalLink className="w-3.5 h-3.5"/>
                        </a>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-brutal-slate hover:text-signal-orange transition-colors p-1"
                        aria-label="Close trace detail"
                    >
                        <X className="w-5 h-5"/>
                    </button>
                </div>

                {/* Body */}
                <div className="flex-1 flex min-h-0">
                    {/* Waterfall */}
                    <div className="flex-1 border-r border-brutal-zinc overflow-hidden flex flex-col">
                        <div className="px-4 py-2 border-b border-brutal-zinc">
                            <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                                AI SPANS ({displaySpans.length})
                            </span>
                        </div>
                        <ScrollArea className="flex-1">
                            {isLoading ? (
                                <div className="p-4 space-y-2">
                                    {Array.from({length: 6}).map((_, i) => (
                                        <div key={i} className="h-8 bg-brutal-zinc/20 animate-pulse"/>
                                    ))}
                                </div>
                            ) : (
                                <div className="p-2">
                                    {displaySpans.map((span) => {
                                        const spanStart = new Date(span.timestamp).getTime() - traceStart;
                                        const leftPct = traceDuration > 0 ? (spanStart / traceDuration) * 100 : 0;
                                        const widthPct = traceDuration > 0 ? Math.max((span.durationMs / traceDuration) * 100, 1) : 100;
                                        const isSelected = span.spanId === selectedSpanId;
                                        const isError = span.statusCode === 2;
                                        const barColor = isError
                                            ? 'oklch(0.65 0.25 25)'
                                            : span.toolName
                                                ? 'oklch(0.80 0.15 210)'
                                                : 'oklch(0.60 0.25 300)';

                                        return (
                                            <div
                                                key={span.spanId}
                                                className={cn(
                                                    'flex items-center gap-2 px-2 py-1.5 cursor-pointer transition-colors border-l-2',
                                                    isSelected
                                                        ? 'bg-signal-orange/10 border-signal-orange'
                                                        : 'border-transparent hover:bg-brutal-dark/50'
                                                )}
                                                onClick={() => setSelectedSpanId(isSelected ? null : span.spanId)}
                                            >
                                                <div className="w-[140px] flex-shrink-0">
                                                    <p className="text-[11px] text-brutal-white truncate"
                                                       title={span.name}>
                                                        {span.name}
                                                    </p>
                                                    <p className="text-[10px] text-brutal-zinc">
                                                        {formatDurationHuman(span.durationMs)}
                                                    </p>
                                                </div>
                                                <div className="flex-1 h-5 bg-brutal-black relative">
                                                    <div
                                                        className="absolute top-0 bottom-0 waterfall-bar"
                                                        style={{
                                                            left: `${Math.min(leftPct, 99)}%`,
                                                            width: `${Math.min(widthPct, 100 - leftPct)}%`,
                                                            backgroundColor: barColor,
                                                        }}
                                                    />
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>
                            )}
                        </ScrollArea>
                    </div>

                    {/* Span detail */}
                    <div className="w-[280px] flex-shrink-0 overflow-hidden flex flex-col">
                        <div className="px-4 py-2 border-b border-brutal-zinc">
                            <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                                SPAN DETAIL
                            </span>
                        </div>
                        <ScrollArea className="flex-1">
                            {selectedSpan ? (
                                <div className="p-4 space-y-3 text-xs">
                                    <DetailRow label="SPAN ID" value={selectedSpan.spanId.slice(0, 16)} mono/>
                                    <DetailRow label="NAME" value={selectedSpan.name}/>
                                    <DetailRow label="DURATION" value={formatDurationHuman(selectedSpan.durationMs)}
                                               mono/>
                                    <DetailRow label="STATUS" value={selectedSpan.statusCode === 2 ? 'ERROR' : 'OK'}
                                               color={selectedSpan.statusCode === 2 ? 'text-signal-red' : 'text-signal-green'}/>
                                    {selectedSpan.statusMessage && (
                                        <DetailRow label="MESSAGE" value={selectedSpan.statusMessage}/>
                                    )}
                                    {selectedSpan.provider &&
                                        <DetailRow label="PROVIDER" value={selectedSpan.provider}/>}
                                    {selectedSpan.model && <DetailRow label="MODEL" value={selectedSpan.model}/>}
                                    {selectedSpan.inputTokens != null && (
                                        <DetailRow label="INPUT TOKENS" value={formatCompact(selectedSpan.inputTokens)}
                                                   mono/>
                                    )}
                                    {selectedSpan.outputTokens != null && (
                                        <DetailRow label="OUTPUT TOKENS"
                                                   value={formatCompact(selectedSpan.outputTokens)} mono/>
                                    )}
                                    {selectedSpan.toolName && <DetailRow label="TOOL" value={selectedSpan.toolName}/>}
                                    {selectedSpan.cost != null && selectedSpan.cost > 0 && (
                                        <DetailRow label="COST" value={formatCost(selectedSpan.cost)} mono/>
                                    )}
                                    {selectedSpan.stopReason &&
                                        <DetailRow label="STOP REASON" value={selectedSpan.stopReason}/>}
                                    {selectedSpan.attributesJson && (
                                        <div>
                                            <div
                                                className="text-[10px] font-bold tracking-[0.15em] uppercase text-brutal-slate mb-1">
                                                ATTRIBUTES
                                            </div>
                                            <pre
                                                className="text-[10px] text-brutal-slate bg-brutal-black p-2 border border-brutal-zinc overflow-x-auto whitespace-pre-wrap break-all max-h-40 overflow-y-auto">
                                                {JSON.stringify(JSON.parse(selectedSpan.attributesJson), null, 2)}
                                            </pre>
                                        </div>
                                    )}
                                </div>
                            ) : (
                                <div className="p-4 text-center text-xs text-brutal-slate mt-8">
                                    Click a span to view details
                                </div>
                            )}
                        </ScrollArea>
                    </div>
                </div>
            </div>
        </>
    );
}

function DetailRow({label, value, mono, color}: { label: string; value: string; mono?: boolean; color?: string }) {
    return (
        <div>
            <div className="text-[10px] font-bold tracking-[0.15em] uppercase text-brutal-slate mb-0.5">
                {label}
            </div>
            <div className={cn('text-xs break-all', mono && 'font-mono', color ?? 'text-brutal-white')}>
                {value}
            </div>
        </div>
    );
}

// ── Models tab ─────────────────────────────────────────────────────────────────

function ModelsTab({filter}: { filter: TimeFilter }) {
    const {data, isLoading} = useAgentModels(filter);

    return (
        <div className="space-y-6">
            {/* Table */}
            <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                <div className="flex items-center gap-2 px-4 py-2 border-b-2 border-brutal-zinc">
                    <Cpu className="w-3.5 h-3.5 text-signal-violet"/>
                    <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                        Model Breakdown
                    </span>
                </div>
                <div
                    className="grid grid-cols-[1fr_80px_90px_90px_80px_90px_80px] gap-2 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider uppercase">
                    <div>MODEL</div>
                    <div className="text-right">CALLS</div>
                    <div className="text-right">INPUT TOK</div>
                    <div className="text-right">OUTPUT TOK</div>
                    <div className="text-right">COST</div>
                    <div className="text-right">AVG LATENCY</div>
                    <div className="text-right">ERR RATE</div>
                </div>
                {isLoading ? (
                    Array.from({length: 4}).map((_, i) => <TableRowSkeleton key={i}/>)
                ) : !data?.models?.length ? (
                    <div className="py-10 text-center text-xs text-brutal-slate">No model data found</div>
                ) : (
                    data.models.map((m) => (
                        <ModelRow key={m.name} model={m}/>
                    ))
                )}
            </div>

            {/* Time-series chart */}
            {data?.timeseries && data.legend && (
                <FadeIn delay={100}>
                    <StackedBarPanel
                        title="Model Usage Over Time"
                        icon={Cpu}
                        buckets={data.timeseries}
                        legend={data.legend}
                        nameKey="models"
                        isLoading={false}
                    />
                </FadeIn>
            )}
        </div>
    );
}

function ModelRow({model}: { model: ModelSummary }) {
    return (
        <div
            className="grid grid-cols-[1fr_80px_90px_90px_80px_90px_80px] gap-2 px-4 py-2.5 border-b border-brutal-zinc/50 hover:bg-brutal-dark/30 transition-colors">
            <div className="text-xs text-brutal-white font-bold truncate" title={model.name}>
                {model.name}
            </div>
            <div className="text-right font-mono text-xs text-brutal-slate">
                {formatCompact(model.calls)}
            </div>
            <div className="text-right font-mono text-xs text-brutal-slate">
                {formatCompact(model.inputTokens)}
            </div>
            <div className="text-right font-mono text-xs text-brutal-slate">
                {formatCompact(model.outputTokens)}
            </div>
            <div className="text-right font-mono text-xs text-signal-green">
                {formatCost(model.cost)}
            </div>
            <div className="text-right font-mono text-xs text-brutal-slate">
                {formatDurationHuman(model.avgDurationMs)}
            </div>
            <div
                className={cn('text-right font-mono text-xs', model.errorRate > 0.05 ? 'text-signal-red' : 'text-brutal-zinc')}>
                {(model.errorRate * 100).toFixed(1)}%
            </div>
        </div>
    );
}

// ── Tools tab ──────────────────────────────────────────────────────────────────

function ToolsTab({filter}: { filter: TimeFilter }) {
    const {data, isLoading} = useAgentTools(filter);

    return (
        <div className="space-y-6">
            {/* Table */}
            <div className="border-2 border-brutal-zinc bg-brutal-carbon">
                <div className="flex items-center gap-2 px-4 py-2 border-b-2 border-brutal-zinc">
                    <Wrench className="w-3.5 h-3.5 text-signal-cyan"/>
                    <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                        Tool Breakdown
                    </span>
                </div>
                <div
                    className="grid grid-cols-[1fr_100px_100px_100px] gap-2 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider uppercase">
                    <div>TOOL</div>
                    <div className="text-right">CALLS</div>
                    <div className="text-right">AVG LATENCY</div>
                    <div className="text-right">ERR RATE</div>
                </div>
                {isLoading ? (
                    Array.from({length: 4}).map((_, i) => <TableRowSkeleton key={i}/>)
                ) : !data?.tools?.length ? (
                    <div className="py-10 text-center text-xs text-brutal-slate">No tool data found</div>
                ) : (
                    data.tools.map((t) => (
                        <ToolRow key={t.name} tool={t}/>
                    ))
                )}
            </div>

            {/* Time-series chart */}
            {data?.timeseries && data.legend && (
                <FadeIn delay={100}>
                    <StackedBarPanel
                        title="Tool Usage Over Time"
                        icon={Wrench}
                        buckets={data.timeseries}
                        legend={data.legend}
                        nameKey="tools"
                        isLoading={false}
                    />
                </FadeIn>
            )}
        </div>
    );
}

function ToolRow({tool}: { tool: ToolSummary }) {
    return (
        <div
            className="grid grid-cols-[1fr_100px_100px_100px] gap-2 px-4 py-2.5 border-b border-brutal-zinc/50 hover:bg-brutal-dark/30 transition-colors">
            <div className="text-xs text-brutal-white font-bold truncate" title={tool.name}>
                {tool.name}
            </div>
            <div className="text-right font-mono text-xs text-brutal-slate">
                {formatCompact(tool.calls)}
            </div>
            <div className="text-right font-mono text-xs text-brutal-slate">
                {formatDurationHuman(tool.avgDurationMs)}
            </div>
            <div
                className={cn('text-right font-mono text-xs', tool.errorRate > 0.05 ? 'text-signal-red' : 'text-brutal-zinc')}>
                {(tool.errorRate * 100).toFixed(1)}%
            </div>
        </div>
    );
}

// ── Overview tab (6 panels + trace table) ──────────────────────────────────────

function OverviewTab({filter}: { filter: TimeFilter }) {
    const traffic = useAgentTraffic(filter);
    const duration = useAgentDuration(filter);
    const issues = useAgentIssues(filter);
    const llmCalls = useAgentLlmCalls(filter);
    const tokens = useAgentTokens(filter);
    const toolCalls = useAgentToolCalls(filter);

    const [traceOffset, setTraceOffset] = useState(0);
    const traceLimit = 50;
    const tracesQuery = useAgentTraces(filter, traceLimit, traceOffset);

    const [selectedTraceId, setSelectedTraceId] = useState<string | null>(null);

    return (
        <div className="space-y-6">
            {/* 6-panel chart grid */}
            <div className="grid grid-cols-3 gap-4">
                <FadeIn delay={0}>
                    <TrafficPanel data={traffic.data?.buckets} isLoading={traffic.isLoading}/>
                </FadeIn>
                <FadeIn delay={50}>
                    <DurationPanel data={duration.data?.buckets} isLoading={duration.isLoading}/>
                </FadeIn>
                <FadeIn delay={100}>
                    <IssuesPanel data={issues.data?.issues} isLoading={issues.isLoading}/>
                </FadeIn>
                <FadeIn delay={150}>
                    <StackedBarPanel
                        title="LLM Calls"
                        icon={Cpu}
                        buckets={llmCalls.data?.buckets}
                        legend={llmCalls.data?.legend}
                        nameKey="models"
                        isLoading={llmCalls.isLoading}
                    />
                </FadeIn>
                <FadeIn delay={200}>
                    <StackedBarPanel
                        title="Tokens Used"
                        icon={Coins}
                        buckets={tokens.data?.buckets}
                        legend={tokens.data?.legend}
                        nameKey="models"
                        isLoading={tokens.isLoading}
                    />
                </FadeIn>
                <FadeIn delay={250}>
                    <StackedBarPanel
                        title="Tool Calls"
                        icon={Wrench}
                        buckets={toolCalls.data?.buckets}
                        legend={toolCalls.data?.legend}
                        nameKey="tools"
                        isLoading={toolCalls.isLoading}
                    />
                </FadeIn>
            </div>

            {/* Trace list */}
            <FadeIn delay={300}>
                <TraceTable
                    traces={tracesQuery.data?.items}
                    total={tracesQuery.data?.total ?? 0}
                    isLoading={tracesQuery.isLoading}
                    offset={traceOffset}
                    limit={traceLimit}
                    onPageChange={setTraceOffset}
                    onSelectTrace={setSelectedTraceId}
                />
            </FadeIn>

            {/* Trace detail slide-in */}
            {selectedTraceId && (
                <TraceSlideIn
                    traceId={selectedTraceId}
                    onClose={() => setSelectedTraceId(null)}
                />
            )}
        </div>
    );
}

// ── Main page ──────────────────────────────────────────────────────────────────

export function AgentsPage() {
    const [searchParams, setSearchParams] = useSearchParams();

    // Tab state from URL
    const activeTab = searchParams.get('tab') ?? 'overview';
    const setActiveTab = useCallback((tab: string) => {
        setSearchParams(prev => {
            const next = new URLSearchParams(prev);
            next.set('tab', tab);
            return next;
        }, {replace: true});
    }, [setSearchParams]);

    // Time range from URL with default 24h
    const presetMs = Number(searchParams.get('range')) || PRESETS[0].ms;
    const filter: TimeFilter = useMemo(() => {
        const to = Date.now();
        const from = to - presetMs;
        return {from, to};
    }, [presetMs]);

    const setPreset = useCallback((ms: number) => {
        setSearchParams(prev => {
            const next = new URLSearchParams(prev);
            next.set('range', String(ms));
            return next;
        }, {replace: true});
    }, [setSearchParams]);

    // Search filter (local state, not URL for performance)
    const [search, setSearch] = useState('');

    return (
        <div className="p-6 space-y-5">
            {/* Page header + filter bar */}
            <div className="flex items-center justify-between gap-4">
                <div className="flex items-center gap-3">
                    <Bot className="w-5 h-5 text-signal-orange"/>
                    <h1 className="text-lg font-bold tracking-[0.15em] uppercase text-brutal-white">
                        AGENTS
                    </h1>
                </div>

                <div className="flex items-center gap-3">
                    {/* Search */}
                    <div className="relative">
                        <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-brutal-slate"/>
                        <Input
                            placeholder="Search..."
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                            className="h-8 w-48 pl-8 text-xs bg-brutal-dark border-brutal-zinc"
                            aria-label="Search agents"
                        />
                    </div>

                    {/* Time range presets */}
                    <div className="flex border-2 border-brutal-zinc">
                        {PRESETS.map(p => (
                            <button
                                key={p.label}
                                onClick={() => setPreset(p.ms)}
                                className={cn(
                                    'px-3 py-1 text-[10px] font-bold tracking-wider transition-colors',
                                    presetMs === p.ms
                                        ? 'bg-signal-orange text-brutal-black'
                                        : 'bg-brutal-dark text-brutal-slate hover:text-brutal-white hover:bg-brutal-zinc/30'
                                )}
                            >
                                {p.label}
                            </button>
                        ))}
                    </div>
                </div>
            </div>

            {/* Tabs */}
            <Tabs value={activeTab} onValueChange={setActiveTab}>
                <TabsList className="bg-brutal-dark border-2 border-brutal-zinc p-0 h-auto">
                    <TabsTrigger
                        value="overview"
                        className="text-[10px] font-bold tracking-[0.15em] uppercase px-4 py-2 data-[state=active]:bg-signal-orange/20 data-[state=active]:text-signal-orange data-[state=active]:border-b-2 data-[state=active]:border-signal-orange text-brutal-slate hover:text-brutal-white transition-colors"
                    >
                        <Hash className="w-3.5 h-3.5 mr-1.5"/>
                        Overview
                    </TabsTrigger>
                    <TabsTrigger
                        value="models"
                        className="text-[10px] font-bold tracking-[0.15em] uppercase px-4 py-2 data-[state=active]:bg-signal-violet/20 data-[state=active]:text-signal-violet data-[state=active]:border-b-2 data-[state=active]:border-signal-violet text-brutal-slate hover:text-brutal-white transition-colors"
                    >
                        <Cpu className="w-3.5 h-3.5 mr-1.5"/>
                        Models
                    </TabsTrigger>
                    <TabsTrigger
                        value="tools"
                        className="text-[10px] font-bold tracking-[0.15em] uppercase px-4 py-2 data-[state=active]:bg-signal-cyan/20 data-[state=active]:text-signal-cyan data-[state=active]:border-b-2 data-[state=active]:border-signal-cyan text-brutal-slate hover:text-brutal-white transition-colors"
                    >
                        <Wrench className="w-3.5 h-3.5 mr-1.5"/>
                        Tools
                    </TabsTrigger>
                </TabsList>

                <TabsContent value="overview" className="mt-4">
                    <OverviewTab filter={filter}/>
                </TabsContent>
                <TabsContent value="models" className="mt-4">
                    <ModelsTab filter={filter}/>
                </TabsContent>
                <TabsContent value="tools" className="mt-4">
                    <ToolsTab filter={filter}/>
                </TabsContent>
            </Tabs>
        </div>
    );
}
