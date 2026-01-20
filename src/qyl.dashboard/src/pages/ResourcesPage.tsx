import {memo, useMemo, useRef, useState} from 'react';
import {Link} from 'react-router-dom';
import {useVirtualizer} from '@tanstack/react-virtual';
import {
    Activity,
    AlertCircle,
    ArrowUpRight,
    Clock,
    DollarSign,
    LayoutGrid,
    List,
    Server,
    Zap,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {CopyableText} from '@/components/ui';
import {useSessions} from '@/hooks/use-telemetry';
import type {Session} from '@/types';
import {getPrimaryService} from '@/types';

type ViewMode = 'grid' | 'list' | 'graph';

// =============================================================================
// BRUTALIST Metric Card
// =============================================================================

interface MetricCardProps {
    label: string;
    value: string | number;
    icon: React.ReactNode;
    variant?: 'default' | 'success' | 'warning' | 'error' | 'info' | 'violet';
}

const variantStyles = {
    default: 'border-brutal-zinc text-brutal-white',
    success: 'border-signal-green text-signal-green',
    warning: 'border-signal-yellow text-signal-yellow',
    error: 'border-signal-red text-signal-red',
    info: 'border-signal-cyan text-signal-cyan',
    violet: 'border-signal-violet text-signal-violet',
};

function MetricCard({label, value, icon, variant = 'default'}: MetricCardProps) {
    const style = variantStyles[variant];
    return (
        <div className={cn(
            'bg-brutal-dark border-3 p-4 transition-all hover:translate-x-[-2px] hover:translate-y-[-2px] hover:shadow-brutal',
            style.split(' ')[0]
        )}>
            <div className="flex items-center justify-between mb-3">
                <span className="label-industrial">{label}</span>
                <div className={cn('p-1', style.split(' ')[1])}>
                    {icon}
                </div>
            </div>
            <div className={cn('text-3xl font-bold font-mono', style.split(' ')[1])}>
                {typeof value === 'number' ? value.toLocaleString() : value}
            </div>
        </div>
    );
}

// =============================================================================
// BRUTALIST ResourceCard - Grid view
// =============================================================================

function ResourceCard({session}: { session: Session }) {
    const hasErrors = session.error_count > 0;
    const errorRate =
        session.span_count > 0 ? ((session.error_count / session.span_count) * 100).toFixed(1) : '0';
    const sessionId = session['session.id'];
    const inputTokens = session.genai_usage?.total_input_tokens ?? 0;
    const outputTokens = session.genai_usage?.total_output_tokens ?? 0;
    const costUsd = session.genai_usage?.estimated_cost_usd ?? 0;

    return (
        <div className={cn(
            'bg-brutal-dark border-3 transition-all hover:translate-x-[-2px] hover:translate-y-[-2px] hover:shadow-brutal',
            hasErrors ? 'border-signal-red' : 'border-brutal-zinc hover:border-signal-orange'
        )}>
            {/* Accent bar */}
            <div className={cn('h-1', hasErrors ? 'bg-signal-red' : 'bg-signal-orange')}/>

            {/* Header */}
            <div className="flex items-start justify-between p-4 border-b-3 border-brutal-zinc">
                <div className="flex items-center gap-3">
                    <div className={cn('status-dot', hasErrors ? 'bg-signal-red glow-red' : 'bg-signal-green glow-green')}/>
                    <div>
                        <div className="font-bold text-brutal-white tracking-wider">{getPrimaryService(session)}</div>
                        <div className="text-[10px] text-brutal-slate tracking-wider mt-1">
                            SESSION: {sessionId.slice(0, 8)}...
                        </div>
                    </div>
                </div>
                <Link to={`/traces?session=${sessionId}`}>
                    <Button
                        variant="outline"
                        size="icon"
                        className="border-2 border-brutal-zinc bg-brutal-carbon hover:border-signal-orange hover:bg-signal-orange/10"
                    >
                        <ArrowUpRight className="w-4 h-4"/>
                    </Button>
                </Link>
            </div>

            {/* Stats */}
            <div className="p-4">
                <div className="grid grid-cols-2 gap-4">
                    <div>
                        <div className="label-industrial mb-1">SPANS</div>
                        <div className="font-mono text-xl text-signal-cyan">{session.span_count.toLocaleString()}</div>
                    </div>
                    <div>
                        <div className="label-industrial mb-1">ERRORS</div>
                        <div className={cn(
                            'font-mono text-xl',
                            hasErrors ? 'text-signal-red' : 'text-brutal-slate'
                        )}>
                            {session.error_count.toLocaleString()}
                        </div>
                    </div>
                    <div>
                        <div className="label-industrial mb-1">ERROR RATE</div>
                        <div className={cn(
                            'font-mono',
                            parseFloat(errorRate) > 5 && 'text-signal-yellow',
                            parseFloat(errorRate) > 10 && 'text-signal-red'
                        )}>
                            {errorRate}%
                        </div>
                    </div>
                    <div>
                        <div className="label-industrial mb-1">STARTED</div>
                        <div className="font-mono text-xs text-brutal-slate">
                            {new Date(session.start_time).toLocaleTimeString('en-US', {hour12: false})}
                        </div>
                    </div>
                </div>

                {/* GenAI stats */}
                {(inputTokens > 0 || outputTokens > 0) && (
                    <div className="mt-4 pt-4 border-t-3 border-brutal-zinc">
                        <div className="flex items-center gap-2 mb-3">
                            <Zap className="w-4 h-4 text-signal-violet"/>
                            <span className="label-industrial text-signal-violet">GENAI USAGE</span>
                        </div>
                        <div className="grid grid-cols-3 gap-2">
                            <div>
                                <div className="label-industrial mb-1">IN</div>
                                <div className="font-mono text-sm text-signal-violet">
                                    {inputTokens.toLocaleString()}
                                </div>
                            </div>
                            <div>
                                <div className="label-industrial mb-1">OUT</div>
                                <div className="font-mono text-sm text-signal-violet">
                                    {outputTokens.toLocaleString()}
                                </div>
                            </div>
                            <div>
                                <div className="label-industrial mb-1">COST</div>
                                <div className="font-mono text-sm text-signal-green">
                                    ${costUsd.toFixed(4)}
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}

// =============================================================================
// BRUTALIST ResourceRow - List view row
// =============================================================================

const ROW_HEIGHT = 64;

const ResourceRow = memo(function ResourceRow({session}: { session: Session }) {
    const hasErrors = session.error_count > 0;
    const sessionId = session['session.id'];

    return (
        <Link
            to={`/traces?session=${sessionId}`}
            className={cn(
                'flex items-center gap-4 px-4 hover:bg-brutal-carbon border-b-3 border-brutal-zinc transition-all',
                hasErrors && 'bg-signal-red/10 border-l-3 border-l-signal-red'
            )}
            style={{height: ROW_HEIGHT}}
        >
            <div className={cn('status-dot', hasErrors ? 'bg-signal-red glow-red' : 'bg-signal-green glow-green')}/>

            <div className="flex-1 min-w-0">
                <div className="font-bold text-brutal-white tracking-wider truncate">{getPrimaryService(session)}</div>
                <div className="flex items-center text-xs text-brutal-slate">
                    <span className="mr-1">SESSION:</span>
                    <CopyableText
                        value={sessionId}
                        label="Session ID"
                        truncate
                        maxWidth="80px"
                        textClassName="text-xs text-brutal-slate"
                    />
                </div>
            </div>

            <div className="text-right">
                <div className="font-mono text-signal-cyan">{session.span_count.toLocaleString()}</div>
                <div className="label-industrial">SPANS</div>
            </div>

            {hasErrors && (
                <Badge className="bg-signal-red/20 text-signal-red border-2 border-signal-red">
                    {session.error_count} ERRORS
                </Badge>
            )}

            <ArrowUpRight className="w-4 h-4 text-brutal-slate"/>
        </Link>
    );
});

// =============================================================================
// BRUTALIST VirtualizedListView
// =============================================================================

function VirtualizedListView({sessions}: { sessions: Session[] }) {
    const parentRef = useRef<HTMLDivElement>(null);

    const rowVirtualizer = useVirtualizer({
        count: sessions.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => ROW_HEIGHT,
        overscan: 10,
    });

    if (sessions.length === 0) {
        return (
            <div className="py-12 text-center">
                <Activity className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                <div className="text-brutal-slate font-bold tracking-wider">NO ACTIVE SESSIONS</div>
            </div>
        );
    }

    return (
        <div
            ref={parentRef}
            className="h-[600px] overflow-auto"
            style={{contain: 'strict'}}
        >
            <div
                style={{
                    height: `${rowVirtualizer.getTotalSize()}px`,
                    width: '100%',
                    position: 'relative',
                }}
            >
                {rowVirtualizer.getVirtualItems().map((virtualRow) => {
                    const session = sessions[virtualRow.index];
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
                            <ResourceRow session={session}/>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}

// =============================================================================
// BRUTALIST GraphView
// =============================================================================

function GraphView({sessions}: { sessions: Session[] }) {
    const nodes = useMemo(() => {
        const serviceMap = new Map<string, { name: string; spans: number; errors: number }>();

        for (const session of sessions) {
            const serviceName = getPrimaryService(session);
            const existing = serviceMap.get(serviceName);
            if (existing) {
                existing.spans += session.span_count;
                existing.errors += session.error_count;
            } else {
                serviceMap.set(serviceName, {
                    name: serviceName,
                    spans: session.span_count,
                    errors: session.error_count,
                });
            }
        }

        return Array.from(serviceMap.values());
    }, [sessions]);

    return (
        <div className="p-8">
            <div className="flex flex-wrap gap-8 justify-center">
                {nodes.map((node, i) => (
                    <div
                        key={node.name}
                        className={cn(
                            'relative p-4 border-3 bg-brutal-dark min-w-[160px] transition-all hover:translate-x-[-2px] hover:translate-y-[-2px] hover:shadow-brutal',
                            node.errors > 0 ? 'border-signal-red' : 'border-signal-cyan'
                        )}
                        style={{
                            transform: `translateY(${Math.sin(i * 0.8) * 20}px)`,
                        }}
                    >
                        <div className={cn(
                            'absolute -top-2 -right-2 w-4 h-4',
                            node.errors > 0 ? 'bg-signal-red glow-red' : 'bg-signal-green glow-green'
                        )}/>

                        <div className="text-center">
                            <Server className={cn(
                                'w-8 h-8 mx-auto mb-2',
                                node.errors > 0 ? 'text-signal-red' : 'text-signal-cyan'
                            )}/>
                            <div className="font-bold text-sm truncate text-brutal-white tracking-wider">{node.name}</div>
                            <div className="text-xs text-brutal-slate mt-1 font-mono">
                                {node.spans.toLocaleString()} SPANS
                            </div>
                        </div>
                    </div>
                ))}
            </div>

            {nodes.length === 0 && (
                <div className="text-center py-12">
                    <Server className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                    <div className="text-brutal-slate font-bold tracking-wider">NO RESOURCES FOUND</div>
                    <div className="text-sm text-brutal-zinc mt-2">RESOURCES WILL APPEAR AS TELEMETRY DATA ARRIVES</div>
                </div>
            )}
        </div>
    );
}

// =============================================================================
// ResourcesPage Component
// =============================================================================

export function ResourcesPage() {
    const [viewMode, setViewMode] = useState<ViewMode>('grid');
    const {data: sessions = [], isLoading} = useSessions();

    // Stats
    const stats = useMemo(() => {
        const totalSpans = sessions.reduce((acc, s) => acc + s.span_count, 0);
        const totalErrors = sessions.reduce((acc, s) => acc + s.error_count, 0);
        const errorRate = totalSpans > 0 ? ((totalErrors / totalSpans) * 100).toFixed(2) : '0';
        const activeServices = new Set(sessions.map((s) => getPrimaryService(s))).size;
        const totalCost = sessions.reduce((acc, s) => acc + (s.genai_usage?.estimated_cost_usd ?? 0), 0);

        return {totalSpans, totalErrors, errorRate, activeServices, totalCost};
    }, [sessions]);

    return (
        <div className="p-6 space-y-6">
            {/* BRUTALIST Section Header */}
            <div className="section-header">SYSTEM METRICS</div>

            {/* BRUTALIST Stats cards */}
            <div className="grid grid-cols-2 lg:grid-cols-4 xl:grid-cols-5 gap-4">
                <MetricCard
                    label="SERVICES"
                    value={stats.activeServices}
                    icon={<Server className="w-4 h-4"/>}
                    variant="info"
                />
                <MetricCard
                    label="TOTAL SPANS"
                    value={stats.totalSpans}
                    icon={<Activity className="w-4 h-4"/>}
                    variant="info"
                />
                <MetricCard
                    label="ERRORS"
                    value={stats.totalErrors}
                    icon={<AlertCircle className="w-4 h-4"/>}
                    variant={stats.totalErrors > 0 ? 'error' : 'success'}
                />
                <MetricCard
                    label="ERROR RATE"
                    value={`${stats.errorRate}%`}
                    icon={<Clock className="w-4 h-4"/>}
                    variant={parseFloat(stats.errorRate) > 5 ? (parseFloat(stats.errorRate) > 10 ? 'error' : 'warning') : 'success'}
                />
                {stats.totalCost > 0 && (
                    <MetricCard
                        label="TOTAL COST"
                        value={`$${stats.totalCost.toFixed(4)}`}
                        icon={<DollarSign className="w-4 h-4"/>}
                        variant="success"
                    />
                )}
            </div>

            {/* View mode toggle - BRUTALIST */}
            <div className="flex items-center justify-between">
                <div className="section-header border-none pb-0">ACTIVE SESSIONS [{sessions.length}]</div>
                <div className="flex items-center gap-1 bg-brutal-carbon border-2 border-brutal-zinc p-1">
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setViewMode('grid')}
                        className={cn(
                            'border-2 transition-all',
                            viewMode === 'grid'
                                ? 'bg-signal-orange/20 border-signal-orange text-signal-orange'
                                : 'border-transparent text-brutal-slate hover:text-brutal-white'
                        )}
                    >
                        <LayoutGrid className="w-4 h-4"/>
                    </Button>
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setViewMode('list')}
                        className={cn(
                            'border-2 transition-all',
                            viewMode === 'list'
                                ? 'bg-signal-orange/20 border-signal-orange text-signal-orange'
                                : 'border-transparent text-brutal-slate hover:text-brutal-white'
                        )}
                    >
                        <List className="w-4 h-4"/>
                    </Button>
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setViewMode('graph')}
                        className={cn(
                            'border-2 transition-all',
                            viewMode === 'graph'
                                ? 'bg-signal-orange/20 border-signal-orange text-signal-orange'
                                : 'border-transparent text-brutal-slate hover:text-brutal-white'
                        )}
                    >
                        <Activity className="w-4 h-4"/>
                    </Button>
                </div>
            </div>

            {/* Content */}
            {isLoading ? (
                <div className="flex items-center justify-center py-12">
                    <div className="flex flex-col items-center gap-4">
                        <div className="w-8 h-8 border-3 border-signal-orange border-t-transparent animate-spin"/>
                        <div className="text-brutal-slate font-bold tracking-wider">LOADING...</div>
                    </div>
                </div>
            ) : viewMode === 'grid' ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {sessions.map((session) => (
                        <ResourceCard key={session['session.id']} session={session}/>
                    ))}
                    {sessions.length === 0 && (
                        <div className="col-span-full bg-brutal-dark border-3 border-brutal-zinc p-12 text-center">
                            <Activity className="w-12 h-12 mx-auto mb-4 text-brutal-zinc"/>
                            <div className="text-brutal-slate font-bold tracking-wider">NO ACTIVE SESSIONS</div>
                            <div className="text-sm text-brutal-zinc mt-2">SESSIONS WILL APPEAR AS TELEMETRY DATA ARRIVES</div>
                        </div>
                    )}
                </div>
            ) : viewMode === 'list' ? (
                <div className="bg-brutal-dark border-3 border-brutal-zinc">
                    <VirtualizedListView sessions={sessions}/>
                </div>
            ) : (
                <div className="bg-brutal-dark border-3 border-brutal-zinc min-h-[400px]">
                    <GraphView sessions={sessions}/>
                </div>
            )}
        </div>
    );
}
