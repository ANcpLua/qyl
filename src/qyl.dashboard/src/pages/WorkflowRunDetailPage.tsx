import {useParams, useNavigate} from 'react-router-dom';
import {
    AlertCircle,
    ArrowLeft,
    Clock,
    Cpu,
    DollarSign,
    GitBranch,
    Loader2,
    Save,
    Workflow,
} from 'lucide-react';
import {cn} from '@/lib/utils';
import {Card, CardContent} from '@/components/ui/card';
import {Badge} from '@/components/ui/badge';
import {Button} from '@/components/ui/button';
import {CopyableText} from '@/components/ui';
import {formatDuration, nsToMs} from '@/hooks/use-telemetry';
import {useWorkflowRun, useWorkflowEvents, useWorkflowCheckpoints} from '@/hooks/use-workflows';
import type {WorkflowEvent, WorkflowCheckpoint} from '@/hooks/use-workflows';

function StatusBadge({status}: {status: string}) {
    const styles: Record<string, string> = {
        pending: 'bg-slate-500/20 text-slate-400 border-slate-500/40',
        running: 'bg-blue-500/20 text-blue-400 border-blue-500/40',
        completed: 'bg-green-500/20 text-green-400 border-green-500/40',
        failed: 'bg-red-500/20 text-red-400 border-red-500/40',
        cancelled: 'bg-brutal-zinc/40 text-brutal-slate border-brutal-zinc',
    };
    return (
        <Badge variant="outline" className={cn('uppercase tracking-wider', styles[status] ?? styles.cancelled)}>
            {status}
        </Badge>
    );
}

function SkeletonCard() {
    return (
        <Card>
            <CardContent className="pt-4">
                <div className="h-4 w-20 bg-brutal-zinc rounded animate-pulse mb-2" />
                <div className="h-7 w-32 bg-brutal-zinc rounded animate-pulse" />
            </CardContent>
        </Card>
    );
}

function formatEventTime(nanos?: number): string {
    if (!nanos) return '—';
    return new Date(nanos / 1_000_000).toLocaleTimeString('en-US', {
        hour12: false,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        fractionalSecondDigits: 3,
    });
}

const eventTypeStyles: Record<string, string> = {
    node_started: 'text-blue-400',
    node_completed: 'text-green-400',
    node_failed: 'text-red-400',
    workflow_started: 'text-cyan-400',
    workflow_completed: 'text-green-400',
    workflow_failed: 'text-red-400',
};

function EventRow({event}: {event: WorkflowEvent}) {
    const payloadPreview = event.payload_json
        ? event.payload_json.length > 120
            ? event.payload_json.slice(0, 120) + '…'
            : event.payload_json
        : null;

    return (
        <div className="flex items-start gap-4 px-4 py-2 border-b border-brutal-zinc hover:bg-brutal-dark/30 transition-colors">
            <span className="font-mono text-xs text-brutal-slate w-24 flex-shrink-0 pt-0.5">
                {formatEventTime(event.timestamp)}
            </span>
            <span className={cn('text-xs font-bold w-36 flex-shrink-0 pt-0.5', eventTypeStyles[event.event_type] ?? 'text-brutal-slate')}>
                {event.event_type}
            </span>
            <span className="font-mono text-xs text-brutal-slate w-28 flex-shrink-0 truncate pt-0.5">
                {event.node_id ?? '—'}
            </span>
            {payloadPreview && (
                <span className="font-mono text-[11px] text-brutal-zinc flex-1 truncate pt-0.5">
                    {payloadPreview}
                </span>
            )}
        </div>
    );
}

function CheckpointRow({checkpoint}: {checkpoint: WorkflowCheckpoint}) {
    return (
        <div className="flex items-center gap-4 px-4 py-2 border-b border-brutal-zinc hover:bg-brutal-dark/30 transition-colors">
            <Save className="w-3.5 h-3.5 text-brutal-slate flex-shrink-0" />
            <span className="font-mono text-xs text-brutal-slate w-24 flex-shrink-0">
                {formatEventTime(checkpoint.timestamp)}
            </span>
            <span className="font-mono text-xs text-brutal-slate w-28 flex-shrink-0 truncate">
                {checkpoint.node_id ?? '—'}
            </span>
            <span className="font-mono text-[11px] text-brutal-zinc flex-shrink-0 truncate">
                {checkpoint.checkpoint_id}
            </span>
        </div>
    );
}

