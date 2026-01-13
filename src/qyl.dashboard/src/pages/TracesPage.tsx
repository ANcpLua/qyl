import {useCallback, useMemo, useRef, useState} from 'react';
import {useSearchParams} from 'react-router-dom';
import {useVirtualizer} from '@tanstack/react-virtual';
import {AlertCircle, ChevronDown, ChevronRight, Filter, Network, X,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {ScrollArea} from '@/components/ui/scroll-area';
import {Separator} from '@/components/ui/separator';
import {
    formatDuration,
    formatTimestamp,
    getSpanColor,
    getSpanTypeLabel,
    useSessions,
    useSessionSpans,
} from '@/hooks/use-telemetry';
import type {Span} from '@/types';

interface FlattenedSpan {
    span: Span;
    depth: number;
    hasChildren: boolean;
    isExpanded: boolean;
}

interface SpanRowProps {
    span: Span;
    depth: number;
    isExpanded: boolean;
    onToggle: () => void;
    hasChildren: boolean;
    timelineStart: number;
    timelineEnd: number;
    isSelected: boolean;
    onSelect: () => void;
}

function SpanRow({
                     span,
                     depth,
                     isExpanded,
                     onToggle,
                     hasChildren,
                     timelineStart,
                     timelineEnd,
                     isSelected,
                     onSelect,
                 }: SpanRowProps) {
    const totalDuration = timelineEnd - timelineStart;
    const spanStart = new Date(span.startTime).getTime();
    const spanEnd = new Date(span.endTime).getTime();

    const leftPercent = totalDuration > 0 ? ((spanStart - timelineStart) / totalDuration) * 100 : 0;
    const widthPercent = Math.max(0.5, totalDuration > 0 ? ((spanEnd - spanStart) / totalDuration) * 100 : 1);

    const isError = span.status === 'error';
    const color = getSpanColor(span);
    const typeLabel = getSpanTypeLabel(span);

    const handleClick = (e: React.MouseEvent) => {
        e.stopPropagation();
        onSelect();
    };

    const handleToggle = (e: React.MouseEvent) => {
        e.stopPropagation();
        onToggle();
    };

    return (
        <div
            className={cn(
                'flex items-center gap-2 px-4 py-2 hover:bg-muted/50 border-b border-border cursor-pointer',
                isError && 'bg-red-500/5',
                isSelected && 'bg-primary/10'
            )}
            onClick={handleClick}
        >
            {/* Expand/collapse and indentation */}
            <div style={{paddingLeft: depth * 16}} className="flex items-center">
                {hasChildren ? (
                    <button onClick={handleToggle} className="p-0.5 hover:bg-muted rounded">
                        {isExpanded ? (
                            <ChevronDown className="w-4 h-4 text-muted-foreground"/>
                        ) : (
                            <ChevronRight className="w-4 h-4 text-muted-foreground"/>
                        )}
                    </button>
                ) : (
                    <div className="w-5"/>
                )}
            </div>

            {/* Span info */}
            <div className="flex-shrink-0 w-32">
                <Badge
                    variant="outline"
                    style={{borderColor: color, color}}
                    className="text-xs"
                >
                    {typeLabel}
                </Badge>
            </div>

            <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
          <span className={cn('truncate font-mono text-sm', isError && 'text-red-500')}>
            {span.name}
          </span>
                    {isError && <AlertCircle className="w-4 h-4 text-red-500 flex-shrink-0"/>}
                </div>
                <div className="text-xs text-muted-foreground truncate">
                    {span.serviceName}
                </div>
            </div>

            {/* Waterfall visualization */}
            <div className="w-80 h-6 relative bg-muted/30 rounded overflow-hidden">
                <div
                    className="waterfall-bar absolute top-0.5"
                    style={{
                        left: `${leftPercent}%`,
                        width: `${widthPercent}%`,
                        backgroundColor: isError ? 'var(--color-error)' : color,
                    }}
                />
            </div>

            {/* Duration */}
            <div className="w-20 text-right font-mono text-sm text-muted-foreground">
                {formatDuration(span.durationMs)}
            </div>
        </div>
    );
}

function SpanDetails({span}: { span: Span }) {
    return (
        <div className="p-4 space-y-4">
            {/* Header */}
            <div>
                <div className="flex items-center gap-2">
                    <Badge
                        variant="outline"
                        style={{borderColor: getSpanColor(span), color: getSpanColor(span)}}
                    >
                        {getSpanTypeLabel(span)}
                    </Badge>
                    <Badge variant={span.status === 'error' ? 'destructive' : 'secondary'}>
                        {span.status}
                    </Badge>
                </div>
                <h3 className="text-lg font-semibold mt-2 font-mono">{span.name}</h3>
                <p className="text-sm text-muted-foreground">{span.serviceName}</p>
            </div>

            <Separator/>

            {/* Timing */}
            <div>
                <h4 className="text-sm font-medium mb-2">Timing</h4>
                <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                        <span className="text-muted-foreground">Start:</span>
                        <span className="ml-2 font-mono">{formatTimestamp(span.startTime)}</span>
                    </div>
                    <div>
                        <span className="text-muted-foreground">End:</span>
                        <span className="ml-2 font-mono">{formatTimestamp(span.endTime)}</span>
                    </div>
                    <div>
                        <span className="text-muted-foreground">Duration:</span>
                        <span className="ml-2 font-mono">{formatDuration(span.durationMs)}</span>
                    </div>
                </div>
            </div>

            <Separator/>

            {/* IDs */}
            <div>
                <h4 className="text-sm font-medium mb-2">IDs</h4>
                <div className="space-y-1 text-sm font-mono">
                    <div>
                        <span className="text-muted-foreground">Trace:</span>
                        <span className="ml-2 text-primary">{span.traceId}</span>
                    </div>
                    <div>
                        <span className="text-muted-foreground">Span:</span>
                        <span className="ml-2">{span.spanId}</span>
                    </div>
                    {span.parentSpanId && (
                        <div>
                            <span className="text-muted-foreground">Parent:</span>
                            <span className="ml-2">{span.parentSpanId}</span>
                        </div>
                    )}
                </div>
            </div>

            <Separator/>

            {/* Attributes */}
            <div>
                <h4 className="text-sm font-medium mb-2">Attributes</h4>
                <div className="space-y-1 text-sm">
                    {Object.entries(span.attributes ?? {}).map(([key, value]) => (
                        <div key={key} className="flex">
                            <span className="text-muted-foreground min-w-32">{key}:</span>
                            <span className="font-mono break-all">{String(value)}</span>
                        </div>
                    ))}
                    {Object.keys(span.attributes ?? {}).length === 0 && (
                        <p className="text-muted-foreground">No attributes</p>
                    )}
                </div>
            </div>

            {/* GenAI specific */}
            {span.genai && (
                <>
                    <Separator/>
                    <div>
                        <h4 className="text-sm font-medium mb-2">GenAI Details</h4>
                        <div className="grid grid-cols-2 gap-4 text-sm">
                            <div>
                                <span className="text-muted-foreground">Provider:</span>
                                <span className="ml-2">{span.genai.providerName}</span>
                            </div>
                            <div>
                                <span className="text-muted-foreground">Model:</span>
                                <span className="ml-2">{span.genai.requestModel}</span>
                            </div>
                            <div>
                                <span className="text-muted-foreground">Tokens In:</span>
                                <span className="ml-2 font-mono">{span.genai.inputTokens?.toLocaleString()}</span>
                            </div>
                            <div>
                                <span className="text-muted-foreground">Tokens Out:</span>
                                <span className="ml-2 font-mono">{span.genai.outputTokens?.toLocaleString()}</span>
                            </div>
                            {span.genai.costUsd && (
                                <div>
                                    <span className="text-muted-foreground">Cost:</span>
                                    <span className="ml-2 font-mono text-green-500">
                    ${span.genai.costUsd.toFixed(6)}
                  </span>
                                </div>
                            )}
                        </div>
                    </div>
                </>
            )}

            {/* Events */}
            {(span.events?.length ?? 0) > 0 && (
                <>
                    <Separator/>
                    <div>
                        <h4 className="text-sm font-medium mb-2">Events ({span.events?.length ?? 0})</h4>
                        <div className="space-y-2">
                            {(span.events ?? []).map((event, i) => (
                                <div key={i} className="text-sm p-2 bg-muted rounded">
                                    <div className="flex items-center gap-2">
                                        <span className="font-medium">{event.name}</span>
                                        <span className="text-xs text-muted-foreground">
                      {formatTimestamp(event.timestamp)}
                    </span>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </>
            )}
        </div>
    );
}

export function TracesPage() {
    const [searchParams] = useSearchParams();
    const sessionId = searchParams.get('session') || '';

    const [selectedSpan, setSelectedSpan] = useState<Span | null>(null);
    const [expandedSpans, setExpandedSpans] = useState<Set<string>>(new Set());
    const [filterText, setFilterText] = useState('');

    const parentRef = useRef<HTMLDivElement>(null);

    const {data: sessions = []} = useSessions();
    const {data: spans = [], isLoading} = useSessionSpans(
        sessionId || sessions[0]?.sessionId || ''
    );

    // Build span tree and compute timeline bounds
    const {childrenMap, timelineStart, timelineEnd} = useMemo(() => {
        const childrenMap = new Map<string, Span[]>();
        let minTime = Infinity;
        let maxTime = -Infinity;

        for (const span of spans) {
            const startTime = new Date(span.startTime).getTime();
            const endTime = new Date(span.endTime).getTime();
            minTime = Math.min(minTime, startTime);
            maxTime = Math.max(maxTime, endTime);

            if (span.parentSpanId) {
                const siblings = childrenMap.get(span.parentSpanId) || [];
                siblings.push(span);
                childrenMap.set(span.parentSpanId, siblings);
            }
        }

        // Sort children by start time
        for (const siblings of childrenMap.values()) {
            siblings.sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
        }

        return {
            childrenMap,
            timelineStart: minTime === Infinity ? 0 : minTime,
            timelineEnd: maxTime === -Infinity ? 1 : maxTime,
        };
    }, [spans]);

    // Flatten visible tree for virtualization
    const flattenedSpans = useMemo(() => {
        const result: FlattenedSpan[] = [];
        const filterLower = filterText.toLowerCase();

        // Get root spans (no parent)
        const rootSpans = spans
            .filter(s => !s.parentSpanId)
            .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());

        const matchesFilter = (span: Span): boolean => {
            if (!filterText) return true;
            return (
                span.name.toLowerCase().includes(filterLower) ||
                span.serviceName.toLowerCase().includes(filterLower) ||
                Object.values(span.attributes ?? {}).some(v =>
                    String(v).toLowerCase().includes(filterLower)
                )
            );
        };

        const flatten = (span: Span, depth: number) => {
            const children = childrenMap.get(span.spanId) || [];
            const hasChildren = children.length > 0;
            const isExpanded = expandedSpans.has(span.spanId);

            // Include if matches filter or has matching descendants
            const matches = matchesFilter(span);
            const hasMatchingDescendant = hasChildren && children.some(c => {
                const stack = [c];
                while (stack.length > 0) {
                    const current = stack.pop()!;
                    if (matchesFilter(current)) return true;
                    const grandchildren = childrenMap.get(current.spanId) || [];
                    stack.push(...grandchildren);
                }
                return false;
            });

            if (!matches && !hasMatchingDescendant) return;

            result.push({span, depth, hasChildren, isExpanded});

            if (isExpanded && hasChildren) {
                for (const child of children) {
                    flatten(child, depth + 1);
                }
            }
        };

        for (const root of rootSpans) {
            flatten(root, 0);
        }

        return result;
    }, [spans, childrenMap, expandedSpans, filterText]);

    const toggleSpan = useCallback((spanId: string) => {
        setExpandedSpans(prev => {
            const next = new Set(prev);
            if (next.has(spanId)) {
                next.delete(spanId);
            } else {
                next.add(spanId);
            }
            return next;
        });
    }, []);

    // Virtualization
    const rowVirtualizer = useVirtualizer({
        count: flattenedSpans.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 52, // Fixed row height
        overscan: 10,
    });

    return (
        <div className="flex h-full">
            {/* Main content */}
            <div className="flex-1 flex flex-col min-w-0">
                {/* Toolbar */}
                <div className="flex items-center gap-4 p-4 border-b border-border">
                    <div className="relative flex-1 max-w-sm">
                        <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground"/>
                        <Input
                            placeholder="Filter spans..."
                            value={filterText}
                            onChange={(e) => setFilterText(e.target.value)}
                            className="pl-9"
                        />
                    </div>

                    <div className="text-sm text-muted-foreground">
                        {flattenedSpans.length} / {spans.length} spans
                    </div>

                    <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setExpandedSpans(new Set(spans.map((s) => s.spanId)))}
                    >
                        Expand All
                    </Button>

                    <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setExpandedSpans(new Set())}
                    >
                        Collapse All
                    </Button>
                </div>

                {/* Virtualized trace waterfall */}
                <div
                    ref={parentRef}
                    className="flex-1 overflow-auto"
                    style={{contain: 'strict'}}
                >
                    {isLoading ? (
                        <div className="flex items-center justify-center py-12">
                            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"/>
                        </div>
                    ) : spans.length === 0 ? (
                        <div className="py-12 text-center text-muted-foreground">
                            <Network className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                            <p>No traces found</p>
                            <p className="text-sm">Select a session or wait for telemetry data</p>
                        </div>
                    ) : (
                        <div
                            style={{
                                height: `${rowVirtualizer.getTotalSize()}px`,
                                width: '100%',
                                position: 'relative',
                            }}
                        >
                            {rowVirtualizer.getVirtualItems().map((virtualRow) => {
                                const {span, depth, hasChildren, isExpanded} = flattenedSpans[virtualRow.index];
                                return (
                                    <div
                                        key={virtualRow.key}
                                        style={{
                                            position: 'absolute',
                                            top: 0,
                                            left: 0,
                                            width: '100%',
                                            height: `${virtualRow.size}px`,
                                            transform: `translateY(${virtualRow.start}px)`,
                                        }}
                                    >
                                        <SpanRow
                                            span={span}
                                            depth={depth}
                                            isExpanded={isExpanded}
                                            onToggle={() => toggleSpan(span.spanId)}
                                            hasChildren={hasChildren}
                                            timelineStart={timelineStart}
                                            timelineEnd={timelineEnd}
                                            isSelected={selectedSpan?.spanId === span.spanId}
                                            onSelect={() => setSelectedSpan(span)}
                                        />
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </div>
            </div>

            {/* Details panel */}
            {selectedSpan && (
                <div className="w-96 border-l border-border bg-card">
                    <div className="flex items-center justify-between px-4 py-2 border-b border-border">
                        <span className="font-medium">Span Details</span>
                        <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setSelectedSpan(null)}
                        >
                            <X className="w-4 h-4"/>
                        </Button>
                    </div>
                    <ScrollArea className="h-[calc(100vh-12rem)]">
                        <SpanDetails span={selectedSpan}/>
                    </ScrollArea>
                </div>
            )}
        </div>
    );
}
