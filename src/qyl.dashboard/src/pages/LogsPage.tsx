import {memo, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState} from 'react';
import {useVirtualizer} from '@tanstack/react-virtual';
import {
    AlertCircle,
    AlertTriangle,
    ArrowDown,
    Bug,
    ChevronDown,
    ChevronRight,
    FileText,
    Filter,
    Info,
    Skull,
    X,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue,} from '@/components/ui/select';
import {CopyableText} from '@/components/ui';
import {formatTimestamp} from '@/hooks/use-telemetry';
import {RingBuffer} from '@/lib/RingBuffer';
import type {LogLevel, LogRecord} from '@/types';

// =============================================================================
// Configuration
// =============================================================================

const MAX_LOGS = 10_000;
const AUTO_SCROLL_THRESHOLD = 100; // px from bottom to consider "attached"
const SSE_RECONNECT_DELAY = 3000;

const LOG_LEVEL_CONFIG: Record<
    LogLevel,
    { icon: typeof Info; className: string; color: string }
> = {
    trace: {icon: Bug, className: 'log-level-trace', color: 'text-gray-400'},
    debug: {icon: Bug, className: 'log-level-debug', color: 'text-blue-400'},
    info: {icon: Info, className: 'log-level-info', color: 'text-cyan-400'},
    warn: {icon: AlertTriangle, className: 'log-level-warn', color: 'text-yellow-400'},
    error: {icon: AlertCircle, className: 'log-level-error', color: 'text-red-400'},
    fatal: {icon: Skull, className: 'log-level-fatal', color: 'text-red-300'},
};

const LOG_LEVELS: LogLevel[] = ['trace', 'debug', 'info', 'warn', 'error', 'fatal'];

// =============================================================================
// LogRow Component (memoized for virtualization performance)
// =============================================================================

interface LogRowProps {
    log: LogRecord;
    isExpanded: boolean;
    onToggle: () => void;
}