export function WorkflowRunDetailPage() {
    const {runId} = useParams<{runId: string}>();
    const navigate = useNavigate();

    const {data: run, isLoading: runLoading, error: runError} = useWorkflowRun(runId ?? '');
    const {data: events = [], isLoading: eventsLoading} = useWorkflowEvents(runId ?? '');
    const {data: checkpoints = [], isLoading: checkpointsLoading} = useWorkflowCheckpoints(runId ?? '');

    if (runError) {
        return (
            <div className="p-6">
                <Card>
                    <CardContent className="py-12 text-center">
                        <AlertCircle className="w-12 h-12 mx-auto mb-4 text-red-500" />
                        <p className="text-red-400">Failed to load workflow run</p>
                        <p className="text-sm text-brutal-slate mt-2">
                            {runError instanceof Error ? runError.message : 'Unknown error'}
                        </p>
                    </CardContent>
                </Card>
            </div>
        );
    }

    const isLoading = runLoading;
    const durationMs = run?.duration_ns ? nsToMs(run.duration_ns) : null;

    return (
        <div className="p-6 space-y-6">
            {/* Back button + header */}
            <div className="flex items-center gap-4">
                <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => navigate('/workflows')}
                    className="text-brutal-slate hover:text-brutal-white"
                >
                    <ArrowLeft className="w-4 h-4 mr-1" />
                    Back
                </Button>

                {isLoading ? (
                    <div className="flex items-center gap-2">
                        <Loader2 className="w-5 h-5 animate-spin text-brutal-slate" />
                        <span className="text-brutal-slate">Loading…</span>
                    </div>
                ) : run ? (
                    <div className="flex items-center gap-3 flex-1">
                        <Workflow className="w-5 h-5 text-signal-orange" />
                        <h1 className="text-lg font-bold text-brutal-white tracking-wide">
                            {run.workflow_name ?? 'Workflow Run'}
                        </h1>
                        <StatusBadge status={run.status} />
                        {run.trigger && (
                            <Badge variant="outline" className="text-xs text-brutal-slate border-brutal-zinc">
                                {run.trigger}
                            </Badge>
                        )}
                    </div>
                ) : null}
            </div>

            {/* Stats cards */}
            <div className="grid grid-cols-5 gap-4">
                {isLoading ? (
                    <>
                        <SkeletonCard />
                        <SkeletonCard />
                        <SkeletonCard />
                        <SkeletonCard />
                        <SkeletonCard />
                    </>
                ) : run ? (
                    <>
                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <GitBranch className="w-4 h-4 text-cyan-500" />
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">NODES</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {run.completed_nodes}/{run.node_count}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Cpu className="w-4 h-4 text-cyan-500" />
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">INPUT TOKENS</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {run.input_tokens.toLocaleString()}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Cpu className="w-4 h-4 text-violet-500" />
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">OUTPUT TOKENS</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {run.output_tokens.toLocaleString()}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <DollarSign className="w-4 h-4 text-signal-green" />
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">TOTAL COST</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-signal-green font-mono">
                                    ${run.total_cost.toFixed(4)}
                                </div>
                            </CardContent>
                        </Card>

                        <Card>
                            <CardContent className="pt-4">
                                <div className="flex items-center gap-2">
                                    <Clock className="w-4 h-4 text-signal-orange" />
                                    <span className="text-[10px] font-bold text-brutal-slate tracking-wider">DURATION</span>
                                </div>
                                <div className="text-xl font-bold mt-1 text-brutal-white font-mono">
                                    {durationMs !== null ? formatDuration(durationMs) : '—'}
                                </div>
                            </CardContent>
                        </Card>
                    </>
                ) : null}
            </div>

            {/* IDs */}
            {run && (
                <div className="flex items-center gap-6 text-sm">
                    <div className="flex items-center gap-2">
                        <span className="text-brutal-slate text-xs font-bold tracking-wider">RUN ID</span>
                        <CopyableText value={run.run_id} label="Run ID" truncate maxWidth="140px" />
                    </div>
                    {run.trace_id && (
                        <div className="flex items-center gap-2">
                            <span className="text-brutal-slate text-xs font-bold tracking-wider">TRACE ID</span>
                            <CopyableText value={run.trace_id} label="Trace ID" truncate maxWidth="140px" textClassName="text-primary" />
                        </div>
                    )}
                </div>
            )}

            {/* Event stream */}
            {run && (
                <div>
                    <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em] mb-3">EVENT STREAM</h2>
                    <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                        <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                            <div className="w-24">TIME</div>
                            <div className="w-36">TYPE</div>
                            <div className="w-28">NODE</div>
                            <div className="flex-1">PAYLOAD</div>
                        </div>
                        {eventsLoading ? (
                            <div className="py-8 text-center">
                                <Loader2 className="w-5 h-5 mx-auto animate-spin text-brutal-slate" />
                            </div>
                        ) : events.length === 0 ? (
                            <div className="py-8 text-center text-brutal-slate text-sm">No events recorded</div>
                        ) : (
                            events.map((event) => (
                                <EventRow key={event.event_id} event={event} />
                            ))
                        )}
                    </div>
                </div>
            )}

            {/* Checkpoints */}
            {run && checkpoints.length > 0 && (
                <div>
                    <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em] mb-3">CHECKPOINTS</h2>
                    <div className="border-2 border-brutal-zinc rounded bg-brutal-carbon">
                        <div className="flex items-center gap-4 px-4 py-2 border-b-2 border-brutal-zinc text-[10px] font-bold text-brutal-slate tracking-wider">
                            <div className="w-4" />
                            <div className="w-24">TIME</div>
                            <div className="w-28">NODE</div>
                            <div className="flex-1">CHECKPOINT ID</div>
                        </div>
                        {checkpointsLoading ? (
                            <div className="py-8 text-center">
                                <Loader2 className="w-5 h-5 mx-auto animate-spin text-brutal-slate" />
                            </div>
                        ) : (
                            checkpoints.map((cp) => (
                                <CheckpointRow key={cp.checkpoint_id} checkpoint={cp} />
                            ))
                        )}
                    </div>
                </div>
            )}

            {/* Error message */}
            {run?.error_message && (
                <div>
                    <h2 className="text-xs font-bold text-brutal-slate tracking-[0.3em] mb-3">ERROR</h2>
                    <div className="text-sm text-red-400 bg-red-500/10 border-2 border-red-500/30 rounded p-4 font-mono whitespace-pre-wrap">
                        {run.error_message}
                    </div>
                </div>
            )}
        </div>
    );
}
