import {cloneElement, useEffect, useMemo, useRef, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {
    Activity,
    AlertCircle,
    ChevronDown,
    ChevronRight,
    Coins,
    Cpu,
    DollarSign,
    FileJson,
    Loader2,
    MessageSquare,
    Sparkles,
    Wrench,
    Zap,
} from 'lucide-react';
import {Area, AreaChart, Bar, BarChart, CartesianGrid, Tooltip, XAxis, YAxis,} from 'recharts';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Tabs, TabsContent, TabsList, TabsTrigger} from '@/components/ui/tabs';
import {Separator} from '@/components/ui/separator';
import {CopyableText, DownloadButton, TextVisualizer} from '@/components/ui';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import {extractToolCallInfo, hasToolDefinitions, ToolCallViewer, ToolDefinitionsViewer,} from '@/components/genai';
import type {TimeFilter} from '@/hooks/use-agent-insights';
import {useAgentLlmCalls, useAgentTokens} from '@/hooks/use-agent-insights';

interface GenAiStats {
    requestCount: number;
    totalInputTokens: number;
    totalOutputTokens: number;
    totalCostUsd: number;
    averageEvalScore?: number;
}

interface GenAiSpan {
    spanId: string;
    traceId: string;
    name: string;
    kind: number;
    startTimeUnixNano: number;
    endTimeUnixNano: number;
    durationNs: number;
    statusCode: number;
    statusMessage?: string;
    serviceName?: string;
    genAiProviderName?: string;
    genAiRequestModel?: string;
    genAiResponseModel?: string;
    genAiInputTokens?: number;
    genAiOutputTokens?: number;
    genAiTemperature?: number;
    genAiStopReason?: string;
    genAiToolName?: string;
    genAiToolCallId?: string;
    genAiCostUsd?: number;
    attributesJson?: string;
}

interface GenAiSpansResponse {
    spans: GenAiSpan[];
    total: number;
}

async function fetchGenAiStats(): Promise<GenAiStats> {
    const res = await fetch('/api/v1/genai/stats');
    if (!res.ok) throw new Error('Failed to fetch GenAI stats');
    return res.json();
}

async function fetchGenAiSpans(limit = 50): Promise<GenAiSpansResponse> {
    const res = await fetch(`/api/v1/genai/spans?limit=${limit}`);
    if (!res.ok) throw new Error('Failed to fetch GenAI spans');
    return res.json();
}

const CHART_COLORS = [
    'var(--color-signal-violet)',
    'var(--color-signal-orange)',
    'var(--color-signal-cyan)',
    'var(--color-signal-green)',
    'var(--color-signal-yellow)',
    'var(--color-signal-red)',
];

const CHART_GRID_STROKE = 'var(--color-brutal-dark)';
const CHART_AXIS_TICK = {fill: 'var(--color-brutal-zinc)', fontSize: 10};
const CHART_AXIS_LINE = {stroke: 'var(--color-brutal-dark)'};

const TOOLTIP_STYLE: React.CSSProperties = {
    backgroundColor: 'var(--color-brutal-dark)',
    border: '2px solid var(--color-brutal-zinc)',
    borderRadius: 0,
    color: 'var(--color-brutal-white)',
    fontSize: 11,
    fontFamily: "'JetBrains Mono', monospace",
};

function formatCompact(n: number): string {
    if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}b`;
    if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(0)}m`;
    if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
    return n.toString();
}

function formatBucketTime(iso: string): string {
    const d = new Date(iso);
    return d.toLocaleTimeString('en-US', {hour12: false, hour: '2-digit', minute: '2-digit'});
}

function ChartViewport({children}: { children: React.ReactElement<{ width?: number; height?: number }> }) {
    const hostRef = useRef<HTMLDivElement | null>(null);
    const [size, setSize] = useState({width: 0, height: 0});

    useEffect(() => {
        const host = hostRef.current;
        if (!host) return;

        const updateSize = () => {
            const {width, height} = host.getBoundingClientRect();
            const nextWidth = Math.max(0, Math.floor(width));
            const nextHeight = Math.max(0, Math.floor(height));
            setSize((prev) => (
                prev.width === nextWidth && prev.height === nextHeight
                    ? prev
                    : {width: nextWidth, height: nextHeight}
            ));
        };

        const frameId = window.requestAnimationFrame(updateSize);

        if (typeof ResizeObserver === 'undefined') {
            return () => window.cancelAnimationFrame(frameId);
        }

        const observer = new ResizeObserver(updateSize);
        observer.observe(host);
        return () => {
            window.cancelAnimationFrame(frameId);
            observer.disconnect();
        };
    }, []);

    const canRender = size.width > 0 && size.height > 0;

    return (
        <div ref={hostRef} className="h-full w-full">
            {canRender ? cloneElement(children, {width: size.width, height: size.height}) : null}
        </div>
    );
}