const LogRow = memo(function LogRow({log, isExpanded, onToggle}: LogRowProps) {
    const config = LOG_LEVEL_CONFIG[log.severityText];
    const Icon = config.icon;

    return (
        <div className="border-b border-border">
            <div
                className={cn(
                    'flex items-start gap-3 px-4 py-2 hover:bg-muted/50 cursor-pointer',
                    log.severityText === 'error' && 'bg-red-500/5',
                    log.severityText === 'fatal' && 'bg-red-500/10',
                    log.severityText === 'warn' && 'bg-yellow-500/5'
                )}
                onClick={onToggle}
            >
                {/* Expand icon */}
                <div className="pt-0.5">
                    {isExpanded ? (
                        <ChevronDown className="w-4 h-4 text-muted-foreground"/>
                    ) : (
                        <ChevronRight className="w-4 h-4 text-muted-foreground"/>
                    )}
                </div>

                {/* Timestamp */}
                <span className="font-mono text-xs text-muted-foreground w-24 flex-shrink-0">
          {formatTimestamp(log.timestamp)}
        </span>

                {/* Level */}
                <Badge variant="outline" className={cn('text-xs', config.className)}>
                    <Icon className="w-3 h-3 mr-1"/>
                    {log.severityText.toUpperCase()}
                </Badge>

                {/* Service */}
                <span className="text-sm text-muted-foreground w-28 truncate flex-shrink-0">
          {log.serviceName}
        </span>

                {/* Message */}
                <span className="text-sm flex-1 truncate font-mono">{log.body}</span>
            </div>

            {/* Expanded details */}
            {isExpanded && (
                <div className="px-4 py-3 bg-muted/30 border-t border-border">
                    <div className="pl-8 space-y-3">
                        {/* Full message */}
                        <div>
                            <h4 className="text-xs font-medium text-muted-foreground mb-1">Message</h4>
                            <pre className="text-sm font-mono whitespace-pre-wrap bg-background p-2 rounded">
                {log.body}
              </pre>
                        </div>

                        {/* Trace context */}
                        {log.traceId && (
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <h4 className="text-xs font-medium text-muted-foreground mb-1">Trace ID</h4>
                                    <CopyableText
                                        value={log.traceId}
                                        label="Trace ID"
                                        textClassName="text-primary"
                                        truncate
                                        maxWidth="180px"
                                    />
                                </div>
                                {log.spanId && (
                                    <div>
                                        <h4 className="text-xs font-medium text-muted-foreground mb-1">Span ID</h4>
                                        <CopyableText
                                            value={log.spanId}
                                            label="Span ID"
                                            truncate
                                            maxWidth="180px"
                                        />
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Attributes */}
                        {Object.keys(log.attributes).length > 0 && (
                            <div>
                                <h4 className="text-xs font-medium text-muted-foreground mb-1">Attributes</h4>
                                <div className="grid grid-cols-2 gap-2">
                                    {Object.entries(log.attributes).map(([key, value]) => (
                                        <div key={key} className="flex items-center text-sm">
                                            <span className="text-muted-foreground">{key}:</span>
                                            <CopyableText
                                                value={String(value)}
                                                label={key}
                                                className="ml-2"
                                                truncate
                                                maxWidth="150px"
                                            />
                                        </div>
                                    ))}
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
});

// =============================================================================
// useLiveLogs Hook - SSE connection with RAF batching
// =============================================================================

interface UseLiveLogsOptions {
    enabled?: boolean;
    onError?: (error: Error) => void;
}

function useLiveLogs(
    bufferRef: React.MutableRefObject<RingBuffer<LogRecord>>,
    setVersion: React.Dispatch<React.SetStateAction<number>>,
    options: UseLiveLogsOptions = {}
) {
    const {enabled = true, onError} = options;

    const pendingLogsRef = useRef<LogRecord[]>([]);
    const rafIdRef = useRef<number | null>(null);
    const reconnectTimeoutRef = useRef<number | null>(null);
    const [isConnected, setIsConnected] = useState(false);

    // RAF-batched flush - coalesces all logs received in a frame into single state update
    const flushPending = useCallback(() => {
        const pending = pendingLogsRef.current;
        pendingLogsRef.current = [];
        rafIdRef.current = null;

        if (pending.length > 0) {
            bufferRef.current.pushMany(pending);
            setVersion((v) => v + 1);
        }
    }, [bufferRef, setVersion]);

    // Queue logs for RAF batch
    const queueLogs = useCallback(
        (logs: LogRecord[]) => {
            pendingLogsRef.current.push(...logs);

            if (rafIdRef.current === null) {
                rafIdRef.current = requestAnimationFrame(flushPending);
            }
        },
        [flushPending]
    );

    useEffect(() => {
        if (!enabled) return;

        let eventSource: EventSource | null = null;

        const connect = () => {
            eventSource = new EventSource('/api/v1/logs/live');

            eventSource.addEventListener('connected', () => {
                setIsConnected(true);
            });

            eventSource.addEventListener('logs', (e) => {
                try {
                    const data = JSON.parse(e.data);
                    // Handle both single log and batch formats
                    const logs: LogRecord[] = Array.isArray(data.logs)
                        ? data.logs
                        : Array.isArray(data)
                            ? data
                            : [data];
                    queueLogs(logs);
                } catch (err) {
                    console.error('Failed to parse log event:', err);
                }
            });

            // Also handle generic message events (fallback)
            eventSource.onmessage = (e) => {
                try {
                    const data = JSON.parse(e.data);
                    if (data.logs || data.body) {
                        const logs: LogRecord[] = data.logs ? data.logs : [data];
                        queueLogs(logs);
                    }
                } catch {
                    // Ignore parse errors for non-log messages
                }
            };

            eventSource.onerror = () => {
                setIsConnected(false);
                eventSource?.close();
                onError?.(new Error('SSE connection lost'));

                // Reconnect with backoff
                reconnectTimeoutRef.current = window.setTimeout(() => {
                    connect();
                }, SSE_RECONNECT_DELAY);
            };
        };

        connect();

        return () => {
            if (reconnectTimeoutRef.current) {
                clearTimeout(reconnectTimeoutRef.current);
            }
            if (rafIdRef.current) {
                cancelAnimationFrame(rafIdRef.current);
            }
            eventSource?.close();
            setIsConnected(false);
        };
    }, [enabled, queueLogs, onError]);

    return {isConnected};
}

// =============================================================================
// LogsPage Component
// =============================================================================

export function LogsPage() {
    // -------------------------------------------------------------------------
    // Ring buffer state - O(1) append with automatic pruning
    // -------------------------------------------------------------------------
    const logsBufferRef = useRef(new RingBuffer<LogRecord>(MAX_LOGS));
    const [logsVersion, setLogsVersion] = useState(0);
    const lastGenerationRef = useRef(0);

    // -------------------------------------------------------------------------
    // UI state
    // -------------------------------------------------------------------------
    // Use composite keys (traceId + timestamp) instead of indices - survives buffer wrap
    const [expandedLogs, setExpandedLogs] = useState<Set<string>>(new Set());
    const [filterText, setFilterText] = useState('');
    const [minLevel, setMinLevel] = useState<LogLevel>('trace');
    const [selectedService, setSelectedService] = useState<string>('all');
    const [isLive, setIsLive] = useState(true);

    // -------------------------------------------------------------------------
    // Auto-scroll (tail-f) state
    // -------------------------------------------------------------------------
    const [isAutoScroll, setIsAutoScroll] = useState(true);
    const [newLogsCount, setNewLogsCount] = useState(0);
    const isAutoScrollRef = useRef(isAutoScroll);
    isAutoScrollRef.current = isAutoScroll;

    // -------------------------------------------------------------------------
    // Refs
    // -------------------------------------------------------------------------
    const parentRef = useRef<HTMLDivElement>(null);
    const expandedLogsRef = useRef(expandedLogs);
    expandedLogsRef.current = expandedLogs;

    // -------------------------------------------------------------------------
    // SSE connection with RAF batching
    // -------------------------------------------------------------------------
    const {isConnected} = useLiveLogs(logsBufferRef, setLogsVersion, {
        enabled: isLive,
    });

    // Track new logs when not auto-scrolling
    useEffect(() => {
        if (!isAutoScrollRef.current && logsVersion > 0) {
            // Approximate: we don't know exact count, but version change means new logs
            setNewLogsCount((c) => c + 1);
        }
    }, [logsVersion]);

    // -------------------------------------------------------------------------
    // Derived data
    // -------------------------------------------------------------------------

    // Get logs from buffer (re-runs when version changes)
    const logs = useMemo(() => {
        void logsVersion; // Dependency trigger - version change means buffer updated
        return logsBufferRef.current.toArray();
    }, [logsVersion]);

    // Unique services for filter dropdown
    const services = useMemo(() => {
        const set = new Set(logs.map((l) => l.serviceName));
        return Array.from(set).sort();
    }, [logs]);

    // Filtered logs for display
    const filteredLogs = useMemo(() => {
        const minLevelIndex = LOG_LEVELS.indexOf(minLevel);

        return logs.filter((log) => {
            // Level filter
            const levelIndex = LOG_LEVELS.indexOf(log.severityText);
            if (levelIndex < minLevelIndex) return false;

            // Service filter
            if (selectedService !== 'all' && log.serviceName !== selectedService) return false;

            // Text filter
            if (filterText) {
                const searchLower = filterText.toLowerCase();
                const matches =
                    log.body.toLowerCase().includes(searchLower) ||
                    log.serviceName.toLowerCase().includes(searchLower) ||
                    Object.values(log.attributes).some((v) =>
                        String(v).toLowerCase().includes(searchLower)
                    );
                if (!matches) return false;
            }

            return true;
        });
    }, [logs, minLevel, selectedService, filterText]);

    // Stats
    const stats = useMemo(() => {
        return {
            total: logs.length,
            errors: logs.filter((l) => l.severityText === 'error' || l.severityText === 'fatal')
                .length,
            warnings: logs.filter((l) => l.severityText === 'warn').length,
        };
    }, [logs]);

    // -------------------------------------------------------------------------
    // Composite key helper - survives buffer rotation
    // -------------------------------------------------------------------------
    const getLogKey = useCallback((log: LogRecord) => {
        return `${log.traceId ?? ''}:${log.timestamp}`;
    }, []);

    // -------------------------------------------------------------------------
    // Virtualizer
    // -------------------------------------------------------------------------
    const rowVirtualizer = useVirtualizer({
        count: filteredLogs.length,
        getScrollElement: () => parentRef.current,
        estimateSize: (index) => {
            const log = filteredLogs[index];
            if (!log) return 44;
            return expandedLogsRef.current.has(getLogKey(log)) ? 300 : 44;
        },
        overscan: 20,
    });

    // Remeasure when expanded set changes
    const prevExpandedRef = useRef(expandedLogs);
    if (prevExpandedRef.current !== expandedLogs) {
        prevExpandedRef.current = expandedLogs;
        queueMicrotask(() => rowVirtualizer.measure());
    }

    // Remeasure on buffer wrap-around (indices shifted)
    useEffect(() => {
        const currentGen = logsBufferRef.current.generation;
        if (currentGen !== lastGenerationRef.current) {
            lastGenerationRef.current = currentGen;
            // With composite keys, we don't need to clear expanded state anymore!
            // Just remeasure since indices may have shifted
            rowVirtualizer.measure();
        }
    }, [logsVersion, rowVirtualizer]);

    // -------------------------------------------------------------------------
    // Scroll handling for tail-f
    // -------------------------------------------------------------------------
    const handleScroll = useCallback(() => {
        const el = parentRef.current;
        if (!el) return;

        const {scrollTop, scrollHeight, clientHeight} = el;
        const distanceFromBottom = scrollHeight - scrollTop - clientHeight;
        const shouldAutoScroll = distanceFromBottom < AUTO_SCROLL_THRESHOLD;

        if (shouldAutoScroll !== isAutoScrollRef.current) {
            setIsAutoScroll(shouldAutoScroll);
            if (shouldAutoScroll) {
                setNewLogsCount(0);
            }
        }
    }, []);

    useEffect(() => {
        const el = parentRef.current;
        if (!el) return;

        el.addEventListener('scroll', handleScroll, {passive: true});
        return () => el.removeEventListener('scroll', handleScroll);
    }, [handleScroll]);

    // Auto-scroll to bottom when new logs arrive (if attached)
    // useLayoutEffect runs BEFORE browser paint - prevents scroll position flicker
    useLayoutEffect(() => {
        if (isAutoScroll && filteredLogs.length > 0) {
            rowVirtualizer.scrollToIndex(filteredLogs.length - 1, {
                align: 'end',
                behavior: 'auto', // 'smooth' causes queuing issues with fast streams
            });
        }
    }, [filteredLogs.length, isAutoScroll, rowVirtualizer]);

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------
    const toggleLog = useCallback((logKey: string) => {
        setExpandedLogs((prev) => {
            const next = new Set(prev);
            if (next.has(logKey)) {
                next.delete(logKey);
            } else {
                next.add(logKey);
            }
            return next;
        });
    }, []);

    const jumpToBottom = useCallback(() => {
        setIsAutoScroll(true);
        setNewLogsCount(0);
        if (filteredLogs.length > 0) {
            rowVirtualizer.scrollToIndex(filteredLogs.length - 1, {
                align: 'end',
                behavior: 'smooth',
            });
        }
    }, [filteredLogs.length, rowVirtualizer]);

    const clearLogs = useCallback(() => {
        logsBufferRef.current.clear();
        setLogsVersion((v) => v + 1);
        setExpandedLogs(new Set());
    }, []);

    // -------------------------------------------------------------------------
    // Render
    // -------------------------------------------------------------------------
    return (
        <div className="flex flex-col h-full">
            {/* Toolbar */}
            <div className="flex items-center gap-4 p-4 border-b border-border">
                {/* Search */}
                <div className="relative flex-1 max-w-sm">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground"/>
                    <Input
                        placeholder="Search logs..."
                        value={filterText}
                        onChange={(e) => setFilterText(e.target.value)}
                        className="pl-9"
                    />
                </div>

                {/* Level filter */}
                <Select value={minLevel} onValueChange={(v) => setMinLevel(v as LogLevel)}>
                    <SelectTrigger className="w-32">
                        <SelectValue placeholder="Min Level"/>
                    </SelectTrigger>
                    <SelectContent>
                        {LOG_LEVELS.map((level) => (
                            <SelectItem key={level} value={level}>
                                {level.toUpperCase()}
                            </SelectItem>
                        ))}
                    </SelectContent>
                </Select>

                {/* Service filter */}
                <Select value={selectedService} onValueChange={setSelectedService}>
                    <SelectTrigger className="w-40">
                        <SelectValue placeholder="Service"/>
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="all">All Services</SelectItem>
                        {services.map((service) => (
                            <SelectItem key={service} value={service}>
                                {service}
                            </SelectItem>
                        ))}
                    </SelectContent>
                </Select>

                {/* Stats */}
                <div className="flex items-center gap-4 text-sm">
          <span className="text-muted-foreground">
            {filteredLogs.length.toLocaleString()} / {stats.total.toLocaleString()} logs
          </span>
                    {stats.errors > 0 && (
                        <Badge variant="destructive">{stats.errors} errors</Badge>
                    )}
                    {stats.warnings > 0 && (
                        <Badge variant="outline" className="border-yellow-500 text-yellow-500">
                            {stats.warnings} warnings
                        </Badge>
                    )}
                </div>

                {/* Clear button */}
                <Button variant="outline" size="sm" onClick={clearLogs}>
                    Clear
                </Button>

                {/* Live toggle */}
                <Button
                    variant={isLive ? 'default' : 'outline'}
                    size="sm"
                    onClick={() => setIsLive(!isLive)}
                    className={cn(isLive && isConnected && 'bg-green-600 hover:bg-green-700')}
                >
          <span
              className={cn(
                  'w-2 h-2 rounded-full mr-2',
                  isLive && isConnected ? 'bg-green-300 animate-pulse' : 'bg-gray-400'
              )}
          />
                    {isLive ? (isConnected ? 'Live' : 'Connecting...') : 'Paused'}
                </Button>
            </div>

            {/* Active Filters */}
            {(filterText || minLevel !== 'trace' || selectedService !== 'all') && (
                <div className="flex items-center gap-2 px-4 pb-2 flex-wrap">
                    <span className="text-xs text-muted-foreground">Active filters:</span>
                    {filterText && (
                        <Badge variant="secondary" className="gap-1 pr-1">
                            search: {filterText}
                            <button
                                onClick={() => setFilterText('')}
                                className="ml-1 rounded-full hover:bg-muted p-0.5"
                            >
                                <X className="h-3 w-3"/>
                            </button>
                        </Badge>
                    )}
                    {minLevel !== 'trace' && (
                        <Badge variant="secondary" className="gap-1 pr-1">
                            min level: {minLevel}
                            <button
                                onClick={() => setMinLevel('trace')}
                                className="ml-1 rounded-full hover:bg-muted p-0.5"
                            >
                                <X className="h-3 w-3"/>
                            </button>
                        </Badge>
                    )}
                    {selectedService !== 'all' && (
                        <Badge variant="secondary" className="gap-1 pr-1">
                            service: {selectedService}
                            <button
                                onClick={() => setSelectedService('all')}
                                className="ml-1 rounded-full hover:bg-muted p-0.5"
                            >
                                <X className="h-3 w-3"/>
                            </button>
                        </Badge>
                    )}
                    <Button
                        variant="ghost"
                        size="sm"
                        className="h-6 text-xs"
                        onClick={() => {
                            setFilterText('');
                            setMinLevel('trace');
                            setSelectedService('all');
                        }}
                    >
                        Clear all
                    </Button>
                </div>
            )}

            {/* Virtualized Logs */}
            <div
                ref={parentRef}
                className="flex-1 overflow-auto relative"
                style={{contain: 'strict'}}
            >
                {filteredLogs.length === 0 ? (
                    <div className="py-12 text-center text-muted-foreground">
                        <FileText className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                        <p>No logs found</p>
                        <p className="text-sm">Adjust filters or wait for new logs</p>
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
                            const log = filteredLogs[virtualRow.index];
                            const logKey = getLogKey(log);
                            return (
                                <div
                                    key={virtualRow.key}
                                    data-index={virtualRow.index}
                                    ref={rowVirtualizer.measureElement}
                                    style={{
                                        position: 'absolute',
                                        top: 0,
                                        left: 0,
                                        width: '100%',
                                        transform: `translateY(${virtualRow.start}px)`,
                                    }}
                                >
                                    <LogRow
                                        log={log}
                                        isExpanded={expandedLogs.has(logKey)}
                                        onToggle={() => toggleLog(logKey)}
                                    />
                                </div>
                            );
                        })}
                    </div>
                )}

                {/* Jump to bottom button (shown when scrolled away from bottom) */}
                {!isAutoScroll && filteredLogs.length > 0 && (
                    <Button
                        className="absolute bottom-4 right-4 shadow-lg"
                        onClick={jumpToBottom}
                    >
                        <ArrowDown className="w-4 h-4 mr-2"/>
                        {newLogsCount > 0 ? `${newLogsCount} new logs` : 'Jump to bottom'}
                    </Button>
                )}
            </div>
        </div>
    );
}
