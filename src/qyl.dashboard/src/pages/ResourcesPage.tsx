import {memo, useMemo, useRef, useState} from 'react';
import {Link} from 'react-router-dom';
import {useVirtualizer} from '@tanstack/react-virtual';
import {Activity, AlertCircle, ArrowUpRight, Clock, LayoutGrid, List, Server, Zap,} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent, CardHeader, CardTitle} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Badge} from '@/components/ui/badge';
import {useSessions} from '@/hooks/use-telemetry';
import type {Session} from '@/types';
import {getPrimaryService} from '@/types';

type ViewMode = 'grid' | 'list' | 'graph';

// =============================================================================
// ResourceCard - Grid view card (not virtualized - grid layout is more complex)
// =============================================================================

function ResourceCard({session}: { session: Session }) {
    const hasErrors = session.errorCount > 0;
    const errorRate =
        session.spanCount > 0 ? ((session.errorCount / session.spanCount) * 100).toFixed(1) : '0';

    return (
        <Card
            className={cn(
                'transition-all hover:border-primary/50 cursor-pointer',
                hasErrors && 'border-red-500/30'
            )}
        >
            <CardHeader className="pb-2">
                <div className="flex items-start justify-between">
                    <div className="flex items-center gap-2">
                        <div className={cn('status-dot', hasErrors ? 'status-error' : 'status-running')}/>
                        <CardTitle className="text-base font-medium">{getPrimaryService(session)}</CardTitle>
                    </div>
                    <Link to={`/traces?session=${session.sessionId}`}>
                        <Button variant="ghost" size="icon" className="h-8 w-8">
                            <ArrowUpRight className="w-4 h-4"/>
                        </Button>
                    </Link>
                </div>
            </CardHeader>
            <CardContent>
                <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                        <div className="text-muted-foreground">Spans</div>
                        <div className="font-mono text-lg">{session.spanCount.toLocaleString()}</div>
                    </div>
                    <div>
                        <div className="text-muted-foreground">Errors</div>
                        <div className={cn('font-mono text-lg', hasErrors && 'text-red-500')}>
                            {session.errorCount.toLocaleString()}
                        </div>
                    </div>
                    <div>
                        <div className="text-muted-foreground">Error Rate</div>
                        <div
                            className={cn(
                                'font-mono',
                                parseFloat(errorRate) > 5 && 'text-yellow-500',
                                parseFloat(errorRate) > 10 && 'text-red-500'
                            )}
                        >
                            {errorRate}%
                        </div>
                    </div>
                    <div>
                        <div className="text-muted-foreground">Started</div>
                        <div className="font-mono text-xs">
                            {new Date(session.startTime).toLocaleTimeString()}
                        </div>
                    </div>
                </div>

                {/* GenAI stats if available */}
                {(session.totalInputTokens > 0 || session.totalOutputTokens > 0) && (
                    <div className="mt-4 pt-4 border-t border-border">
                        <div className="flex items-center gap-2 mb-2">
                            <Zap className="w-4 h-4 text-violet-500"/>
                            <span className="text-sm text-muted-foreground">GenAI Usage</span>
                        </div>
                        <div className="grid grid-cols-3 gap-2 text-xs">
                            <div>
                                <div className="text-muted-foreground">Tokens In</div>
                                <div
                                    className="font-mono">{session.totalInputTokens.toLocaleString()}</div>
                            </div>
                            <div>
                                <div className="text-muted-foreground">Tokens Out</div>
                                <div className="font-mono">
                                    {session.totalOutputTokens.toLocaleString()}
                                </div>
                            </div>
                            <div>
                                <div className="text-muted-foreground">Cost</div>
                                <div className="font-mono text-green-500">
                                    ${session.totalCostUsd.toFixed(4)}
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}

// =============================================================================
// ResourceRow - List view row (memoized for virtualization)
// =============================================================================

const ROW_HEIGHT = 64; // Fixed height for virtualization

const ResourceRow = memo(function ResourceRow({session}: { session: Session }) {
    const hasErrors = session.errorCount > 0;

    return (
        <Link
            to={`/traces?session=${session.sessionId}`}
            className={cn(
                'flex items-center gap-4 px-4 hover:bg-muted/50 border-b border-border',
                hasErrors && 'bg-red-500/5'
            )}
            style={{height: ROW_HEIGHT}}
        >
            <div className={cn('status-dot', hasErrors ? 'status-error' : 'status-running')}/>

            <div className="flex-1 min-w-0">
                <div className="font-medium truncate">{getPrimaryService(session)}</div>
                <div className="text-xs text-muted-foreground">
                    Session: {session.sessionId.slice(0, 8)}...
                </div>
            </div>

            <div className="text-right">
                <div className="font-mono">{session.spanCount.toLocaleString()}</div>
                <div className="text-xs text-muted-foreground">spans</div>
            </div>

            {hasErrors && (
                <Badge variant="destructive" className="ml-2">
                    {session.errorCount} errors
                </Badge>
            )}

            <ArrowUpRight className="w-4 h-4 text-muted-foreground"/>
        </Link>
    );
});

// =============================================================================
// VirtualizedListView - List view with virtualization
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
            <div className="py-12 text-center text-muted-foreground">
                <Activity className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                <p>No active sessions</p>
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
// GraphView - Placeholder graph visualization
// =============================================================================

function GraphView({sessions}: { sessions: Session[] }) {
    const nodes = useMemo(() => {
        const serviceMap = new Map<string, { name: string; spans: number; errors: number }>();

        for (const session of sessions) {
            const serviceName = getPrimaryService(session);
            const existing = serviceMap.get(serviceName);
            if (existing) {
                existing.spans += session.spanCount;
                existing.errors += session.errorCount;
            } else {
                serviceMap.set(serviceName, {
                    name: serviceName,
                    spans: session.spanCount,
                    errors: session.errorCount,
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
                            'relative p-4 rounded-lg border-2 bg-card min-w-[160px]',
                            node.errors > 0 ? 'border-red-500/50' : 'border-primary/30'
                        )}
                        style={{
                            transform: `translateY(${Math.sin(i * 0.8) * 20}px)`,
                        }}
                    >
                        <div
                            className={cn(
                                'absolute -top-2 -right-2 w-4 h-4 rounded-full',
                                node.errors > 0 ? 'status-error' : 'status-running'
                            )}
                        />

                        <div className="text-center">
                            <Server className="w-8 h-8 mx-auto mb-2 text-primary"/>
                            <div className="font-medium text-sm truncate">{node.name}</div>
                            <div className="text-xs text-muted-foreground mt-1">
                                {node.spans.toLocaleString()} spans
                            </div>
                        </div>
                    </div>
                ))}
            </div>

            {nodes.length === 0 && (
                <div className="text-center text-muted-foreground py-12">
                    <Server className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                    <p>No resources found</p>
                    <p className="text-sm">Resources will appear as telemetry data arrives</p>
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
        const totalSpans = sessions.reduce((acc, s) => acc + s.spanCount, 0);
        const totalErrors = sessions.reduce((acc, s) => acc + s.errorCount, 0);
        const errorRate = totalSpans > 0 ? ((totalErrors / totalSpans) * 100).toFixed(2) : '0';
        const activeServices = new Set(sessions.map((s) => getPrimaryService(s))).size;

        return {totalSpans, totalErrors, errorRate, activeServices};
    }, [sessions]);

    return (
        <div className="p-6 space-y-6">
            {/* Stats cards */}
            <div className="grid grid-cols-4 gap-4">
                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Server className="w-4 h-4 text-primary"/>
                            <span className="text-sm text-muted-foreground">Services</span>
                        </div>
                        <div className="text-2xl font-bold mt-1">{stats.activeServices}</div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Activity className="w-4 h-4 text-cyan-500"/>
                            <span className="text-sm text-muted-foreground">Total Spans</span>
                        </div>
                        <div className="text-2xl font-bold mt-1">{stats.totalSpans.toLocaleString()}</div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <AlertCircle className="w-4 h-4 text-red-500"/>
                            <span className="text-sm text-muted-foreground">Errors</span>
                        </div>
                        <div className="text-2xl font-bold mt-1 text-red-500">
                            {stats.totalErrors.toLocaleString()}
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent className="pt-4">
                        <div className="flex items-center gap-2">
                            <Clock className="w-4 h-4 text-yellow-500"/>
                            <span className="text-sm text-muted-foreground">Error Rate</span>
                        </div>
                        <div
                            className={cn(
                                'text-2xl font-bold mt-1',
                                parseFloat(stats.errorRate) > 5 && 'text-yellow-500',
                                parseFloat(stats.errorRate) > 10 && 'text-red-500'
                            )}
                        >
                            {stats.errorRate}%
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* View mode toggle */}
            <div className="flex items-center justify-between">
                <h2 className="text-lg font-semibold">Active Sessions</h2>
                <div className="flex items-center gap-1 bg-muted rounded-lg p-1">
                    <Button
                        variant={viewMode === 'grid' ? 'secondary' : 'ghost'}
                        size="sm"
                        onClick={() => setViewMode('grid')}
                    >
                        <LayoutGrid className="w-4 h-4"/>
                    </Button>
                    <Button
                        variant={viewMode === 'list' ? 'secondary' : 'ghost'}
                        size="sm"
                        onClick={() => setViewMode('list')}
                    >
                        <List className="w-4 h-4"/>
                    </Button>
                    <Button
                        variant={viewMode === 'graph' ? 'secondary' : 'ghost'}
                        size="sm"
                        onClick={() => setViewMode('graph')}
                    >
                        <Activity className="w-4 h-4"/>
                    </Button>
                </div>
            </div>

            {/* Content */}
            {isLoading ? (
                <div className="flex items-center justify-center py-12">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"/>
                </div>
            ) : viewMode === 'grid' ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {sessions.map((session) => (
                        <ResourceCard key={session.sessionId} session={session}/>
                    ))}
                    {sessions.length === 0 && (
                        <Card className="col-span-full">
                            <CardContent className="py-12 text-center text-muted-foreground">
                                <Activity className="w-12 h-12 mx-auto mb-4 opacity-50"/>
                                <p>No active sessions</p>
                                <p className="text-sm">Sessions will appear as telemetry data arrives</p>
                            </CardContent>
                        </Card>
                    )}
                </div>
            ) : viewMode === 'list' ? (
                <Card>
                    <VirtualizedListView sessions={sessions}/>
                </Card>
            ) : (
                <Card className="min-h-[400px]">
                    <GraphView sessions={sessions}/>
                </Card>
            )}
        </div>
    );
}