function GenAiCallsChart({filter}: { filter: TimeFilter }) {
    const {data, isLoading} = useAgentLlmCalls(filter);

    if (isLoading || !data?.buckets || !data?.legend) {
        return (
            <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64 animate-pulse">
                <div className="h-3 w-24 bg-brutal-zinc mb-4"/>
                <div className="space-y-2">
                    {Array.from({length: 5}).map((_, i) => (
                        <div key={i} className="bg-brutal-zinc/30"
                             style={{height: '12px', width: `${60 + Math.random() * 40}%`}}/>
                    ))}
                </div>
            </div>
        );
    }

    const names = data.legend.map(l => l.name);
    const chartData = data.buckets.map((b) => {
        const point: Record<string, string | number> = {time: b.time};
        for (const name of names) {
            point[name] = b.models[name] ?? 0;
        }
        return point;
    });

    return (
        <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64 flex flex-col">
            <div className="flex items-center gap-2 mb-3">
                <Zap className="w-3.5 h-3.5 text-signal-cyan"/>
                <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                    GenAI Call Volume
                </span>
            </div>
            <div className="flex-1 min-h-0">
                <ChartViewport>
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
                </ChartViewport>
            </div>
            <div className="flex flex-wrap gap-x-4 gap-y-1 mt-2">
                {data.legend.map((entry, i) => (
                    <div key={entry.name} className="flex items-center gap-1.5">
                        <div className="w-2 h-2" style={{backgroundColor: CHART_COLORS[i % CHART_COLORS.length]}}/>
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

function GenAiTokensChart({filter}: { filter: TimeFilter }) {
    const {data, isLoading} = useAgentTokens(filter);

    const chartData = useMemo(() => {
        if (!data?.buckets || !data?.legend) return [];

        return data.buckets.map((b) => {
            const total = Object.values(b.models).reduce((s, v) => s + v, 0);
            return {time: b.time, tokens: total};
        });
    }, [data]);

    if (isLoading || chartData.length === 0) {
        return (
            <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64 animate-pulse">
                <div className="h-3 w-24 bg-brutal-zinc mb-4"/>
                <div className="space-y-2">
                    {Array.from({length: 5}).map((_, i) => (
                        <div key={i} className="bg-brutal-zinc/30"
                             style={{height: '12px', width: `${60 + Math.random() * 40}%`}}/>
                    ))}
                </div>
            </div>
        );
    }

    const totalTokens = chartData.reduce((s, b) => s + b.tokens, 0);

    return (
        <div className="bg-brutal-carbon border-2 border-brutal-zinc p-4 h-64 flex flex-col">
            <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                    <Coins className="w-3.5 h-3.5 text-signal-cyan"/>
                    <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-brutal-slate">
                        Token Usage
                    </span>
                </div>
                <span className="text-[10px] font-mono font-bold text-signal-cyan">
                    {formatCompact(totalTokens)} total
                </span>
            </div>
            <div className="flex-1 min-h-0">
                <ChartViewport>
                    <AreaChart data={chartData} margin={{top: 4, right: 4, bottom: 0, left: 0}}>
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
                            tickFormatter={formatCompact}
                        />
                        <Tooltip
                            contentStyle={TOOLTIP_STYLE}
                            formatter={((value: number) => [formatCompact(value), 'Tokens']) as never}
                            labelFormatter={((label: string) => formatBucketTime(label)) as never}
                        />
                        <Area
                            type="monotone"
                            dataKey="tokens"
                            fill="var(--color-signal-cyan)"
                            fillOpacity={0.3}
                            stroke="var(--color-signal-cyan)"
                            strokeWidth={2}
                            name="tokens"
                        />
                    </AreaChart>
                </ChartViewport>
            </div>
        </div>
    );
}

function GenAISpanCard({
                           span,
                           isExpanded,
                           onToggle,
                       }: {
    span: GenAiSpan;
    isExpanded: boolean;
    onToggle: () => void;
}) {
    const totalTokens = (span.genAiInputTokens ?? 0) + (span.genAiOutputTokens ?? 0);
    const durationMs = nsToMs(span.durationNs);

    const providerColor =
        span.genAiProviderName === 'openai'
            ? 'text-signal-green border-signal-green'
            : span.genAiProviderName === 'anthropic'
                ? 'text-signal-orange border-signal-orange'
                : span.genAiProviderName === 'gcp.gemini'
                    ? 'text-signal-cyan border-signal-cyan'
                    : 'text-signal-violet border-signal-violet';

    let parsedAttrs: Record<string, unknown> = {};
    if (span.attributesJson) {
        try {
            parsedAttrs = JSON.parse(span.attributesJson);
        } catch {
            // Ignore parse errors
        }
    }

    const toolCallInfo = extractToolCallInfo(parsedAttrs);
    const showToolDefinitions = hasToolDefinitions(parsedAttrs);
    const hasToolData = toolCallInfo.hasToolCall || showToolDefinitions;

    return (
        <Card className={cn('transition-shadow', isExpanded && 'ring-1 ring-primary/50')}>
            <CardHeader className="cursor-pointer hover:bg-brutal-dark" role="button" tabIndex={0}
                        aria-expanded={isExpanded} onClick={onToggle} onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    onToggle();
                }
            }}>
                <div className="flex items-start gap-4">
                    {isExpanded ? (
                        <ChevronDown className="w-5 h-5 mt-0.5 text-brutal-slate"/>
                    ) : (
                        <ChevronRight className="w-5 h-5 mt-0.5 text-brutal-slate"/>
                    )}

                    <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                            <Badge variant="outline" className={providerColor}>
                                {span.genAiProviderName ?? 'unknown'}
                            </Badge>
                            <Badge variant="secondary">
                                {span.genAiResponseModel ?? span.genAiRequestModel ?? 'unknown'}
                            </Badge>
                            {hasToolData && (
                                <Badge variant="outline" className="text-signal-cyan border-signal-cyan">
                                    <Wrench className="w-3 h-3 mr-1"/>
                                    Tools
                                </Badge>
                            )}
                            <span className="text-sm text-brutal-slate">
                                {span.serviceName ?? 'unknown'}
                            </span>
                        </div>
                        <p className="text-sm text-brutal-slate mt-1 truncate">{span.name}</p>
                    </div>

                    <div className="flex items-center gap-6 text-sm">
                        <div className="text-right">
                            <div className="text-brutal-slate">Tokens</div>
                            <div className="font-mono">
                                {span.genAiInputTokens?.toLocaleString() ?? '-'} /{' '}
                                {span.genAiOutputTokens?.toLocaleString() ?? '-'}
                            </div>
                        </div>
                        <div className="text-right">
                            <div className="text-brutal-slate">Duration</div>
                            <div className="font-mono">{formatDuration(durationMs)}</div>
                        </div>
                        <div className="text-right">
                            <div className="text-brutal-slate">Cost</div>
                            <div className="font-mono text-signal-green">
                                ${span.genAiCostUsd?.toFixed(4) ?? '0.0000'}
                            </div>
                        </div>
                    </div>
                </div>
            </CardHeader>

            {isExpanded && (
                <CardContent className="pt-0">
                    <Separator className="mb-4"/>

                    <Tabs defaultValue="details" className="w-full">
                        <TabsList>
                            <TabsTrigger value="details">
                                <Cpu className="w-4 h-4 mr-1"/>
                                Details
                            </TabsTrigger>
                            {hasToolData && (
                                <TabsTrigger value="tools">
                                    <Wrench className="w-4 h-4 mr-1"/>
                                    Tools
                                </TabsTrigger>
                            )}
                            {Object.keys(parsedAttrs).length > 0 && (
                                <TabsTrigger value="attributes">
                                    <MessageSquare className="w-4 h-4 mr-1"/>
                                    Attributes
                                </TabsTrigger>
                            )}
                        </TabsList>

                        <TabsContent value="details" className="mt-4">
                            <div className="grid grid-cols-2 gap-4 text-sm">
                                <div>
                                    <span className="text-brutal-slate">Provider:</span>
                                    <span className="ml-2">{span.genAiProviderName ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Request Model:</span>
                                    <span className="ml-2">{span.genAiRequestModel ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Response Model:</span>
                                    <span className="ml-2">{span.genAiResponseModel ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Temperature:</span>
                                    <span className="ml-2">{span.genAiTemperature ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Input Tokens:</span>
                                    <span className="ml-2 font-mono">
                                        {span.genAiInputTokens?.toLocaleString() ?? '-'}
                                    </span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Output Tokens:</span>
                                    <span className="ml-2 font-mono">
                                        {span.genAiOutputTokens?.toLocaleString() ?? '-'}
                                    </span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Total Tokens:</span>
                                    <span className="ml-2 font-mono">{totalTokens.toLocaleString()}</span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Stop Reason:</span>
                                    <span className="ml-2">{span.genAiStopReason ?? '-'}</span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Duration:</span>
                                    <span className="ml-2 font-mono">{formatDuration(durationMs)}</span>
                                </div>
                                <div>
                                    <span className="text-brutal-slate">Cost:</span>
                                    <span className="ml-2 font-mono text-signal-green">
                                        ${span.genAiCostUsd?.toFixed(6) ?? '0.000000'}
                                    </span>
                                </div>
                                <div className="flex items-center">
                                    <span className="text-brutal-slate">Trace ID:</span>
                                    <CopyableText
                                        value={span.traceId}
                                        label="Trace ID"
                                        className="ml-2"
                                        textClassName="text-primary"
                                        truncate
                                        maxWidth="120px"
                                    />
                                </div>
                                <div className="flex items-center">
                                    <span className="text-brutal-slate">Span ID:</span>
                                    <CopyableText
                                        value={span.spanId}
                                        label="Span ID"
                                        className="ml-2"
                                        truncate
                                        maxWidth="120px"
                                    />
                                </div>
                            </div>
                        </TabsContent>

                        {hasToolData && (
                            <TabsContent value="tools" className="mt-4 space-y-6">
                                {/* Tool Definitions Section */}
                                {showToolDefinitions && (
                                    <ToolDefinitionsViewer attributes={parsedAttrs}/>
                                )}

                                {/* Tool Call Section */}
                                {toolCallInfo.hasToolCall && (
                                    <div className="space-y-2">
                                        <div className="flex items-center gap-2 mb-3">
                                            <FileJson className="w-4 h-4 text-brutal-slate"/>
                                            <span
                                                className="text-sm font-medium text-brutal-slate uppercase tracking-wide">
                                                Tool Call
                                            </span>
                                        </div>
                                        <ToolCallViewer
                                            toolName={toolCallInfo.toolName!}
                                            toolCallId={toolCallInfo.toolCallId}
                                            toolType={toolCallInfo.toolType}
                                            description={toolCallInfo.description}
                                            arguments={toolCallInfo.arguments}
                                            result={toolCallInfo.result}
                                        />
                                    </div>
                                )}
                            </TabsContent>
                        )}

                        {Object.keys(parsedAttrs).length > 0 && (
                            <TabsContent value="attributes" className="mt-4">
                                <TextVisualizer
                                    content={JSON.stringify(parsedAttrs)}
                                    label="Attributes"
                                    defaultExpanded={false}
                                    maxCollapsedHeight={160}
                                    showTreeView={true}
                                />
                            </TabsContent>
                        )}
                    </Tabs>
                </CardContent>
            )}
        </Card>
    );
}

export function GenAIPage() {
    const [expandedSpans, setExpandedSpans] = useState<Set<string>>(new Set());

    const timeFilter = useMemo<TimeFilter>(() => {
        const to = Date.now();
        return {from: to - 24 * 60 * 60 * 1000, to};
    }, []);

    const {
        data: stats,
        isLoading: statsLoading,
        error: statsError,
    } = useQuery({
        queryKey: ['genai-stats'],
        queryFn: fetchGenAiStats,
        staleTime: 30_000,
    });

    const {
        data: spansData,
        isLoading: spansLoading,
        error: spansError,
    } = useQuery({
        queryKey: ['genai-spans'],
        queryFn: () => fetchGenAiSpans(50),
        staleTime: 30_000,
    });

    const toggleSpan = (spanId: string) => {
        setExpandedSpans((prev) => {
            const next = new Set(prev);
            if (next.has(spanId)) {
                next.delete(spanId);
            } else {
                next.add(spanId);
            }
            return next;
        });
    };

    const isLoading = statsLoading || spansLoading;
    const error = statsError || spansError;

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-destructive"/>
                        <p className="text-destructive">Failed to load GenAI telemetry</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {error instanceof Error ? error.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    return (
        <div className="p-6 space-y-6">
            {/* Stats */}
            <div className="grid grid-cols-4 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Sparkles className="w-4 h-4 text-signal-violet"/>
                            <span className="text-sm text-brutal-slate">GenAI Calls</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1">
                                {stats?.requestCount.toLocaleString() ?? 0}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Cpu className="w-4 h-4 text-signal-cyan"/>
                            <span className="text-sm text-brutal-slate">Total Tokens</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1">
                                {((stats?.totalInputTokens ?? 0) + (stats?.totalOutputTokens ?? 0)).toLocaleString()}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <DollarSign className="w-4 h-4 text-signal-green"/>
                            <span className="text-sm text-brutal-slate">Total Cost</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1 text-signal-green">
                                ${stats?.totalCostUsd?.toFixed(4) ?? '0.0000'}
                            </div>
                        )}
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Activity className="w-4 h-4 text-signal-yellow"/>
                            <span className="text-sm text-brutal-slate">Input/Output</span>
                        </div>
                        {isLoading ? (
                            <Loader2 className="w-5 h-5 mt-2 animate-spin"/>
                        ) : (
                            <div className="text-2xl font-bold mt-1">
                                {(stats?.totalInputTokens ?? 0).toLocaleString()} /{' '}
                                {(stats?.totalOutputTokens ?? 0).toLocaleString()}
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Time-series charts */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                <GenAiCallsChart filter={timeFilter}/>
                <GenAiTokensChart filter={timeFilter}/>
            </div>

            {/* GenAI Spans */}
            <div className="space-y-4">
                <div className="flex items-center justify-between">
                    <h2 className="text-lg font-semibold">Recent GenAI Calls</h2>
                    <DownloadButton
                        getData={() => (spansData?.spans ?? []).map(span => ({
                            spanId: span.spanId,
                            traceId: span.traceId,
                            name: span.name,
                            provider: span.genAiProviderName ?? '',
                            requestModel: span.genAiRequestModel ?? '',
                            responseModel: span.genAiResponseModel ?? '',
                            inputTokens: span.genAiInputTokens ?? 0,
                            outputTokens: span.genAiOutputTokens ?? 0,
                            totalTokens: (span.genAiInputTokens ?? 0) + (span.genAiOutputTokens ?? 0),
                            temperature: span.genAiTemperature ?? '',
                            stopReason: span.genAiStopReason ?? '',
                            toolName: span.genAiToolName ?? '',
                            toolCallId: span.genAiToolCallId ?? '',
                            costUsd: span.genAiCostUsd ?? 0,
                            durationMs: nsToMs(span.durationNs),
                            serviceName: span.serviceName ?? '',
                            statusCode: span.statusCode,
                            statusMessage: span.statusMessage ?? '',
                        }))}
                        filenamePrefix="genai-spans"
                        columns={[
                            'spanId', 'traceId', 'name', 'provider', 'requestModel', 'responseModel',
                            'inputTokens', 'outputTokens', 'totalTokens', 'temperature', 'stopReason',
                            'toolName', 'toolCallId', 'costUsd', 'durationMs', 'serviceName',
                            'statusCode', 'statusMessage'
                        ]}
                        disabled={!spansData?.spans?.length}
                    />
                </div>

                {isLoading ? (
                    <Card>
                        <CardContent className="py-12 text-center">
                            <Loader2 className="w-12 h-12 mx-auto mb-4 animate-spin text-brutal-slate"/>
                            <p className="text-brutal-slate">Loading GenAI telemetry...</p>
                        </CardContent>
                    </Card>
                ) : !spansData?.spans?.length ? (
                    <Card>
                        <CardContent className="py-12 text-center text-brutal-slate">
                            <Sparkles className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                            <p>No GenAI telemetry found</p>
                            <p className="text-sm">GenAI spans will appear as your AI calls are traced</p>
                        </CardContent>
                    </Card>
                ) : (
                    <div className="space-y-4">
                        {spansData.spans.map((span) => (
                            <GenAISpanCard
                                key={span.spanId}
                                span={span}
                                isExpanded={expandedSpans.has(span.spanId)}
                                onToggle={() => toggleSpan(span.spanId)}
                            />
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
