import {memo, useCallback, useDeferredValue, useEffect, useMemo, useRef, useState} from 'react';
import {z} from 'zod';
import {useVirtualizer} from '@tanstack/react-virtual';
import {
    AlertCircle,
    AlertTriangle,
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
import {CopyableText} from '@/components/ui/copyable-text';
import {DownloadButton} from '@/components/ui/download-button';
import {isStructuredContent, TextVisualizer} from '@/components/ui/text-visualizer';
import {OnboardingHint} from '@/components/OnboardingHint';
import {formatTimestamp} from '@/hooks/use-telemetry';
import {useLogHistory} from '@/hooks/use-log-history';
import {formatAttributeValue} from '@ancplua/qyl-api-schema/runtime';
import type {AttributeValue, LogRecord} from '@ancplua/qyl-api-schema/types';

const LOG_LEVELS = ['trace', 'debug', 'info', 'warn', 'error', 'fatal'] as const;

const logLevelSchema = z.enum(LOG_LEVELS);

type LogLevel = z.infer<typeof logLevelSchema>;

// `severity_text` is free-form on the wire, so an unrecognised label falls through to the
// numeric severity mapping below; the level picker falls back to its unfiltered default.
const minLevelSchema = logLevelSchema.catch('trace');

interface LogViewRecord {
    original: LogRecord;
    timestamp: string;
    observedTimestamp: string;
    traceId?: string;
    spanId?: string;
    eventName?: string;
    severityNumber: number;
    severityText: LogLevel;
    body: string;
    attributes: Record<string, AttributeValue>;
    serviceName: string;
    serviceVersion?: string;
}

const LOG_LEVEL_CONFIG: Record<
    LogLevel,
    { icon: typeof Info; className: string; color: string }
> = {
    trace: {icon: Bug, className: 'log-level-trace', color: 'text-brutal-slate'},
    debug: {icon: Bug, className: 'log-level-debug', color: 'text-signal-cyan'},
    info: {icon: Info, className: 'log-level-info', color: 'text-signal-cyan'},
    warn: {icon: AlertTriangle, className: 'log-level-warn', color: 'text-signal-yellow'},
    error: {icon: AlertCircle, className: 'log-level-error', color: 'text-signal-red'},
    fatal: {icon: Skull, className: 'log-level-fatal', color: 'text-signal-red'},
};

function normalizeLogRecord(log: LogRecord): LogViewRecord {
    const timeUnixNano = log.time_unix_nano;
    const observedUnixNano = log.observed_time_unix_nano;
    const severityNumber = log.severity_number;

    return {
        original: log,
        timestamp: unixNanoToIso(timeUnixNano),
        observedTimestamp: unixNanoToIso(observedUnixNano),
        traceId: log.trace_id,
        spanId: log.span_id,
        eventName: log.event_name,
        severityNumber,
        severityText: normalizeSeverity(log.severity_text, severityNumber),
        body: normalizeBody(log.body),
        attributes: normalizeAttributes(log.attributes),
        serviceName: log.resource.service_name,
        serviceVersion: log.resource.service_version,
    };
}

export function normalizeSeverity(severityText: LogRecord['severity_text'], severityNumber: number): LogLevel {
    if (typeof severityText === 'string') {
        const named = logLevelSchema.safeParse(severityText.trim().toLowerCase());
        if (named.success) return named.data;
    }

    return severityNumber >= 21 ? 'fatal'
        : severityNumber >= 17 ? 'error'
            : severityNumber >= 13 ? 'warn'
                : severityNumber >= 9 ? 'info'
                    : severityNumber >= 5 ? 'debug'
                        : 'trace';
}

function normalizeBody(body: LogRecord['body']): string {
    return formatAttributeValue(body);
}

function normalizeAttributes(
    attributes?: LogRecord['attributes'],
): LogViewRecord['attributes'] {
    if (!attributes) return {};
    const record: LogViewRecord['attributes'] = {};
    for (const attr of attributes) record[attr.key] = attr.value;
    return record;
}

// Absolute nanosecond timestamps arrive as decimal strings and exceed Number.MAX_SAFE_INTEGER;
// the millisecond floor is taken in BigInt so no precision is assumed before narrowing.
function unixNanoToIso(value: string): string {
    return new Date(Number(BigInt(value) / 1_000_000n)).toISOString();
}

interface LogRowProps {
    log: LogViewRecord;
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
                    'flex items-start gap-3 px-4 py-2 hover:bg-brutal-dark cursor-pointer',
                    log.severityText === 'error' && 'bg-signal-red/5',
                    log.severityText === 'fatal' && 'bg-signal-red/10',
                    log.severityText === 'warn' && 'bg-signal-yellow/5'
                )}
                role="button"
                tabIndex={0}
                aria-expanded={isExpanded}
                onClick={onToggle}
                onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        onToggle();
                    }
                }}
            >
                <div className="pt-0.5">
                    {isExpanded ? (
                        <ChevronDown className="w-4 h-4 text-brutal-slate"/>
                    ) : (
                        <ChevronRight className="w-4 h-4 text-brutal-slate"/>
                    )}
                </div>

                <span className="font-mono text-xs text-brutal-slate w-24 flex-shrink-0">
          {formatTimestamp(log.timestamp)}
        </span>

                <Badge variant="outline" className={cn('text-xs', config.className)}>
                    <Icon className="w-3 h-3 mr-1"/>
                    {log.severityText.toUpperCase()}
                </Badge>

                <span className="text-sm text-brutal-slate w-28 truncate flex-shrink-0">
          {log.serviceName}
        </span>

                <span className="text-sm flex-1 truncate font-mono">{log.body}</span>
            </div>

            {isExpanded && (
                <div className="px-4 py-3 bg-brutal-dark/30 border-t border-border">
                    <div className="pl-8 space-y-3">
                        <div>
                            <h4 className="text-xs font-medium text-brutal-slate mb-1">Message</h4>
                            {isStructuredContent(log.body) ? (
                                <TextVisualizer
                                    content={log.body}
                                    label="Message"
                                    defaultExpanded={true}
                                    maxCollapsedHeight={200}
                                    showTreeView={true}
                                />
                            ) : (
                                <pre className="text-sm font-mono whitespace-pre-wrap bg-background p-2">
                                    {log.body}
                                </pre>
                            )}
                        </div>

                        {log.traceId && (
                            <div className="grid grid-cols-2 gap-4">
                                <div>
                                    <h4 className="text-xs font-medium text-brutal-slate mb-1">Trace ID</h4>
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
                                        <h4 className="text-xs font-medium text-brutal-slate mb-1">Span ID</h4>
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

                        {log.eventName && (
                            <div>
                                <h4 className="text-xs font-medium text-brutal-slate mb-1">Event name</h4>
                                <CopyableText value={log.eventName} label="Event name"/>
                            </div>
                        )}

                        {Object.keys(log.attributes).length > 0 && (
                            <div>
                                <h4 className="text-xs font-medium text-brutal-slate mb-1">Attributes</h4>
                                <div className="grid grid-cols-2 gap-2">
                                    {Object.entries(log.attributes).map(([key, value]) => (
                                        <div key={key} className="flex items-center text-sm">
                                            <span className="text-brutal-slate">{key}:</span>
                                            <CopyableText
                                                value={formatAttributeValue(value)}
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

export function LogsPage() {
    const [expandedLogs, setExpandedLogs] = useState<Set<string>>(new Set());
    const [filterText, setFilterText] = useState('');
    const [minLevel, setMinLevel] = useState<LogLevel>('trace');
    const [selectedService, setSelectedService] = useState('');
    const [isLive, setIsLive] = useState(true);
    const query = useDeferredValue(filterText);
    const service = useDeferredValue(selectedService);
    const filters = useMemo(() => ({
        query,
        service,
        // An unfiltered view also includes unspecified (zero) severities.
        severityMin: minLevel === 'trace' ? undefined : LOG_LEVELS.indexOf(minLevel) * 4 + 1,
    }), [query, service, minLevel]);
    const history = useLogHistory(filters, isLive);
    const logs = useMemo(() => history.data?.pages.flatMap(page => page.items).map(normalizeLogRecord) ?? [], [history.data]);
    const error = history.error?.message ?? history.cursorError ?? history.streamError;
    const parentRef = useRef<HTMLDivElement>(null);
    const getLogKey = useCallback((log: LogViewRecord, index: number) =>
        `${index}:${log.original.time_unix_nano}:${log.spanId ?? ''}`, []);
    const rowVirtualizer = useVirtualizer({
        count: logs.length,
        getScrollElement: () => parentRef.current,
        getItemKey: index => getLogKey(logs[index], index),
        estimateSize: index => expandedLogs.has(getLogKey(logs[index], index)) ? 300 : 44,
        overscan: 20,
    });
    useEffect(() => { rowVirtualizer.measure(); }, [expandedLogs, rowVirtualizer]);
    const stats = useMemo(() => ({
        errors: logs.filter(log => log.severityNumber >= 17).length,
        warnings: logs.filter(log => log.severityNumber >= 13 && log.severityNumber < 17).length,
    }), [logs]);
    const toggleLog = useCallback((key: string) => {
        setExpandedLogs(previous => {
            const next = new Set(previous);
            if (next.has(key)) next.delete(key);
            else next.add(key);
            return next;
        });
    }, []);

    return (
        <div className="flex flex-col h-full">
            <div className="flex items-center gap-4 p-4 border-b border-border flex-wrap">
                <div className="relative flex-1 max-w-sm">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-brutal-slate"/>
                    <Input
                        placeholder="Search log messages..."
                        value={filterText}
                        onChange={event => setFilterText(event.target.value)}
                        className="pl-9"
                        aria-label="Search logs"
                    />
                </div>
                <Select value={minLevel} onValueChange={value => setMinLevel(minLevelSchema.parse(value))}>
                    <SelectTrigger className="w-32" aria-label="Minimum log level">
                        <SelectValue placeholder="Min Level"/>
                    </SelectTrigger>
                    <SelectContent>
                        {LOG_LEVELS.map(level => (
                            <SelectItem key={level} value={level}>{level.toUpperCase()}</SelectItem>
                        ))}
                    </SelectContent>
                </Select>
                <Input
                    value={selectedService}
                    onChange={event => setSelectedService(event.target.value)}
                    placeholder="Service name (exact)"
                    aria-label="Filter service"
                    className="w-48"
                />
                <span role="status" aria-label="Log results" className="text-sm text-brutal-slate">
                    {logs.length} logs loaded{history.hasNextPage ? ' · more available' : ''}
                </span>
                {stats.errors > 0 && <Badge variant="destructive">{stats.errors} loaded errors</Badge>}
                {stats.warnings > 0 && (
                    <Badge variant="outline" className="border-signal-yellow text-signal-yellow">
                        {stats.warnings} loaded warnings
                    </Badge>
                )}
                <Button variant="outline" size="sm" onClick={() => { void history.refetch(); }} disabled={history.isFetching}>
                    Refresh
                </Button>
                <div title="Download loaded matching logs">
                    <DownloadButton
                        getJsonData={() => logs.map(log => log.original)}
                        getCsvData={() => logs.map(log => ({
                            timestamp: log.timestamp,
                            level: log.severityText,
                            service: log.serviceName,
                            message: log.body,
                            traceId: log.traceId ?? '',
                            spanId: log.spanId ?? '',
                            eventName: log.eventName ?? '',
                            attributes: log.attributes,
                        }))}
                        filenamePrefix="logs"
                        columns={['timestamp', 'level', 'service', 'message', 'traceId', 'spanId', 'eventName', 'attributes']}
                        disabled={logs.length === 0}
                    />
                </div>
                <Button
                    variant={isLive ? 'default' : 'outline'}
                    size="sm"
                    aria-pressed={isLive}
                    onClick={() => setIsLive(!isLive)}
                    className={cn(isLive && history.isConnected && 'bg-signal-green hover:bg-signal-green/80')}
                >
                    {isLive ? (history.isConnected ? 'Live' : 'Connecting...') : 'Paused'}
                </Button>
            </div>
            {(filterText || minLevel !== 'trace' || selectedService) && (
                <div className="flex items-center gap-2 px-4 pb-2 flex-wrap">
                    <span className="text-xs text-brutal-slate">Active filters:</span>
                    {filterText && (
                        <Badge variant="secondary" className="gap-1 pr-1">
                            search: {filterText}
                            <button onClick={() => setFilterText('')} aria-label="Clear search filter">
                                <X className="h-3 w-3"/>
                            </button>
                        </Badge>
                    )}
                    {minLevel !== 'trace' && (
                        <Badge variant="secondary" className="gap-1 pr-1">
                            min level: {minLevel}
                            <button onClick={() => setMinLevel('trace')} aria-label="Clear minimum level filter">
                                <X className="h-3 w-3"/>
                            </button>
                        </Badge>
                    )}
                    {selectedService && (
                        <Badge variant="secondary" className="gap-1 pr-1">
                            service: {selectedService}
                            <button onClick={() => setSelectedService('')} aria-label="Clear service filter">
                                <X className="h-3 w-3"/>
                            </button>
                        </Badge>
                    )}
                    <Button variant="ghost" size="sm" className="h-6 text-xs" onClick={() => {
                        setFilterText('');
                        setMinLevel('trace');
                        setSelectedService('');
                    }}>Clear all</Button>
                </div>
            )}
            {error && (
                <div role="alert" className="px-4 py-2 text-signal-red border-b border-border">
                    {error}
                </div>
            )}
            <div ref={parentRef} className="flex-1 overflow-auto relative" style={{contain: 'strict'}}>
                {history.isPending ? (
                    <p className="p-4 text-brutal-slate">Loading logs...</p>
                ) : logs.length === 0 && !error ? (
                    <OnboardingHint icon={FileText} title="No logs found" description="Adjust filters or connect a service"/>
                ) : (
                    <div style={{height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative'}}>
                        {rowVirtualizer.getVirtualItems().map(virtualRow => {
                            const log = logs[virtualRow.index];
                            const key = getLogKey(log, virtualRow.index);
                            return (
                                <div
                                    key={virtualRow.key}
                                    data-index={virtualRow.index}
                                    ref={rowVirtualizer.measureElement}
                                    style={{position: 'absolute', top: 0, left: 0, width: '100%', transform: `translateY(${virtualRow.start}px)`}}
                                >
                                    <LogRow log={log} isExpanded={expandedLogs.has(key)} onToggle={() => toggleLog(key)}/>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>
            <div className="flex items-center gap-4 p-3 border-t border-border text-sm text-brutal-slate">
                {history.hasNextPage ? (
                    <Button variant="outline" size="sm" disabled={history.isFetching || Boolean(history.cursorError)} onClick={() => {
                        // Hold the viewed pages steady while browsing older records.
                        setIsLive(false);
                        void history.fetchNextPage();
                    }}>{history.isFetchingNextPage ? 'Loading older...' : 'Load older'}</Button>
                ) : !history.isPending && !error ? <span>All matching logs loaded</span> : null}
                <span>Downloads include the loaded records.</span>
            </div>
        </div>
    );
}
