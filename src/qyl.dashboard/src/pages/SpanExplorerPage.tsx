// TODO: Backend span search endpoint needed — currently client-side filtering only

import {useMemo, useState} from 'react';
import {useNavigate} from 'react-router-dom';
import {AlertCircle, ChevronRight, Database, Filter, Search} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import type {Span} from '@/types';
import {nsToMs, nanoToIso} from '@/types';
import {
    formatDuration,
    formatTimestamp,
    getSpanColor,
    getSpanTypeLabel,
    STATUS_ERROR,
} from '@/hooks/use-telemetry';
import {useRecentSpans} from '@/hooks/use-spans';

function getServiceName(span: Span): string {
    const attr = span.resource?.attributes?.find(a => a.key === 'service.name');
    return (attr?.value as string) ?? 'unknown';
}

function getDurationMs(span: Span): number {
    return nsToMs(span.end_time_unix_nano - span.start_time_unix_nano);
}

const statusCodeLabels: Record<number, string> = {
    0: 'Unset',
    1: 'OK',
    2: 'Error',
};

function StatusBadge({code}: { code: number }) {
    const style = code === 2
        ? 'bg-red-500/20 text-red-400 border-red-500/40'
        : code === 1
            ? 'bg-green-500/20 text-green-400 border-green-500/40'
            : 'bg-zinc-500/20 text-zinc-400 border-zinc-500/40';

    return (
        <Badge variant="outline" className={cn('text-[10px] uppercase tracking-wider', style)}>
            {statusCodeLabels[code] ?? 'Unknown'}
        </Badge>
    );
}

function TypeBadge({span}: { span: Span }) {
    const color = getSpanColor(span);
    const label = getSpanTypeLabel(span);
    return (
        <Badge variant="outline" style={{borderColor: color, color}} className="text-[10px] uppercase tracking-wider">
            {label}
        </Badge>
    );
}

function SkeletonRow() {
    return (
        <div className="flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc animate-pulse">
            <div className="flex-1 h-4 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
            <div className="w-20 h-5 bg-brutal-zinc rounded"/>
            <div className="w-20 h-4 bg-brutal-zinc rounded"/>
            <div className="w-20 h-5 bg-brutal-zinc rounded"/>
            <div className="w-28 h-4 bg-brutal-zinc rounded"/>
        </div>
    );
}

function SpanRow({span, onClick}: { span: Span; onClick: () => void }) {
    const isError = span.status.code === STATUS_ERROR;

    return (
        <div
            className={cn(
                'flex items-center gap-4 px-4 py-3 border-b border-brutal-zinc hover:bg-brutal-dark/50 cursor-pointer transition-colors group',
                isError && 'bg-red-500/5',
            )}
            role="button"
            tabIndex={0}
            onClick={onClick}
            onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    onClick();
                }
            }}
        >
            <div className="flex-1 min-w-0">
                <span className={cn('text-sm font-bold truncate block font-mono', isError && 'text-red-400')}>
                    {span.name}
                </span>
            </div>

            <div className="w-28 min-w-0">
                <span className="text-xs text-brutal-slate truncate block">
                    {getServiceName(span)}
                </span>
            </div>

            <div className="w-20">
                <TypeBadge span={span}/>
            </div>

            <div className="w-20 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatDuration(getDurationMs(span))}
                </span>
            </div>

            <div className="w-20">
                <StatusBadge code={span.status.code}/>
            </div>

            <div className="w-28 text-right">
                <span className="font-mono text-xs text-brutal-slate">
                    {formatTimestamp(nanoToIso(span.start_time_unix_nano))}
                </span>
            </div>

            <ChevronRight
                className="w-4 h-4 text-brutal-zinc group-hover:text-brutal-slate transition-colors flex-shrink-0"/>
        </div>
    );
}

export function SpanExplorerPage() {
    const navigate = useNavigate();
    const [serviceFilter, setServiceFilter] = useState('');
    const [spanNameFilter, setSpanNameFilter] = useState('');
    const [statusFilter, setStatusFilter] = useState<string>('');
    const [minDuration, setMinDuration] = useState('');
    const [maxDuration, setMaxDuration] = useState('');

    const {data, isLoading, error} = useRecentSpans(200);

    const spans = data?.spans ?? [];
    const bufferCount = data?.bufferCount ?? 0;
    const bufferCapacity = data?.bufferCapacity ?? 0;

    // Client-side filtering
    const filteredSpans = useMemo(() => {
        const serviceFilterLower = serviceFilter.toLowerCase();
        const spanNameFilterLower = spanNameFilter.toLowerCase();
        const minMs = minDuration ? parseFloat(minDuration) : undefined;
        const maxMs = maxDuration ? parseFloat(maxDuration) : undefined;
        const statusCode = statusFilter && statusFilter !== 'all'
            ? parseInt(statusFilter, 10)
            : undefined;

        return spans.filter((span) => {
            if (serviceFilterLower && !getServiceName(span).toLowerCase().includes(serviceFilterLower)) {
                return false;
            }
            if (spanNameFilterLower && !span.name.toLowerCase().includes(spanNameFilterLower)) {
                return false;
            }
            if (statusCode !== undefined && span.status.code !== statusCode) {
                return false;
            }
            const durationMs = getDurationMs(span);
            if (minMs !== undefined && durationMs < minMs) {
                return false;
            }
            if (maxMs !== undefined && durationMs > maxMs) {
                return false;
            }
            return true;
        });
    }, [spans, serviceFilter, spanNameFilter, statusFilter, minDuration, maxDuration]);

    if (error) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500"/>
                        <p className="text-red-400">Failed to load spans</p>
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
            {/* Buffer stats */}
            <div className="flex items-center gap-3">
                <Database className="w-4 h-4 text-brutal-slate"/>
                <span className="text-xs font-bold text-brutal-slate tracking-wider">
                    RING BUFFER: {bufferCount} / {bufferCapacity}
                </span>
                {bufferCapacity > 0 && (
                    <div className="w-24 h-1.5 bg-brutal-zinc rounded-full overflow-hidden">
                        <div
                            className="h-full bg-signal-orange rounded-full transition-all"
                            style={{width: `${Math.min(100, (bufferCount / bufferCapacity) * 100)}%`}}
                        />
                    </div>
                )}
            </div>

            {/* Filters */}
            <div className="flex items-center gap-4 flex-wrap">
                <div className="relative flex-1 min-w-48 max-w-xs">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate"/>
                    <Input
                        placeholder="Span name..."
                        value={spanNameFilter}
                        onChange={(e) => setSpanNameFilter(e.target.value)}
                        className="pl-9"
                        aria-label="Filter by span name"
                    />
                </div>

                <div className="relative flex-1 min-w-48 max-w-xs">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate"/>
                    <Input
                        placeholder="Service name..."
                        value={serviceFilter}
                        onChange={(e) => setServiceFilter(e.target.value)}
                        className="pl-9"
                        aria-label="Filter by service name"
                    />
                </div>

                <Select value={statusFilter} onValueChange={setStatusFilter}>
                    <SelectTrigger className="w-32" aria-label="Filter by status">
                        <SelectValue placeholder="All statuses"/>
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="all">All</SelectItem>
                        <SelectItem value="1">OK</SelectItem>
                        <SelectItem value="2">Error</SelectItem>
                        <SelectItem value="0">Unset</SelectItem>
                    </SelectContent>
                </Select>

                <Input
                    type="number"
                    placeholder="Min ms"
                    value={minDuration}
                    onChange={(e) => setMinDuration(e.target.value)}
                    className="w-24"
                    aria-label="Minimum duration in milliseconds"
                />

                <Input
                    type="number"
                    placeholder="Max ms"
                    value={maxDuration}
                    onChange={(e) => setMaxDuration(e.target.value)}
                    className="w-24"
                    aria-label="Maximum duration in milliseconds"
                />

                <div className="text-xs font-bold text-brutal-slate tracking-wider">
                    {filteredSpans.length} / {spans.length} SPANS
                </div>
            </div>

            {/* Table */}
            <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                {/* Header */}
                <div
                    className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                    <div className="flex-1">SPAN NAME</div>
                    <div className="w-28">SERVICE</div>
                    <div className="w-20">TYPE</div>
                    <div className="w-20 text-right">DURATION</div>
                    <div className="w-20">STATUS</div>
                    <div className="w-28 text-right">TIMESTAMP</div>
                    <div className="w-4"/>
                </div>

                {/* Body */}
                {isLoading ? (
                    <>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                        <SkeletonRow/>
                    </>
                ) : filteredSpans.length === 0 ? (
                    <div className="py-12 text-center">
                        <Search className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                        <p className="text-brutal-slate text-sm">
                            {spans.length === 0 ? 'No spans in buffer' : 'No spans match your filters'}
                        </p>
                        <p className="text-brutal-zinc text-xs mt-1">
                            {spans.length === 0
                                ? 'Spans will appear as telemetry is ingested'
                                : 'Try adjusting your filter criteria'}
                        </p>
                    </div>
                ) : (
                    filteredSpans.map((span) => (
                        <SpanRow
                            key={`${span.trace_id}-${span.span_id}`}
                            span={span}
                            onClick={() => navigate(`/traces?traceId=${span.trace_id}`)}
                        />
                    ))
                )}
            </div>
        </div>
    );
}
